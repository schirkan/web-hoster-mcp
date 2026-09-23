# MVP2 — HTTPS + Retention + HTTP-Delete-Endpoints

Stand: 2026-09-23 · v1.0 (lock)

## Ziel

MVP2 erweitert MVP1 um drei Features:

- **HTTPS-Endpoint** parallel zu HTTP (PFX-Cert mit Self-Signed Fallback)
- **Retention / Auto-Delete** per Site (TTL seit `updated_at`, Background-Timer, Hard Delete; Default 7 Tage, Interval 1h)
- **HTTP-Delete-Endpoints** mit Confirm-Pattern für Browser-User (`[delete]`-Links in Sites-Index und File-Listing)

Referenzen: `specs/mvp1.md` (Basis), `specs/mvp4-render-types.md` (Render-Types für Type-bezogenes Verhalten).

## 1. Server (Update zu MVP1)

Zusätzlich zum HTTP-Listener startet der Server einen HTTPS-Listener.

| Listener | Address | Default |
|----------|---------|---------|
| HTTP    | `<Host:Ip>:<Host:Port>`           | `0.0.0.0:3000` |
| HTTPS   | `<Host:Ip>:<Host:HttpsPort>`     | `0.0.0.0:3443` |

- Gleiche IP wie HTTP (`Host:Ip`).
- HTTPS abschaltbar: `Host:UseHttps = false` oder `Host:HttpsPort = 0`/fehlt → kein HTTPS-Listener.
- HTTP und HTTPS laufen parallel — **kein** HTTP→HTTPS Redirect im MVP.

## 2. HTTPS — Cert-Quelle

Reihenfolge der Cert-Auflösung beim Server-Start:

```
1. appsettings.json:Https.CertPath gesetzt UND Datei existiert
   → PFX laden (Password aus appsettings.json:Https.CertPassword)

2. sonst, appsettings.json:Https.SelfSigned.Enabled = true
   → Self-Signed Cert generieren (oder vorhandenes laden)

3. sonst
   → HTTPS nicht verfügbar; bei UseHttps=true → Startup-Fehler
```

### 2.1 PFX (manuell bereitgestellt)

- Pfad: `appsettings.json:Https.CertPath` (relativ zu Working-Directory oder absolut).
- Password: `appsettings.json:Https.CertPassword`.
- Server lädt Cert beim Start; Fehler → Startup-Fehler mit klarem Log.

### 2.2 Self-Signed Fallback

Aktiv wenn `appsettings.json:Https.SelfSigned.Enabled = true` UND kein PFX geladen.

**Generierung (einmalig, persistiert):**

- RSA 2048, SHA-256, `RSASignaturePadding.Pkcs1`.
- `CN` = `appsettings.json:Https.SelfSigned.Cn` falls gesetzt, sonst `Environment.MachineName`.
- Gültigkeit: 1 Jahr (`NotBefore = DateTimeOffset.UtcNow.AddDays(-1)`, `NotAfter = DateTimeOffset.UtcNow.AddYears(1)`).
- Export als PFX (Password aus `appsettings.json:Https.CertPassword`).
- Speichern unter `<Https.SelfSigned.CertDir>/<sanitized-hostname>.pfx`.
- Idempotent: bei späteren Starts wird vorhandenes Cert wiederverwendet (kein Re-Generate wenn File existiert und gültig).

**Browser-Warnung:**

Self-Signed-Certs lösen in Browsern eine Sicherheits-Warnung aus — im LAN akzeptiert. User müssen einmalig eine Ausnahme hinzufügen.

**Self-Signed Override per Force-Option:**

Wenn `appsettings.json:Https.SelfSigned.ForceRegenerate = true` → Cert immer neu generieren (überschreibt vorhandenes File). Für Tests / Cert-Rotation.

## 3. Retention / Auto-Delete

### 3.1 Konzept

- Sites bekommen ein optionales `retention_seconds`-Feld in der Registry.
- TTL startet ab `updated_at`-Zeitstempel — Site lebt länger wenn neu beschrieben.
- Background-Timer im selben Prozess prüft regelmäßig alle Sites.
- Bei Expiry: **Hard Delete** — `rm -rf <SitesRoot>/<site>/` + Registry-Eintrag weg.

