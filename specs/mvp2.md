# MVP2 — Retention + HTTP-Delete-Endpoints

Stand: 2026-09-25 · v2.0 (lock)

## Changelog

- **v2.0 (2026-09-25):** HTTPS-Endpoint-Feature komplett entfernt. Server liefert nur noch HTTP. Betroffen:
  - `Host:HttpsPort` und `Host:UseHttps` Config-Keys entfernt
  - `Https`-Section aus `appsettings.json` entfernt
  - HTTPS-Listener in `Program.cs` entfernt (kein paralleler Listener mehr)
  - `HttpsOptions`/`HttpsSelfSignedOptions` aus `HostOptions.cs` entfernt; `HttpsCertificateLoader.cs` gelöscht
  - `certs/`-Folder mit Self-Signed-PFX entfernt
  - §2 (HTTPS Cert-Quelle) komplett gestrichen
  - §9.1 (HTTPS-Serving) gestrichen
  - Error-Codes `cert_load_failed`, `self_signed_failed`, `https_startup_failed` entfernt
  - **`src`-Downloads via `https://`-URLs bleiben unterstützt** (Client-seitiger Download — separater Concern vom Server-Endpoint, siehe `specs/mvp3.md`)
- **v1.3 (2026-09-23):** Range-Validation für `RetentionCheckIntervalSeconds` (1–86400). `folder`-Retention-Expiry: Registry-Eintrag weg, Host-Folder bleibt, bei Re-Deploy werden `path` + `updated_at` neu gesetzt.
- **v1.2 (2026-09-23):** HTTP-Delete-Endpoints von GET+Confirm auf **DELETE-Methode** umgestellt (kein Confirm-Pattern, kein `/file/`-Segment); Browser-UI nutzt JS-Buttons mit `fetch(..., {method: 'DELETE'})`.
- **v1.1 (2026-09-23):** Lock-Semantik-Footer.
- **v1.0 (2026-09-23):** Initiale Spec (HTTPS + Retention + HTTP-Delete-Endpoints).

## Ziel

MVP2 erweitert MVP1 um zwei Features:

- **Retention / Auto-Delete** per Site (TTL seit `updated_at`, Background-Timer, Hard Delete; Default 7 Tage, Interval 1h)
- **HTTP-Delete-Endpoints** mit **DELETE-Methode** (kein Confirm-Pattern, kein Prefetch-Risiko) + JS-Buttons in Listings für Browser-User

Referenzen: `specs/mvp1.md` (Basis), `specs/mvp4-render-types.md` (Hosting-Typen für `type`-bezogenes Verhalten).

## 1. Server (Update zu MVP1)

Server startet **einen** HTTP-Listener auf `<Host:Ip>:<Host:Port>` (Default `0.0.0.0:3000`).

| Listener | Address | Default |
|----------|---------|---------|
| HTTP    | `<Host:Ip>:<Host:Port>`           | `0.0.0.0:3000` |

- **Kein HTTPS-Listener mehr** (entfernt in v2.0). Es gibt keine HTTPS-Config und keine Cert-Generierung.
- Kein HTTP→HTTPS Redirect (kein HTTPS mehr).

## 2. Retention / Auto-Delete

### 2.1 Konzept

- Sites bekommen ein optionales `retention_seconds`-Feld in der Registry.
- TTL startet ab `updated_at`-Zeitstempel (lokale Server-Zeit) — Site lebt länger wenn neu beschrieben.
- Background-Timer im selben Prozess prüft regelmäßig alle Sites.
- Bei Expiry: **Hard Delete** — `rm -rf <SitesRoot>/<site>/` + Registry-Eintrag weg.

### 2.2 TTL-Scope

| `deploy.retention_seconds` Wert | Verhalten |
|----------------------------------|-----------|
| weggelassen | Global Default aus `Retention:DefaultTtlSeconds` |
| `0` | Nie ablaufen |
| `> 0` | Ablauf nach N Sekunden ab `updated_at` |

Bei Site-Erstellung ohne `retention_seconds`-Angabe → Global Default wird in Registry eingetragen.

### 2.3 Background-Service

`IHostedService` mit `Timer` (in `Microsoft.Extensions.Hosting`):

```
RetentionCheckIntervalSeconds = appsettings.json:Retention:CheckIntervalSeconds
  Erlaubter Range: 1 ≤ RetentionCheckIntervalSeconds ≤ 86400 (1 Tag)
  Default: 3600 (= 1h)
```

Pro Tick:
1. Lock auf `registry.json` (gleicher Mutex wie in MVP1).
2. Registry lesen.
3. Für jede Site: wenn `DateTime.Now > updated_at + retention_seconds` → expired.
4. Expired Sites hard-deletet (siehe 2.4).
5. Registry zurückschreiben (atomic via temp + rename).
6. Lock freigeben.

Loggt pro Expiry: `Site expired: <site_path> (ttl=<n>s, age=<age>s)`.

### 2.4 Hard-Delete-Verhalten pro Hosting-Typ

