# 01 — HTTP Listener

**Stand:** 2026-09-26 · v1.3 (lock)

## Changelog

- **2026-09-25 (Übergang in Feature-Modell):** HTTPS-Endpoint (paralleler HTTPS-Listener mit PFX / Self-Signed Fallback) gestrichen — Web Hoster liefert ausschließlich HTTP. Entfernt: `Host:UseHttps`, `Host:HttpsPort`, `Https`-Section in `appsettings.json`, HTTPS-Kestrel-Setup in `Program.cs`, `HttpsCertificateLoader.cs`, `certs/`-Folder. Konfigurations-Source: `src/WebHosterMcp.Core/HostOptions.cs`.
- **v1.3 (2026-09-23, MVP1):** Default-Bind `0.0.0.0:3000`. LAN-IP-Auflösung über `LanIpDetector` für `SiteManager.BuildSiteUrl` (damit URLs LAN-reachable sind, statt nur auf `127.0.0.1` zu zeigen).
- (vorherige Versionen) Siehe Git-History von `specs/mvp1.md`.

## Ziel

Stellt den einzigen HTTP-Listener (Kestrel) bereit, an den ASP.NET-Core-Routen in `Program.cs` gebunden werden. Liefert ausschließlich HTTP — kein HTTPS.

## Bind-Spec

| Property | Quelle | Default | Bedeutung |
|---|---|---|---|
| `Host:Ip`   | appsettings.json (`Host` section) | `0.0.0.0` | HTTP-Bind-Adresse |
| `Host:Port` | appsettings.json (`Host` section) | `3000`     | HTTP-Port |

`Host:HttpsPort` und `Host:UseHttps` sind **weggefallen** (siehe Changelog oben). Kein HTTPS-Listener, kein HTTPS-Setup.

## URL-Pattern

```
http://<Host:Ip>:<Host:Port>/<site_path>/<file>
                          erstes Segment = Site-Identität
```

Beispiel mit Defaults: `http://0.0.0.0:3000/demo-001/index.html`.

## Bind-Verhalten

```
Host:Ip       Bind-IP                  Verhalten
─────────────────────────────────────────────────────
"0.0.0.0"     INADDR_ANY               Alle Interfaces (Production-Default)
"::"          IN6ADDR_ANY              IPv6 Any (selten in der Praxis)
"127.0.0.1"   Loopback                 Nur lokal
"<lan-ip>"    spezifische IPv4         spezifisches Interface
(empty)       Fallback auf 0.0.0.0
```

`Program.cs` parst die IP via `IPAddress.TryParse(hostConfig.Ip, out var parsed) ? parsed : IPAddress.Any`. Bei Parse-Fehler wird auf `Any` (0.0.0.0) zurückgefallen.

## LAN-IP-Auflösung

Wenn `Host:Ip` ein Bind-Any-Sentinel ist (`0.0.0.0` / `::` / leer), wird die LAN-IP automatisch ermittelt und in `SiteManager.BuildSiteUrl` als Host in den result-URLs eingesetzt, damit andere Geräte im LAN die Site per `http://<lan-ip>:3000/...` erreichen können.

```
1) Host:Ip ist "0.0.0.0" / "::" / leer
   → LanIpDetector.GetLanIpv4() gibt erste nicht-loopback,
     nicht-link-local IPv4-Adresse eines aktiven Interfaces zurück
   → BuildSiteUrl verwendet diese LAN-IP als Host

2) Sonst
   → Host:Ip verbatim (z. B. "127.0.0.1" in Tests,
     oder eine spezifische IP, die ein Operator setzt)
```

`LanIpDetector` wirft keine Exceptions (try/catch → `null`), Sandbox- oder Permission-Probleme führen zu Fallback auf `Host:Ip`.

## HTTP-only — kein HTTPS

Seit 2026-09-25 ist der HTTPS-Endpoint entfernt (siehe Übersicht → *Removed Features*). Konkret:

- **Kein** paralleler HTTPS-Listener
- **Kein** HTTP → HTTPS Redirect (entfällt)
- **Kein** Cert-Loading
- **Kein** Self-Signed-Cert-Fallback
- **Kein** PFX-Support
- **Kein** `Https`-Section in `appsettings.json`
- **Kein** `HttpsCertificateLoader.cs`
- **Kein** `certs/`-Folder
- **Kein** `HttpsOptions` / `HttpsSelfSignedOptions` in `HostOptions`

`src`-Downloads in [13 per-File `src`](./13-per-file-src.md) (HTTP-Client auf Server-Seite für `src`-URLs) bleiben eine client-seitige Download-Quelle und sind orthogonal zum Server-Endpoint. Wenn dort eine `https://`-URL als `src`-Wert steht, wird sie vom Server per HTTP-Client (mit LAN-only-Trust-Modell, ohne Cert-Validation) abgerufen und unter dem Site-Pfad abgelegt.

## Configuration (`appsettings.json`)

```json
{
  "Host": {
    "Ip": "0.0.0.0",
    "Port": 3000
  }
}
```

Layer-Priorität (höchste zuerst):

1. Kommandozeile (z. B. `--Host:Port=4000`)
2. Umgebungsvariablen (Doppel-Underscore: `Host__Ip`, `Host__Port`)
3. `appsettings.{ASPNETCORE_ENVIRONMENT}.json`
4. `appsettings.json`

## Cross-References

- Bindet Routen aus [03 Static File Route](./03-static-file-route.md), [09 Sites Index Route](./09-sites-index-route.md), [10 Site Listing Route](./10-site-listing-route.md), [11 Delete-Routes](./11-delete-routes.md)
- `Host:Ip` / `Host:Port` werden in `SiteManager.BuildSiteUrl` zu Site-URLs (von [02 Sites Storage](./02-sites-storage.md) referenziert)
- Logged beim Startup: `HTTP: http://{Ip}:{Port}/` + (falls LAN-IP abweicht) `HTTP (LAN): http://{LanIp}:{Port}/`

## Out of Scope

- HTTPS (entfernt; siehe Changelog)
- HTTP/HTTP2-Konfiguration (Kestrel-Defaults)
- TLS, mTLS, Client-Cert-Auth
- Reverse-Proxy-Modus / Forwarded-Headers-Verarbeitung
- Multi-Listener (mehrere IPs / Ports gleichzeitig) — für die Use-Cases nicht nötig