### 3.2 TTL-Scope

| `deploy.retention_seconds` Wert | Verhalten |
|----------------------------------|-----------|
| weggelassen | Global Default aus `Retention:DefaultTtlSeconds` |
| `0` | Nie ablaufen |
| `> 0` | Ablauf nach N Sekunden ab `updated_at` |

Bei Site-Erstellung ohne `retention_seconds`-Angabe → Global Default wird in Registry eingetragen.

### 3.3 Background-Service

`IHostedService` mit `Timer` (in `Microsoft.Extensions.Hosting`):

```
RetentionCheckIntervalSeconds = appsettings.json:Retention:CheckIntervalSeconds (Default: 3600 = 1h)
```

Pro Tick:
1. Lock auf `registry.json` (gleicher Mutex wie in MVP1).
2. Registry lesen.
3. Für jede Site: wenn `DateTimeOffset.UtcNow > updated_at + retention_seconds` → expired.
4. Expired Sites hard-deletet (siehe 3.4).
5. Registry zurückschreiben (atomic via temp + rename).
6. Lock freigeben.

Loggt pro Expiry: `Site expired: <site_path> (ttl=<n>s, age=<age>s)`.

### 3.4 Hard-Delete-Verhalten pro Render-Type

| Render-Type | Bei Expiry |
|-------------|------------|
| `files`       | `rm -rf <SitesRoot>/<site>/` + Registry weg |
| `a2ui`       | `rm -rf <SitesRoot>/<site>/` (enthaelt `payload.json`) + Registry weg |
| `schema-form` | `rm -rf <SitesRoot>/<site>/` (enthaelt `payload.json` + alle `<submission-id>.json`) + Registry weg |
| `folder`      | **Nur** Registry weg — Host-Folder bleibt unangetastet |

## 4. HTTP-Delete-Endpoints

### 4.1 Confirm-Pattern (verhindert Link-Prefetch-Loeschen)

```
GET /<site>/delete                → Bestätigungs-Seite (HTML, 200)
GET /<site>/delete?confirm=yes    → Loeschen, 302 Redirect zu /
GET /<site>/delete?confirm=no     → Cancel, 302 Redirect zu /
```

Analog für File-Delete:

```
GET /<site>/delete-file/<path>             → Bestätigungs-Seite (HTML, 200)
GET /<site>/delete-file/<path>?confirm=yes → Loeschen, 302 Redirect zu /<site>/
GET /<site>/delete-file/<path>?confirm=no  → Cancel, 302 Redirect zu /<site>/
```

URL-Encoding: `path` im URL ist URL-encoded; Server decodiert.

### 4.2 Site-Delete (`/delete`)

Verfügbar für **alle Render-Types**.

| Render-Type | Verhalten nach `confirm=yes` |
|-------------|-------------------------------|
| `files`       | Hard-Delete (siehe 3.4) |
| `folder`      | Nur Registry weg (Host-Folder bleibt) |
| `a2ui`       | Hard-Delete |
| `schema-form` | Hard-Delete inkl. aller `<submission-id>.json` |

### 4.3 File-Delete (`/delete-file/<path>`)

Verfügbar **nur** für `type: files`. Für andere Types → `404`:

| Render-Type | File-Delete-Endpoint |
|-------------|----------------------|
| `files`       | aktiv |
| `folder`      | `404` (kein File-Delete bei folder, per MVP4) |
| `a2ui`       | `404` (kein File-Listing, daher kein Delete-Link) |
| `schema-form` | `404` (kein File-Listing) |

Bei `type: files` löscht das Endpoint das File via `deploy(mode: merge, files: [{path, delete: true}])`-Semantik (oder direktem File.delete + Registry-Update).

### 4.4 Bestätigungs-Seiten

Einfache HTML-Seiten, inline CSS (300-400 Byte). Inhalte:

**`/<site>/delete`:**

```html
<h1>Site &quot;<site_path>&quot; löschen?</h1>
<p>Alle Files und der Registry-Eintrag werden entfernt.
   Bei <code>type: folder</code> bleibt der referenzierte Host-Folder erhalten.</p>
<a href="/<site>/delete?confirm=yes">Ja, löschen</a>
<a href="/">Abbrechen</a>
```

