# 12 — Retention / Auto-Delete

**Stand:** 2026-09-26 · v2.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP2 v2.0 §2 Retention / Auto-Delete extrahiert.
- **v2.0 (2026-09-25, MVP2 v2.0-Pass):** HTTPS-Removal dokumentiert; Retention-Inhalt unverändert. Range-Validation `RetentionCheckIntervalSeconds` (1–86400).
- **v1.2 (2026-09-23):** `folder`-Retention-Expiry: Registry-Eintrag weg, Host-Folder bleibt, Re-Deploy setzt `path` + `updated_at`.
- **v1.1 (2026-09-23):** Lock-Semantik-Footer.
- **v1.0 (2026-09-23):** Initiale Spec.

## Ziel

Löscht Sites automatisch nach Ablauf ihrer TTL (`updated_at + retention_seconds`). Background-Timer im selben Prozess prüft regelmäßig. Default 7 Tage, Interval 1h.

## TTL-Scope

| `retention_seconds` Wert | Verhalten |
|---|---|
| weggelassen | Global Default aus `Retention:DefaultTtlSeconds` |
| `0` | Nie ablaufen |
| `> 0` | Ablauf nach N Sekunden ab `updated_at` |

`retention_seconds` aus [04 Tool: `deploy`](./04-tool-deploy.md) ist optional. Bei Site-Erstellung ohne Wert → Global Default (604800 = 7 Tage).

## Configuration

```json
{
  "Retention": {
    "Enabled": true,
    "DefaultTtlSeconds": 604800,
    "CheckIntervalSeconds": 3600
  }
}
```

| Key | Default | Range | Bedeutung |
|---|---|---|---|
| `Retention:Enabled` | `true` | – | Master-Switch |
| `Retention:DefaultTtlSeconds` | `604800` (7 Tage) | `> 0` | Globaler Site-TTL-Default |
| `Retention:CheckIntervalSeconds` | `3600` (1h) | `1..86400` | Timer-Intervall (Validation in `RetentionHostedService`-ctor) |

## Background-Service

`WebHosterMcp.Host.RetentionHostedService : BackgroundService`:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    if (!_options.Enabled) { _logger.LogInformation("deaktiviert"); return; }

    await SweepAsync(stoppingToken);

    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.CheckIntervalSeconds));
    while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
    {
        await SweepAsync(stoppingToken);
    }
}
```

Pro Tick (`SweepAsync`):

1. Lock auf `registry.json` (gleicher Mutex wie `SiteRegistry`)
2. Registry lesen
3. Für jede Site: `now > updated_at + retention_seconds` → expired?
4. Expired Sites hard-deletet (siehe §Hard-Delete pro Hosting-Typ)
5. Registry zurückschreiben (atomic via tmp + rename)
6. Lock freigeben

Log pro Expiry:

```
Site expired: <site_path> (ttl=<n>s, age=<age>s)
```

Erster Sweep läuft sofort beim Start (`await SweepAsync` vor Timer-Loop), damit Sites, die zwischen Iterationen-Disabled und Re-Enable abgelaufen sind, weggefegt werden.

## Hard-Delete pro Hosting-Typ

| Hosting-Typ | Bei Expiry |
|---|---|
| `files` | `rm -rf <SitesRoot>/<site>/` + Registry-Eintrag weg |
| `a2ui` | `rm -rf <SitesRoot>/<site>/` (enthält `payload.json`) + Registry-Eintrag weg |
| `json-schema-form` | `rm -rf <SitesRoot>/<site>/` (enthält `payload.json` + alle `<submission-id>.json`) + Registry-Eintrag weg |
| `folder` | **Nur Registry-Eintrag** weg; Host-Folder unangetastet; bei Re-Deploy werden `path` und `updated_at` neu gesetzt (TTL-Reset) |

Sonderfall `folder`: User behält die Kontrolle über den externen Folder. Web Hoster vergisst ihn nur (Registry-Eintrag). Re-Deploy auf demselben `site_path` setzt `path` und `updated_at` zurück, was den TTL-Counter neu startet.

## TTL-Reset

Jeder erfolgreiche `deploy` (egal welcher Type) updated `updated_at`. Damit:

- Re-Deploy einer fast abgelaufenen Site resetet die TTL
- `retention_seconds` kann beim Update geändert werden (`?retention_seconds=N` im [04 Tool: `deploy`](./04-tool-deploy.md))

## Sweep-Konflikt mit anderen Ops

Da `SweepAsync` unter demselben Mutex wie `SiteRegistry.DeployAsync` / `DeleteAsync` läuft:

- Konflikt-Szenarien (`Sweep` will löschen, paralleler `deploy` updatet): Lock serialisiert; entweder Sweep oder Deploy gewinnt; idempotent auf der anderen Seite
- `RetentionHostedService.SweepAsync` wird in Tests reflektiv via `BindingFlags.NonPublic.Invoke` aufgerufen, um deterministisch (timer-frei) zu testen (siehe `ServerE2ETests.Retention_DeletesExpiredSite`)

## Master-Switch

`Retention:Enabled = false` deaktiviert den ganzen Service:

```
Returnwert: kein Sweep, keine Timer
Log: "Retention-Service deaktiviert."
```

Damit kann der Service in Development / Manual-Cleanup-Setups ausgeschaltet werden, ohne den Code zu ändern.

## Errors

Retention hat **keine** Domain-Errors. IO-Errors beim Hard-Delete (z. B. gesperrte Datei, Permission) werden geloggt:

```
Fehler beim Löschen der Site: <site_path>, <exception>
```

und führen nicht zu Registry-Korruption (Sweep schreibt Registry erst nach erfolgreichem Delete).

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — Registry-Mutation
- [04 Tool: `deploy`](./04-tool-deploy.md) — `retention_seconds`-Input + TTL-Reset
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — gleiche Hard-Delete-Logik
- [11 Delete-Routes](./11-delete-routes.md) — HTTP-DELETE-`/<site>` (gleiche Logik)
- [15 Hosting Type: `folder`](./15-hosting-type-folder.md) — Sonderfall: Registry-only-Delete
- [16 Hosting Type: `a2ui`](./16-hosting-type-a2ui.md) — `payload.json`-Hard-Delete
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — `payload.json` + Submissions

## Out of Scope

- Soft-Delete mit Trash-Folder / Recovery
- TTL-Events / Push-Notifications an KI
- Pre-Expiry-Warning (z. B. "Site läuft in 1h ab")
- Retention per User / per Folder-Target (kein Multi-User-Modell)
- Cross-Server-Koordination (mehrere Web-Hoster-Instanzen → Sweep kann Site doppelt angreifen, idempotent OK)
- TTL für `path`-Retention-Default (kann via `Retention:DefaultTtlSeconds` global überschrieben werden)
