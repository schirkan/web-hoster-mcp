# 18 — HTTP Authorization (Bearer Token)

**Stand:** 2026-09-26 · v0.1 (Draft, **nicht gelockt**)

> **Status:** Entwurf. Aktuell noch nicht festgelegt. Diskussion nötig vor Lock. Noch nicht implementiert.

## Ziel

Optionale Authentifizierung für state-changing HTTP-Endpoints, sobald der Web Hoster in nicht-LAN-Netzen oder öffentlich exponiert wird. Aktuell sind die HTTP-Endpoints offen (LAN-Trust-Modell, siehe [01 HTTP Listener](./01-http-listener.md)).

## Konzept (Vorschlag)

Statischer Bearer-Token aus `appsettings.json`. HTTP-Endpoints prüfen `Authorization: Bearer <token>` Header.

```json
{
  "Auth": {
    "BearerToken": "<32-char-hex-string>",
    "Enabled": false
  }
}
```

- `Enabled: false` (Default) → **keine** Auth (wie aktuelles MVP1–MVP4)
- `Enabled: true` → `Authorization: Bearer <token>` Header erforderlich auf den state-changing-Endpoints, sonst `401 Unauthorized`

## Anwendungsbereich (Vorschlag)

State-changing HTTP-Endpoints:

| Endpoint | Tool-Schwester |
|---|---|
| `DELETE /<site>` | [07 Tool: `delete_site`](./07-tool-delete-site.md) |
| `DELETE /<site>/<file>` | – |
| `POST /<site>/submit` | – |

Read-only HTTP-Endpoints bleiben **ohne** Auth:

| Endpoint | Begründung |
|---|---|
| `GET /` | Sites-Index |
| `GET /<site>/` | Site-Listing |
| `GET /<site>/<file>` | Static File Serving |
| `GET /<site>/submit` | Form-Render |

Optional später: separate Auth-Config pro Endpoint-Typ (z. B. `Auth:RequiredOn: ["DELETE","POST"]`).

## Token-Generierung (Vorschlag)

Beim ersten Start mit `Enabled: true` und leerem `BearerToken`:

- Server generiert 32-char-hex-Token (`RandomNumberGenerator.Fill` → 16 Bytes hex)
- Loggt Token **einmalig** auf STDOUT (User muss ihn kopieren)
- Persistiert in `appsettings.json` (oder separater Token-File)

Bei manuellem Set: User setzt Token in `appsettings.json` (oder via `Host__Auth__BearerToken=...`).

## Error-Codes (neu)

| Code | Wann |
|---|---|
| `auth_required` | `Enabled: true` aber kein `Authorization`-Header |
| `auth_invalid` | Token falsch |
| `internal_error` | Token-Generierung fehlgeschlagen |

## Middleware / Handler-Pattern

ASP.NET-Core-Pattern:

```csharp
app.Use(async (ctx, next) => {
    if (authOptions.Enabled && RequiresAuth(ctx.Request))
    {
        if (!ctx.Request.Headers.TryGetValue("Authorization", out var auth) ||
            !auth.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(new ErrorEnvelope("auth_required"), statusCode: 401);
        }
        var token = auth.ToString().Substring("Bearer ".Length).Trim();
        if (!string.Equals(token, authOptions.BearerToken, StringComparison.Ordinal))
        {
            return Results.Json(new ErrorEnvelope("auth_invalid"), statusCode: 401);
        }
    }
    return await next();
});
```

`RequiresAuth` predicate schaut auf `Method` + `Path` (analog zur Tabelle oben).

## Token-Rotation

Aktuell kein Self-Service-Rotation. User muss `appsettings.json` editieren oder `Auth:BearerToken`-Env-Var setzen und Server neu starten.

## MCP-Tool-Aufrufe (nicht betroffen)

MCP-Tools (`deploy`, `list_sites`, `get_site_info`, `delete_site`, `get_submissions`) benutzen **stdio** (siehe Spec oder `Program.cs` `AddMcpServer().WithStdioServerTransport()`). Sie sind von HTTP-Auth nicht betroffen — der lokale MCP-Client gilt bereits als authentifiziert (Lokal-Prozess).

## Cross-References

- [01 HTTP Listener](./01-http-listener.md) — Listener-Hookup
- [07 Tool: `delete_site`](./07-tool-delete-site.md) — DELETE-MCP-Pfad
- [11 Delete-Routes](./11-delete-routes.md) — DELETE-HTTP-Pfad
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — POST `/submit`-HTTP-Pfad

## Offene Design-Fragen

- **Endpoint-Granularität:** nur State-changing, oder auch Read? (Pro-LAN: Read offen lassen; Pro-Public: alle schützen)
- **Token-Storage:** `appsettings.json` (Klartext, env-overridable) vs. Secret-Store (Keychain / Vault) — Production-Deployment out of scope
- **Mehrere Tokens** (z. B. Read-Token + Admin-Token) — overkill für MVP
- **HTTPS-Require für Token-Transport** — aktuell nur HTTP-Server; siehe "Removed Features" in Übersicht (HTTPS-Endpoint ist weg); `Authorization`-Header im Klartext wäre dann riskant. Vor MVP5-Implementation ggf. neu diskutieren oder nur in LAN-Vertrauensstellung aktivieren.

## Out of Scope (MVP5-Draft)

- OAuth2 / OIDC
- Per-User-Berechtigungen (Multi-Tenant)
- mTLS / Client-Cert-Auth
- Refresh-Tokens
- Rate-Limiting
- Audit-Log (welcher User hat was gemacht)
- Token-Rotation ohne Server-Restart
- CSRF-Tokens (kein Cookie-Modell aktiv)
- Authorization auf MCP-Tool-Ebene (kein Mehrbenutzer-Modell)