**`/<site>/delete-file/<path>`:**

```html
<h1>File &quot;<path>&quot; löschen?</h1>
<p>Aus Site &quot;<site_path>&quot; entfernen.</p>
<a href="/<site>/delete-file/<url-encoded-path>?confirm=yes">Ja, löschen</a>
<a href="/<site>/">Abbrechen</a>
```

HTML-Escaping aller Pfade (XSS-Schutz, identisch zum Directory-Listing).

## 5. Delete-Links in Listings

Sites-Index (`GET /`, siehe `specs/mvp2-directory-listing.md`):

```html
<li>
  <a href="/demo-001/">demo-001</a>
  <span>files · 3 Dateien</span>
  <span>2026-09-22 16:50</span>
  <a href="/demo-001/delete">[delete]</a>
</li>
```

Site-Listing (`GET /<site>/`, nur type:files und type:folder):

```html
<li>
  <a href="/demo-001/index.html">index.html</a>
  <span>2026-09-22 16:50</span>
  <a href="/demo-001/delete-file/index.html">[delete]</a>
</li>
```

Für `type: a2ui` und `type: schema-form` werden **keine** Delete-Links im File-Bereich gerendert (kein File-Listing), aber Site-Delete-Link ist im Sites-Index vorhanden.

## 6. Configuration (`appsettings.json`)

```json
{
  "Host": {
    "Ip": "0.0.0.0",
    "Port": 3000,
    "HttpsPort": 3443,
    "UseHttps": true
  },
  "Https": {
    "CertPath": "./certs/site.pfx",
    "CertPassword": "changeme",
    "SelfSigned": {
      "Enabled": true,
      "CertDir": "./certs",
      "Cn": null,
      "ForceRegenerate": false
    }
  },
  "Retention": {
    "Enabled": true,
    "DefaultTtlSeconds": 604800,
    "CheckIntervalSeconds": 3600
  },
  "SitesRoot": "./sites",
  "MaxFileSizeBytes": 1048576
}
```

| Key | Default | Bedeutung |
|-----|---------|-----------|
| `Host:HttpsPort` | `3443` | HTTPS-Port. Effektiv abgeschaltet wenn `0`/fehlt |
| `Host:UseHttps` | `true` | Master-Switch für HTTPS |
| `Https:CertPath` | `null` | PFX-Pfad. `null` = kein PFX, Fallback auf Self-Signed |
| `Https:CertPassword` | `null` | PFX-Password. Empfohlen: in `appsettings.Local.json` auslagern |
| `Https:SelfSigned:Enabled` | `true` | Self-Signed Fallback aktiv |
| `Https:SelfSigned:CertDir` | `./certs` | Verzeichnis für generiertes/gespeichertes Cert |
| `Https:SelfSigned:Cn` | `null` | Custom CN, sonst `Environment.MachineName` |
| `Https:SelfSigned:ForceRegenerate` | `false` | Cert neu generieren (überschreibt vorhandenes) |
| `Retention:Enabled` | `true` | Master-Switch für Retention-Service |
| `Retention:DefaultTtlSeconds` | `604800` (7 Tage) | Global Default für Site-TTL |
| `Retention:CheckIntervalSeconds` | `3600` (1h) | Background-Timer-Intervall |

**Sicherheitshinweis:** `Https:CertPassword` liegt aktuell in
`appsettings.json` (Klartext). Für Production in
`appsettings.Local.json` (gitignored) auslagern — separate Story,
out of scope hier.

## 7. `registry.json` (Update zu MVP1)

```json
{
  "version": 1,
  "sites": {
    "demo-001": {
      "site_path": "demo-001",
      "type": "files",
      "created_at": "2026-09-22T19:25:00Z",
      "updated_at": "2026-09-22T19:30:00Z",
      "retention_seconds": 0
    }
  }
}
```

`retention_seconds` ist **optional**:

| Wert | Bedeutung |
|------|-----------|
| Feld fehlt | Global Default aus `Retention:DefaultTtlSeconds` |
| `0` | Nie ablaufen |
| `> 0` | Ablauf N Sekunden nach `updated_at` |

