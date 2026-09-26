# 13 — per-File `src`

**Stand:** 2026-09-26 · v1.1 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP3 extrahiert.
- **v1.1 (2026-09-23, MVP3):** `data:` case-insensitive; KEIN 1 MB Limit für `src`-Downloads (nur für `content` in MVP1); Path-Validation analog MVP1 (`..` nicht erlaubt, ≤ 260 Zeichen); UNC-Pfade erlaubt; HTTP-Timeout via `HttpClient.Timeout` im Constructor.
- **v1.0 (2026-09-23):** Initiale Spec.

## Ziel

Ergänzt [04 Tool: `deploy`](./04-tool-deploy.md) um eine alternative File-Quelle pro Eintrag. Statt inline `content` (MVP1) referenziert `src` eine externe Quelle — Data URL, lokaler Pfad oder `http://` / `https://`-URL. Die Datei wird physisch in `<SitesRoot>/<site>/<path>` kopiert (atomic write).

## API

`files[]`-Eintrag in [04 Tool: `deploy`](./04-tool-deploy.md):

```jsonc
{
  "path": "icon.svg",
  "src": "https://example.com/icon.svg"   // genau eine Quelle pro Eintrag
}
```

**Validation:**

- `path` analog zu MVP1 (siehe [04 Tool: `deploy`](./04-tool-deploy.md) Validation-Pass): kein `..`, ≤ 260 Zeichen
- `src`: niemals kombinieren mit `content` / `delete: true` → `invalid_file_entry`
- UNC-Pfade (`\\server\share\...`) sind erlaubt
- **`data:` case-insensitive** erkannt (`StringComparison.OrdinalIgnoreCase`)

## Erkennungs-Logik (3 Quellen)

```
if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
    → Data-URL parsen, base64 dekodieren, atomic write
else if (src.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
         src.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    → HttpClient (mit Timeout via Constructor), atomic write
else
    → File.ReadAllBytes() (lokaler Pfad inkl. UNC), atomic write
```

Alle drei Pfade führen zur gleichen Zielstruktur: `<SitesRoot>/<site>/<path>` mit atomic-write (tmp + rename).

## 1) Data URL

Format: `data:<mime>;base64,<data>`.

```csharp
var prefix = dataUrl.AsSpan(5);                  // skip "data:"
var semicolon = prefix.IndexOf(';');
var rest = semicolon > 0 ? prefix.Slice(semicolon + 1) : prefix;

if (rest.StartsWith("base64,", StringComparison.OrdinalIgnoreCase))
    return Convert.FromBase64String(rest.Slice(7).ToString());
else
    return Encoding.UTF8.GetBytes(Uri.UnescapeDataString(rest.ToString()));
```

- Mimetype aus Data-URL wird **ignoriert** (Content-Type via File-Extension)
- Größenlimit: **kein** MVP3-spezifisches Limit (gilt nur für `content` in MVP1, siehe [04 Tool: `deploy`](./04-tool-deploy.md))
- Server-HTTP-Request-Body-Limit (`MaxRequestBodySize`) gilt zusätzlich
- Fehler: `src_invalid_data_url` (base64 kaputt, Format kaputt, semicolon fehlt → Fallback auf UTF-8-Text-Decode kann fehlschlagen)

## 2) Lokaler Pfad

```
"banner.png":  { "src": "C:/local-assets/banner.png" }   // Windows absolut
"photo.jpg":   { "src": "/home/user/photo.jpg" }         // Unix absolut
"docs.md":     { "src": "\\\\server\\share\\docs.md" }   // UNC
```

```csharp
if (!File.Exists(src)) return (Array.Empty<byte>(), "src_not_found");
return (await File.ReadAllBytesAsync(src, ct), null);
```

- Path-Validation `..` / ≤ 260 wird vor `File.ReadAllBytes` geprüft
- Größenlimit: keines

## 3) HTTP/HTTPS URL

```
"remote.txt": { "src": "https://example.com/remote.txt" }
```

```csharp
var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
};
using var http = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(_srcOptions.HttpTimeoutSeconds)
};
```

