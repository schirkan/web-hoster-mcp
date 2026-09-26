# 11 — HTTP-DELETE Routes

**Stand:** 2026-09-26 · v2.0 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP2 v2.0 §3 HTTP-Delete-Endpoints + §5 Delete-Buttons in Listings extrahiert.
- **v2.0 (2026-09-25, MVP2 v2.0-Pass):** HTTPS-Removal dokumentiert (kein Server-Endpoint mehr); Inhalt der DELETE-Endpoints unverändert.
- **v1.2 (2026-09-23):** Browser-UI von GET+Confirm-Pattern auf **DELETE-Methode** mit JS-Buttons (kein `?confirm=yes`).
- **v1.0 (2026-09-23):** Initiale Spec.

## Ziel

Stellt HTTP-DELETE-Endpoints bereit, mit denen Browser-User Sites und Files destruktiv löschen können. Methoden-konform (kein GET-Confirm-Pattern), idempotent, CSRF-resistent (kein Prefetch).

## Routes

```
DELETE /<site>                 → Site löschen, 302 → /
DELETE /<site>/<file>          → File löschen, 302 → /<site>/
```

`Program.cs`:
- `app.MapDelete("/{sitePath}", ...)`
- `app.MapDelete("/{sitePath}/{*filePath:regex(.+)}", ...)`

Beide Antworten mit `302 Found` und `Location: /` bzw. `Location: /<site>/`.

## Verhalten pro Hosting-Typ

| Hosting-Typ | `DELETE /<site>` | `DELETE /<site>/<file>` |
|---|---|---|
| `files` | Hard-Delete (Site-Folder + Registry-Eintrag) | File löschen |
| `folder` | **Nur Registry-Eintrag** weg (Host-Folder unangetastet) | **404** (kein Endpoint registriert) |
| `a2ui` | Hard-Delete (`payload.json` + Registry) | **404** (kein File-Listing → kein File-DELETE) |
| `json-schema-form` | Hard-Delete (`payload.json` + Submissions + Registry) | **404** |

Implementierung in `SiteManager.DeleteAsync` / `DeleteFileAsync` (siehe [07 Tool: `delete_site`](./07-tool-delete-site.md)).

## Trust-Modell

- Path-Validation `..` / ≤ 260 Zeichen wird nicht erneut im Route-Handler geprüft (das `Path.Combine`-Resolution in `SiteManager.DeleteFileAsync` deckt es mit `StartsWith(baseFolder, OrdinalIgnoreCase)` ab)
- UNC-Pfade im `<site>/<file>`-Segment werden wie bei [03 Static File Route](./03-static-file-route.md) über `Path.GetFullPath` aufgelöst
- Authentifizierung kommt mit [18 Authorization](./18-authorization.md) (MVP5-Draft)

## Browser-UI

Plain-HTML-Links können `DELETE` nicht aufrufen (Browser unterstützen GET/POST in `<a>` / `<form>`). Die Delete-Buttons im [09 Sites Index Route](./09-sites-index-route.md) und [10 Site Listing Route](./10-site-listing-route.md) sind deshalb JS-Buttons mit `fetch`:

```html
<!-- Sites-Index: pro Site -->
<button data-delete-site="demo-001" class="delete-btn"
        aria-label="Delete site" title="Delete site"><!-- SVG --></button>

<!-- Site-Listing: pro File -->
<button data-delete-file="index.html" data-site="demo-001"
        class="delete-btn"
        aria-label="Delete file" title="Delete file"><!-- SVG --></button>
```

```js
document.querySelectorAll('[data-delete-site]').forEach(btn => {
  btn.onclick = async () => {
    const site = btn.dataset.deleteSite;
    if (!confirm(`Really delete site "${site}"?`)) return;
    const res = await fetch('/' + encodeURIComponent(site), { method: 'DELETE' });
    if (res.ok || res.redirected) location.href = '/';
    else alert('Error: ' + res.status);
  };
});

document.querySelectorAll('[data-delete-file]').forEach(btn => {
  btn.onclick = async () => {
    const site = btn.dataset.site;
    const filePath = btn.dataset.deleteFile;
    if (!site || !filePath) { alert('Context missing'); return; }
    if (!confirm(`Really delete file "${filePath}"?`)) return;
    const encoded = filePath.split('/').map(encodeURIComponent).join('/');
    const res = await fetch('/' + encodeURIComponent(site) + '/' + encoded, { method: 'DELETE' });
    if (res.ok || res.redirected) location.href = '/' + encodeURIComponent(site) + '/';
    else alert('Error: ' + res.status);
  };
});
```

Native `confirm()`-Dialog ersetzt eine Server-Confirm-Page. UX ist **1-Klick** statt 2-Klick.

### Behavior-Details

- **`res.ok || res.redirected`**: nach `DELETE` mit `302` redirected der Browser die Antwort automatisch. Das `fetch()`-Promise resolved mit `res.redirected === true`. Auf der Success-URL wird `location.href` manuell gesetzt.
- **`encodeURIComponent`** auf Site- und File-Path-Segmente, weil `site_path` Pfad-konforme Chars sind (`^[a-z0-9-]{3,32}$`), aber defensiv encoded werden

## DELETE-Prefetch-Resistenz

`DELETE` wird **nicht** von Browsern als Speculative-Prefetch ausgelöst (anders als GET). Damit gibt es keine serverseitige Confirm-Page nötig — siehe MVP2-Changelog v1.2.

## CSRF / Auth-Hinweis

Aktuell sind die DELETE-Endpoints offen (LAN-Trust-Modell). Wenn Server öffentlich exponiert wird, kommt [18 Authorization (MVP5-Draft)](./18-authorization.md) mit Bearer-Token-Schutz.

## Graceful Degradation

Bei deaktiviertem JS:

- Delete-Buttons sind nicht funktional (visuell vorhanden, aber Click tut nichts)
- Sites können weiterhin über `delete_site`-MCP-Tool (siehe [07](./07-tool-delete-site.md)) gelöscht werden
- `DELETE /<site>` direkt via `curl` / `httpie` funktioniert

## Cross-References

- [07 Tool: `delete_site`](./07-tool-delete-site.md) — MCP-seitige Schwester
- [09 Sites Index Route](./09-sites-index-route.md) — Site-Delete-Button
- [10 Site Listing Route](./10-site-listing-route.md) — File-Delete-Button
- [03 Static File Route](./03-static-file-route.md) — File-Path-Auflösung

## Out of Scope

- Confirm-Page auf Server-Seite (entfernt in v1.2; native `confirm()` ersetzt)
- Soft-Delete / Trash-Folder
- Batch-Delete (mehrere Files in einem Call)
- Undo
- CSRF-Tokens (kein Auth-Model aktiv)
- Audit-Log (welcher User hat wann gelöscht)