## 8. Tool-Inputs/-Outputs (Updates zu MVP1)

### 8.1 `deploy` — Input-Update

```json
{
  "site_path": "demo-001",
  "type": "files",
  "mode": "merge",
  "retention_seconds": 3600,
  "files": [
    {"path": "index.html", "content": "<!DOCTYPE html>..."}
  ]
}
```

| `retention_seconds` Wert | Effekt auf Registry |
|--------------------------|---------------------|
| weggelassen | Bestehender Wert bleibt; bei neuer Site → Global Default |
| `0` | `retention_seconds: 0` in Registry |
| `> 0` | `retention_seconds: N` in Registry, `updated_at` aktualisiert |

### 8.2 `list_sites` / `get_site_info` — Output-Update

Beide Tools liefern zusätzlich:

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `retention_seconds` | `number` | Effektiver Wert (Override oder Global Default) |
| `expires_at` | `string \| null` | ISO-8601 des Auto-Expire-Zeitpunkts. `null` wenn `retention_seconds: 0` |

`expires_at` wird **bei jedem Read berechnet** (`updated_at + retention_seconds`), nicht persistiert.

## 9. HTTP-Serving (Update zu MVP1)

### 9.1 HTTPS

```
https://<ip>:3443/<site>/<file>     → gleiches Verhalten wie HTTP (mit TLS)
https://<ip>:3443/<site>/          → Directory-Listing (siehe mvp2-directory-listing.md)
https://<ip>:3443/                 → Sites-Index
https://<ip>:3443/<site>/<file>     → für type:a2ui/schema-form: 404
```

Self-Signed-Cert → Browser-Warnung beim ersten Besuch.

### 9.2 Delete-Endpoints

```
GET  /<site>/delete                          → alle Types (Bestätigung)
GET  /<site>/delete?confirm=yes             → alle Types (Loeschen)
GET  /<site>/delete-file/<path>             → nur type:files (Bestätigung)
GET  /<site>/delete-file/<path>?confirm=yes  → nur type:files (Loeschen)
```

Alle Endpoints liefern HTML (Bestätigung oder 302 nach Action).

### 9.3 Bestehende Routes (unverändert zu MVP1 + MVP4)

- `GET /` → Sites-Index (jetzt mit `[delete]`-Links pro Site und Type-Label)
- `GET /<site>/` → Site-Listing (jetzt mit `[delete]`-Links pro File, nur type:files/folder)
- `GET /<site>/<file>` → Static File Serving
- `GET /<site>/submit` → nur type:schema-form (MVP4)
- `GET /<site>/<unknown>` → 404

## 10. Error Codes (MVP2-Ergaenzungen)

| Code | Wann |
|------|------|
| `site_not_found` | Delete-Endpoint für nicht-existente Site |
| `path_not_found` | File-Delete für nicht-existente File |
| `delete_not_allowed` | File-Delete-Endpoint bei type ≠ files |
| `cert_load_failed` | PFX konnte nicht geladen werden |
| `self_signed_failed` | Self-Signed Cert konnte nicht generiert werden |
| `https_startup_failed` | HTTPS-Listener konnte nicht starten |
| `internal_error` | Unerwarteter Server-Fehler |

Hinweis: `path_traversal` wird **nicht** mehr ausgelöst (Trust-Modell, etabliert in MVP4).

## 11. Out of Scope (MVP2)

- HTTP → HTTPS Redirect (parallel-Betrieb bleibt)
- Let's Encrypt / ACME
- HTTPS-Cert-Rotation / automatischer Renewal (ausser `ForceRegenerate` für manuelles Re-Generate)
- Soft-Delete mit Trash-Folder / Recovery
- Auth am Submit/Delete-Endpoint (LAN-only)
- TTL-Events / Push-Notifications an KI
- Cluster-Self-Signed-Cert-Verteilung
- Secret-Store für `CertPassword` (lands in `appsettings.Local.json`, out of scope hier)
- mTLS / Client-Cert-Auth
- HSTS-Header
- `appsettings.Development.json`-Trennung (existiert noch nicht)
- Retention per User / per Folder-Target