| Hosting-Typ | Bei Expiry |
|-------------|------------|
| `files`       | `rm -rf <SitesRoot>/<site>/` + Registry weg |
| `a2ui`       | `rm -rf <SitesRoot>/<site>/` (enthält `payload.json`) + Registry weg |
| `schema-form` | `rm -rf <SitesRoot>/<site>/` (enthält `payload.json` + alle `<submission-id>.json`) + Registry weg |
| `folder`      | **Nur Registry-Eintrag** weg — Host-Folder bleibt unangetastet. Bei Re-Deploy werden `path` und `updated_at` neu gesetzt (TTL-Reset). |

## 3. HTTP-Delete-Endpoints (DELETE-Methode)

### 3.1 DELETE auf Resource-URL

DELETE auf der Resource-URL — direkt destruktiv, **kein** Confirm-Pattern, **kein** `/file/`-Segment:

```
DELETE /<site>             → Site löschen, 302 → /
DELETE /<site>/<file>      → File löschen, 302 → /<site>/
```

**Begründung:**

- DELETE ist der HTTP-Spec-konforme Verb für Destroy-Operationen
- DELETE wird **nicht** von Browsern prefetched (im Gegensatz zu GET)
- **Ein** Round-Trip statt zwei (kein Confirm-Page nötig)
- RESTful + konsistent mit anderen HTTP-Tools (curl, postman)
- Kein `?confirm=yes`-Param-Clutter in URLs

### 3.2 Hosting-Typ-spezifisches Verhalten

| Hosting-Typ | `DELETE /<site>` | `DELETE /<site>/<file>` |
|-------------|------------------|------------------------|
| `files`       | Hard-Delete (Site-Folder + Registry) | File löschen |
| `folder`      | **Nur Registry** weg (Host-Folder bleibt!) | **404** (kein File-Delete bei folder, per MVP4) |
| `a2ui`       | Hard-Delete (payload.json + Registry) | **404** (kein File-Listing) |
| `schema-form` | Hard-Delete (payload.json + Submissions + Registry) | **404** (kein File-Listing) |

### 3.3 Browser-UI (JS-Buttons mit fetch + DELETE)

Plain-HTML-Links können DELETE nicht direkt aufrufen. Browser-UI
nutzt JavaScript-Buttons mit `fetch`:

```html
<!-- Sites-Index: Delete-Button pro Site -->
<li>
  <a href="/demo-001/">demo-001</a>
  <span>files · 3 Dateien</span>
  <span>2026-09-22 16:50</span>
  <button data-delete-site="demo-001" class="delete-btn">Delete</button>
</li>

<!-- Site-Listing (files/folder): Delete-Button pro File -->
<li>
  <a href="/demo-001/index.html">index.html</a>
  <span>2026-09-22 16:50</span>
  <button data-delete-file="index.html" data-site="demo-001" class="delete-btn">Delete</button>
</li>

<script>
document.querySelectorAll('[data-delete-site]').forEach(btn => {
  btn.onclick = async () => {
    const site = btn.dataset.deleteSite;
    if (!confirm(`Site "${site}" wirklich löschen?`)) return;
    const res = await fetch('/' + site, {method: 'DELETE'});
    if (res.ok) location.href = '/';
    else alert('Fehler: ' + res.status);
  };
});

document.querySelectorAll('[data-delete-file]').forEach(btn => {
  btn.onclick = async () => {
    const site = btn.dataset.site;
    const filePath = btn.dataset.deleteFile;
    if (!site) { alert('Site-Kontext fehlt'); return; }
    if (!confirm(`File "${filePath}" wirklich löschen?`)) return;
    const res = await fetch(
      '/' + site + '/' + filePath,
      {method: 'DELETE'}
    );
    if (res.ok) location.href = '/' + site + '/';
    else alert('Fehler: ' + res.status);
  };
});
</script>
```

Native `confirm()`-Dialog ersetzt die Server-Confirm-Page. UX ist
**1-Klick** statt 2-Klick.

### 3.4 CSP / Security-Hinweise

DELETE erfordert JS im Browser. Bei deaktiviertem JS:
- Delete-Buttons nicht funktional (graceful degradation)
- Sites trotzdem über `delete_site`-Tool löschbar (MCP-Pfad)

Für Authentifizierung der HTTP-Endpoints (geplant für Production-Einsatz
außerhalb LAN) siehe `specs/mvp5-authorization.md` (Draft).

### 3.5 MCP-Tool `delete_site` — unverändert

KI benutzt weiterhin `delete_site` über MCP. Das HTTP-DELETE-Endpoint
ist **nur für die Browser-UI** (Directory-Listings). Beide Pfade führen
zur gleichen `SiteManager.DeleteSite()`-Logik im Code.

## 4. Delete-Buttons in Listings

Sites-Index (`GET /`, siehe `specs/mvp2-directory-listing.md`):

```html
<li>
  <a href="/demo-001/">demo-001</a>
  <span>files · 3 Dateien</span>
  <span>2026-09-22 16:50</span>
  <button data-delete-site="demo-001" class="delete-btn">Delete</button>
</li>
```

