# MVP5 — Authorization (Draft)

Stand: 2026-09-23 · v0.1 (draft, nicht gelockt)

> **Status:** Entwurf. Aktuell noch nicht festgelegt. Diskussion nötig vor Lock.

## Ziel

MVP5 fügt optionale Authentifizierung für HTTP-Endpoints hinzu (speziell DELETE und POST).
Aktuell sind HTTP-Endpoints ohne Auth (LAN-only MVP). Für Einsatz in unsicheren
Netzen oder wenn der Server exponiert wird, ist Auth nötig.

## Konzept (Vorschlag)

Statischer Bearer-Token aus `appsettings.json`. HTTP-Endpoints prüfen
`Authorization: Bearer <token>` Header.

```json
{
  "Auth": {
    "BearerToken": "<32-char-hex-string>",
    "Enabled": false
  }
}
```

- `Enabled: false` (default) → keine Auth (wie MVP1–MVP4)
- `Enabled: true` → `Authorization: Bearer <token>` Header erforderlich, sonst 401

## Anwendungsbereich (Vorschlag)

State-Changing HTTP-Endpoints:
- `DELETE /<site>` (MVP2)
- `DELETE /<site>/<file>` (MVP2)
- `POST /<site>/submit` (MVP4)

Read-only HTTP-Endpoints bleiben ohne Auth:
- `GET /<site>/<file>` (File Serving)
- `GET /<site>/` (Directory-Listing)
- `GET /` (Sites-Index)

Optional später: separate Auth-Config pro Endpoint-Typ.

## Token-Generierung (Vorschlag)

Beim ersten Start mit `Enabled: true` und leerem `BearerToken`:
- Server generiert 32-char-hex-Token
- Loggt Token **einmalig** auf STDOUT (User muss es kopieren)
- Persistiert in `appsettings.json` (oder separater Token-File)

Bei manuellem Set: User setzt Token in `appsettings.json`.

## Error-Codes (neu)

| Code | Wann |
|------|------|
| `auth_required` | `Enabled: true` aber kein `Authorization`-Header |
| `auth_invalid` | Token falsch |
| `internal_error` | Token-Generierung fehlgeschlagen |

## MCP-Tool-Aufrufe (NICHT betroffen)

MCP-Tools (`deploy`, `list_sites`, `get_site_info`, `delete_site`, `get_submissions`)
benutzen **stdio** oder Named-Pipes (lokal), nicht HTTP. Sie sind von HTTP-Auth nicht
betroffen — der lokale MCP-Client gilt bereits als authentifiziert.

## Out of Scope (MVP5)

- OAuth2 / OIDC
- Per-User-Berechtigungen (Multi-Tenant)
- mTLS / Client-Cert-Auth
- Refresh-Tokens
- Rate-Limiting
- Audit-Log
- Token-Rotation

> Versionierung: v1.0 = final; Änderungen → v1.1/v2.0-Bump mit Changelog oben.