- Cert-Validation: **deaktiviert** (LAN-only Trust-Modell)
- Redirects: automatisch (bis 50, `HttpClient` Default)
- Timeout: `_srcOptions.HttpTimeoutSeconds` (Default 30s, siehe Configuration)

## Atomic-Write (alle drei Quellen)

```
tmp = <SitesRoot>/<site>/<path>.tmp
write content to tmp
File.Move(tmp → <site>/<path>, overwrite: true)
```

Bei `mode: "merge"` und identischem `path`: tmp + replace.
Bei `mode: "replace"`: alle nicht-im-Call-Files werden vorher gelöscht.

## Configuration

```json
{
  "Src": {
    "HttpTimeoutSeconds": 30
  }
}
```

| Key | Default | Bedeutung |
|---|---|---|
| `Src:HttpTimeoutSeconds` | `30` | Timeout für `src`-HTTP/HTTPS-Downloads |

Env-Vars: `Src__HttpTimeoutSeconds`.

## Errors

### File-Validierung (analog MVP1)

| Check | Error |
|---|---|
| `delete: true` + (`content` ODER `src`) | `invalid_file_entry` |
| `content` + `src` im selben Eintrag | `invalid_file_entry` |
| `!delete` + kein `content` UND kein `src` | `missing_content` |
| Doppelter `path` in einem Call | `duplicate_path` |
| `path` enthält `..` | `path_traversal` |
| `path` > 260 Zeichen | `path_too_long` |

### `src`-spezifische Errors

| Error | Wann |
|---|---|
| `src_invalid_data_url` | Data-URL kaputt / base64 ungültig |
| `src_unreachable` | DNS / TCP-Fehler bei HTTP-URL |
| `src_timeout` | Request-Timeout (`HttpTimeoutSeconds` überschritten) |
| `src_fetch_failed` | HTTP 4xx / 5xx |
| `src_not_found` | Lokaler Pfad existiert nicht |

`src_*`-Errors führen zum **Site-Deploy-Fehler** (analog MVP1 atomic-write-failures): kein partial-site-state. Atomic-Write-Semantik garantiert.

## Größenlimit

- **Kein** MVP3-spezifisches Größenlimit für `src`-Downloads
- `content` (MVP1) hat 1 MB Limit (siehe [04 Tool: `deploy`](./04-tool-deploy.md))
- Server-HTTP-Limit (`MaxRequestBodySize`) gilt zusätzlich

## Trust-Modell

- **Keine** Validierung der Quelle außer `..` + ≤ 260 Zeichen
- **Keine** Cert-Validation für `https://`-Downloads
- KI trägt die Verantwortung (analog MVP4 `folder`-Type und MVP1 Trust-Modell)

## Hinweis HTTPS-Verfügbarkeit

`src`-Downloads via `https://` sind **weiterhin** unterstützt (Stand `d47ab5e`). Begründung: Der HTTPS-Endpoint-Feature wurde 2026-09-25 entfernt, weil der **Server** kein HTTPS mehr liefern soll. `src` ist ein Client-seitiger Download-Pfad (Server lädt eine externe Resource herunter) und ist orthogonal. Falls radikaler Schnitt gewünscht: nur `http://` für `src`-URLs zulassen (Anpassung in `SiteManager.ResolveSrcBytesAsync`).

## Cross-References

- [04 Tool: `deploy`](./04-tool-deploy.md) — File-Eingabe-Validation
- [02 Sites Storage](./02-sites-storage.md) — Atomic-Write-Storage

## Out of Scope

- HTTP-Authentifizierung (Bearer, Basic) für HTTP-URLs — siehe [18 Authorization (Draft)](./18-authorization.md)
- Caching von HTTP-Downloads — atomic-write macht Server-seitig überflüssig
- HTTP-Proxy-Support
- HTTP-Retries bei transienten Fehlern
- File-Source `git:` oder andere URL-Schemas (nur `data:`, `http://`, `https://`, lokaler Pfad)
- 1 MB Größenlimit für `src` — bewusst ausgenommen