Site-Listing (`GET /<site>/`, nur type:files und type:folder):

```html
<li>
  <a href="/demo-001/index.html">index.html</a>
  <span>2026-09-22 16:50</span>
  <button data-delete-file="index.html" data-site="demo-001" class="delete-btn">Delete</button>
</li>
```

JS-Handler (siehe §3.3) macht das eigentliche DELETE.

Für `type: a2ui` und `type: schema-form` werden **keine** Delete-Buttons
im File-Bereich gerendert (kein File-Listing), aber Site-Delete-Button
ist im Sites-Index vorhanden.

## 5. Configuration (`appsettings.json`)

```json
{
  "Host": {
    "Ip": "0.0.0.0",
    "Port": 3000
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
| `Retention:Enabled` | `true` | Master-Switch für Retention-Service |
| `Retention:DefaultTtlSeconds` | `604800` (7 Tage) | Global Default für Site-TTL |
| `Retention:CheckIntervalSeconds` | `3600` (1h) | Background-Timer-Intervall (Range: 1–86400) |

**Hinweis:** Bis v1.3 gab es einen `Https`-Block + `Host:UseHttps` + `Host:HttpsPort` für den HTTPS-Listener. Diese wurden in v2.0 entfernt — der Server liefert ausschließlich HTTP.

## 6. `registry.json` (Update zu MVP1)

```json
{
  "version": 1,
  "sites": {
    "demo-001": {
      "site_path": "demo-001",
      "type": "files",
      "created_at": "2026-09-22T19:25:00",
      "updated_at": "2026-09-22T19:30:00",
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

## 7. Tool-Inputs/-Outputs (Updates zu MVP1)

### 7.1 `deploy` — Input-Update

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

### 7.2 `list_sites` / `get_site_info` — Output-Update

Beide Tools liefern zusätzlich:

| Feld | Typ | Beschreibung |
|------|-----|--------------|
| `retention_seconds` | `number` | Effektiver Wert (Override oder Global Default) |
| `expires_at` | `string \| null` | ISO-8601 des Auto-Expire-Zeitpunkts (lokale Zeit). `null` wenn `retention_seconds: 0` |

`expires_at` wird **bei jedem Read berechnet** (`updated_at + retention_seconds`), nicht persistiert.

## 8. HTTP-Serving (Update zu MVP1)

### 8.1 Bestehende Routes (unverändert zu MVP1 + MVP4)

- `GET /` → Sites-Index (mit JS-Delete-Buttons pro Site und Type-Label)
- `GET /<site>/` → Site-Listing (mit JS-Delete-Buttons pro File, nur type:files/folder)
- `GET /<site>/<file>` → Static File Serving
- `GET /<site>/submit` → nur type:schema-form (MVP4)
- `GET /<site>/<unknown>` → 404

### 8.2 Delete-Endpoints (DELETE-Methode)

```
DELETE /<site>                → Site löschen, 302 → /
DELETE /<site>/<file>         → File löschen, 302 → /<site>/
```

DELETE wird **nicht** von Browsern prefetched — kein Confirm-Pattern nötig.
Browser-UI nutzt JS-Buttons mit `fetch(..., {method: 'DELETE'})` (siehe §3.3).

## 9. Error Codes (MVP2-Ergaenzungen)

| Code | Wann |
|------|------|
| `site_not_found` | DELETE für nicht-existente Site |
| `path_not_found` | DELETE /<site>/<file> für nicht-existente File |
| `internal_error` | Unerwarteter Server-Fehler |

Hinweis: `path_traversal` wird **nicht** mehr ausgelöst (Trust-Modell, etabliert in MVP4). DELETE für nicht erlaubte Hosting-Typen (z. B. `/<site>/<file>` bei folder/a2ui/schema-form) → **404** vom Kestrel-Router (kein Endpoint registriert), nicht ein typisierter Error.

`cert_load_failed`, `self_signed_failed`, `https_startup_failed` wurden in v2.0 entfernt (HTTPS-Endpoint entfällt).

## 10. Out of Scope (MVP2)

- **HTTPS-Endpoint** (entfernt in v2.0 — siehe Changelog oben)
- HTTP → HTTPS Redirect (entfällt mit HTTPS)
- Let's Encrypt / ACME (entfällt)
- HTTPS-Cert-Rotation / Renewal (entfällt)
- mTLS / Client-Cert-Auth
- HSTS-Header
- Soft-Delete mit Trash-Folder / Recovery
- TTL-Events / Push-Notifications an KI
- Secret-Store für `CertPassword` (entfällt)
- `appsettings.Development.json`-Trennung (existiert noch nicht)
- Retention per User / per Folder-Target
- Confirm-Page für DELETE (entfernt in v1.2 — Browser-UI nutzt native `confirm()`)
- Authorization für HTTP-Endpoints (siehe `specs/mvp5-authorization.md` Draft)

> Versionierung: v1.0 = final; Änderungen → v1.1/v2.0-Bump mit Changelog oben.
