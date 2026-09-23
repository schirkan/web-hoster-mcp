# MVP3 — per-File `src`-Parameter

Stand: 2026-09-23 · v1.0 (lock)

## Ziel

MVP3 erweitert MVP1's `deploy`-Tool um eine alternative File-Quelle
pro Eintrag: statt inline Content (MVP1 `content`) referenziert
`src` eine externe Quelle (Data URL, lokaler Pfad, oder HTTP/HTTPS-URL).
Die Datei wird **physisch** in `<SitesRoot>/<site>/<path>` kopiert.

**Semantische Abgrenzung:**

| Field/Type | Quelle | Verhalten |
|------------|--------|-----------|
| MVP1 `content` | inline (nur plain string) | Datei entsteht neu mit dem String als UTF-8 |
| **MVP3 `src`** | Data URL / lokaler Pfad / HTTP(S) URL | **physische Kopie** in `<SitesRoot>/<site>/<path>` |
| MVP4 `type: "folder"` | kompletter Host-Ordner | **keine Kopie**, Live-Reference (kein Site-Folder) |

`src` = **eine Datei reinholen** (kopieren), `folder` = **ganzer Ordner ohne Kopie**. Beide ergänzen MVP1 (`content` inline), sind orthogonal zueinander.

**Trust-Modell:** keine Validierung der Quelle — keine Path-Traversal-Checks, keine Zertifikats-Validierung, keine Netzwerk-Sandboxing. KI trägt die Verantwortung (analog MVP4 `folder`-Type und MVP1 Trust-Modell).

## Erkennungs-Logik (3 Quellen)

```
if (src.startsWith("data:"))
    → Data-URL parsen, base64 dekodieren, atomic write
else if (src.startsWith("http://") || src.startsWith("https://"))
    → HttpClient.GetByteArrayAsync(), atomic write
else
    → File.ReadAllBytes() (lokaler Pfad), atomic write
```

Alle drei Pfade führen zur gleichen Zielstruktur: `<SitesRoot>/<site>/<path>` mit atomic-write (tmp + rename).

## API

### `deploy`-Input (Erweiterung zu MVP1)

`files[]`-Eintrag kann jetzt entweder `content` ODER `src` enthalten (mutually exclusive):

```json
{
  "site_path": "demo-001",
  "mode": "merge",
  "files": [
    {
      "path": "logo.png",
      "src": "data:image/png;base64,iVBOR..."
    },
    {
      "path": "banner.png",
      "src": "C:/local-assets/banner.png"
    },
    {
      "path": "icon.svg",
      "src": "https://example.com/icon.svg"
    },
    {
      "path": "index.html",
      "content": "<!DOCTYPE html>..."
    },
    {
      "path": "old.html",
      "delete": true
    }
  ]
}
```

**Validierung:** Mix von `content` + `src` im selben File-Eintrag → Error `invalid_file_entry`.

### Drei `src`-Formen im Detail

**1. Data URL (`data:<mime>;base64,<data>`)**
- Server parst, base64-dekodiert, schreibt als Bytes
- Größenlimit: 1 MB dekodierte Bytes
- Content-Type: aus File-Extension (Mimetype aus Data-URL ignoriert — analog MVP1-Decision)

**2. Lokaler Pfad (`C:/...`, `/home/...`, UNC `\\server\share\...`)**
- `File.ReadAllBytes(src)` + atomic write in `<site>/<path>`
- Größenlimit: 1 MB
- Keine Path-Validierung (Trust)

**3. HTTP/HTTPS-URL (`http://...`, `https://...`)**
- `HttpClient.GetByteArrayAsync(src)` + atomic write
- Größenlimit: 1 MB (Stream-Limit während Download)
- Konfiguration via `appsettings.json:Mvp3:HttpTimeoutSeconds` (default 30s)
- Cert-Validation: **keine** (LAN-only Trust-Modell)
- Redirects: automatisch (bis 50)
- Stream-Disk direkt (kein full-buffer bei theoretisch größeren Downloads — bei 1 MB Cap egal, aber konsistent)

### Atomic-Write (alle drei Formen)

```
tmp = <SitesRoot>/<site>/<path>.tmp
write content to tmp
File.Move(tmp → <site>/<path>)
```

Bei `mode: "merge"` und identischer Path: tmp + replace (überschreibt).
Bei `mode: "replace"`: identisch, aber alle nicht-im-Call-Files werden vorher gelöscht.

## Configuration

```json
{
  "Mvp3": {
    "HttpTimeoutSeconds": 30
  }
}
```

Default: 30s Timeout. SSL/Cert-Validation deaktiviert (Trust-Modell).

## Validierung & Errors

### File-Validierung (analog MVP1, mit `src`-Variante)

| Check | Error |
|-------|-------|
| `delete: true` + (`content` ODER `src`) | `invalid_file_entry` |
| `content` + `src` im selben Eintrag | `invalid_file_entry` |
| `!delete` + kein `content` UND kein `src` | `missing_content` |
| Doppelter `path` in einem Call | `duplicate_path` |
| Per-File-Größe > 1 MB | `file_too_large` |

### `src`-spezifische Errors

| Error | Wann |
|-------|------|
| `src_invalid_data_url` | Data-URL kaputt / base64 ungültig |
| `src_unreachable` | DNS / TCP-Fehler bei HTTP-URL |
| `src_timeout` | Request-Timeout (`HttpTimeoutSeconds` überschritten) |
| `src_fetch_failed` | HTTP 4xx / 5xx |
| `src_file_too_large` | Stream-Größe > 1 MB während Download (transient file gelöscht) |

`src_*`-Errors führen zum **Site-Deploy-Fehler** (analog MVP1 atomic-write-failures): kein partial-site-state. Atomic-Write-Semantik garantiert.

### Größenlimit (1 MB auf Platte)

- 1 MB auf der **fertig geschriebenen Platte** (= Bytes nach Decoding/Read)
- Plain-Text: String-Länge (UTF-8)
- Data-URL: dekodierte Bytes
- HTTP: Stream-Bytes
- Lokaler Pfad: File-Größe

## Storage & Content-Type

Files landen physisch im Site-Folder: `<SitesRoot>/<site>/<path>`
(wo auch MVP1 `content`-Files landen — identisches Schema).

Content-Type wird aus der **File-Extension** abgeleitet (z. B. `logo.png`
→ `image/png`, `data.txt` → `text/plain`). Mimetype aus Data-URL
wird **ignoriert**.

## Retention-Interaktion

`src`-deployed Files verhalten sich wie normale `content`-Files:
- `updated_at` wird bei Deploy aktualisiert (TTL-Reset — analog MVP1)
- `retention_seconds` greift gleich (siehe MVP2 §3)
- `delete_site` löscht das physische File (wie MVP1)

Kein Sonder-Handling — `src` ist nur eine alternative Quelle für das
physische File, danach ist alles MVP1-konform.

## Out of Scope (MVP3)

- HTTP-Authentifizierung (Bearer, Basic) für HTTP-URLs — nicht im MVP
- Caching von HTTP-Downloads — atomic-write macht Server-seitig
  überflüssig (Site-Folder ist der "Cache")
- HTTP-Proxy-Support
- HTTP-Retries bei transienten Fehlern
- Größere Files (> 1 MB) — explizites Limit

> Versionierung: v1.0 = final; Änderungen → v1.1/v2.0-Bump mit Changelog oben.
