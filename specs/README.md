# Specs — Web Hoster MCP

Aktuelle Specs, phasenweise. MVP1 + MVP2 + MVP2-Listing + MVP3 + MVP4 sind gelockt.

| Spec | Inhalt | Status |
|------|--------|--------|
| [`mvp1.md`](./mvp1.md) | MVP1 — Server, Storage, 4 Tools für `type: "files"` (`content` plain-only, Path-Validation inkl. MAX_PATH, `replace`+`files:[]` löscht) | ✅ v1.3 locked |
| [`mvp2.md`](./mvp2.md) | MVP2 — HTTPS (Cert Beides) + Retention/Auto-Delete + HTTP-Delete-Endpoints via **DELETE** (kein Confirm-Pattern) | ✅ v1.3 locked |
| [`mvp2-directory-listing.md`](./mvp2-directory-listing.md) | MVP2 — Directory-Listing für `files`/`folder` inkl. Delete-Buttons-Referenz | ✅ v1.3 locked |
| [`mvp3.md`](./mvp3.md) | MVP3 — Per-File `src` (Data URL / lokaler Pfad / HTTP-URL), atomic write, UNC erlaubt | ✅ v1.1 locked |
| [`mvp4-render-types.md`](./mvp4-render-types.md) | MVP4 — 4 Hosting-Typen (`type`) + A2UI (`@a2ui/react`) + RJSF + Payload-Limits | ✅ v2.2 locked |
| [`mvp5-authorization.md`](./mvp5-authorization.md) | MVP5 — Authorization Draft (Bearer-Token für HTTP-Endpoints) | 📝 Draft |
| [`ci-cd-pipeline.md`](./ci-cd-pipeline.md) | CI + Release-Workflows (self-contained publish + tag-based releases) | ✅ v1.0 |

---

## Lock-Semantik

Alle Specs sind final gelockt nach **v1.0 = final**; Änderungen führen zu v1.1/v2.0-Bump mit Changelog-Eintrag oben im Spec.

---

Offen (Phase 2):

- MVP2 Implementierung (HTTPS + Retention + HTTP-Delete-Endpoints)
- MVP3 Implementierung (per-File `src` mit Data URL / local / HTTP)
- MVP4 Implementierung (Hosting-Typen-Distribution, A2UI/RJSF/folder)
