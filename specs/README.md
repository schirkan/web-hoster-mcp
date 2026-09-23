# Specs — Web Hoster MCP

Aktuelle Specs, phasenweise. MVP1 + MVP2 + MVP2-Listing + MVP3 + MVP4 sind gelockt.

| Spec | Inhalt | Status |
|------|--------|--------|
| [`mvp1.md`](./mvp1.md) | MVP1 — Server, Storage, 4 Tools für `type: "files"` (`content` plain-only, kein Data-URL-Auto-Decode; `src` separat) | ✅ v1.2 locked |
| [`mvp2.md`](./mvp2.md) | MVP2 — HTTPS (Cert Beides) + Retention/Auto-Delete (7d TTL default, 1h Interval) + HTTP-Delete-Endpoints mit Confirm-Pattern + Delete-Links | ✅ v1.1 locked |
| [`mvp2-directory-listing.md`](./mvp2-directory-listing.md) | MVP2 — Directory-Listing für `files`/`folder` (kein Listing bei `a2ui`/`schema-form`) | ✅ v1.2 locked |
| [`mvp3.md`](./mvp3.md) | MVP3 — Per-File `src`-Parameter (Data URL / lokaler Pfad / HTTP-URL) → physische Kopie in `<SitesRoot>/<site>/<path>`, Trust-Modell | ✅ v1.0 locked |
| [`mvp4-render-types.md`](./mvp4-render-types.md) | MVP4 — 4 Render-Types (`files`/`folder`/`a2ui`/`json-schema-form`) + React + RJSF + A2UI-React | ✅ v2.1 locked |

---

## Lock-Semantik

Alle Specs sind final gelockt nach **v1.0 = final**; Änderungen führen zu v1.1/v2.0-Bump mit Changelog-Eintrag oben im Spec.

---

Offen (Phase 2):

- MVP1 Implementierung starten (Workboard-Karten nach `specify` ziehen, dann claimen + arbeiten)
- MVP2 Implementierung (HTTPS + Retention + HTTP-Delete-Endpoints)
- MVP3 Implementierung (per-File `src` mit Data URL / local / HTTP)
- MVP4 Implementierung (Render-Types-Distribution, A2UI/RJSF/folder)
