# 09 — Sites Index Route

**Stand:** 2026-09-26 · v1.3 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus MVP2-Listing §Sites-Index extrahiert.
- **v1.3 (2026-09-23, MVP2-Listing):** JS-Delete-Buttons via `fetch(..., { method: 'DELETE' })` (kein `?confirm=yes`-Pattern; siehe [11 Delete-Routes](./11-delete-routes.md)).
- (vorherige Versionen) Siehe `specs/mvp2-directory-listing.md`.

## Ziel

Liefert HTML-Übersicht aller Sites unter `GET /`. Pro Site ein Type-Label (`files` / `folder` / `a2ui` / `schema-form`), eine Datei-Anzahl und ein Delete-Button (für Browser-UI).

## Route

```
GET /
```

Implementiert in `Program.cs` als `app.MapGet("/", ...)`. Liefert `text/html; charset=utf-8`.

## Verhalten

| Bedingung | Response |
|---|---|
| Sites.Count == 0 | `<p class="empty">No sites available.</p>` (kein `<ul>`) |
| Sites.Count > 0 | `<ul>` mit einem `<li>` pro Site, sortiert nach `site_path` (case-insensitive, Ordinal) |

## HTML-Struktur

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Web Hoster — Sites</title>
  <style>/* inline CSS, mobile-responsive */</style>
</head>
<body>
  <h1>Web Hoster — Sites</h1>

  <!-- Optional: -->
  <p class="empty">No sites available.</p>

  <!-- Sonst: -->
  <ul>
    <li>
      <a class="listing-name" href="/demo-001/">demo-001</a>
      <span class="meta">files · 3</span>
      <span class="meta">2026-09-22 16:50</span>
      <button type="button"
              data-delete-site="demo-001"
              class="delete-btn"
              aria-label="Delete site"
              title="Delete site"><!-- SVG-Icon --></button>
    </li>
    ... (eine <li> pro Site)
  </ul>

  <script>/* delete-handler aus 11-delete-routes.md */</script>
</body>
</html>
```

### Pro `<li>`

1. **`<a class="listing-name">`** mit `href="/<site>/"` — klickbarer Link zum Site-Listing
2. **`<span class="meta">`** mit `type · file_count` (z. B. `files · 3`)
3. **`<span class="meta">`** mit `updated_at` im Format `yyyy-MM-dd HH:mm` (lokale Server-Zeit)
4. **`<button data-delete-site>`** mit Trash-Can-SVG, `aria-label="Delete site"`, `title="Delete site"`. Klick löst JS-Handler (siehe [11 Delete-Routes](./11-delete-routes.md)) aus.

## Type-Label

Das `type`-Feld aus der Registry wird verbatim ausgegeben:

| Wert | Ausgabe |
|---|---|
| `"files"` | `files` |
| `"folder"` | `folder` |
| `"a2ui"` | `a2ui` |
| `"json-schema-form"` | `schema-form` |

## Mobile / Touch-Targets

- `<meta name="viewport" content="width=device-width, initial-scale=1">`
- Touch-Targets ≥ 44×44 px (Apple HIG)
- `:focus-visible`-Outline für Tastatur-Navigation

Detaillierte CSS-Spec: `Program.cs` `ListingCss()` — eine Funktion liefert den inline-`<style>`-Block, identisch zwischen Sites-Index und Site-Listing.

## HTML-Escape

Alle dynamischen Werte (`site_path`, `type`, `file_count`, `updated_at`) sind via `WebUtility.HtmlEncode` escaped. URLs werden via `WebUtility.UrlEncode` für `href` encoded.

## Cross-References

- [02 Sites Storage](./02-sites-storage.md) — `site_path`, `type`, `updated_at`
- [03 Static File Route](./03-static-file-route.md) — `file_count`
- [10 Site Listing Route](./10-site-listing-route.md) — Klick-Ziel
- [11 Delete-Routes](./11-delete-routes.md) — DELETE-Handler + Button-Aktion
- [05 Tool: `list_sites`](./05-tool-list-sites.md) — MCP-seitige Schwester

## Out of Scope

- Filter-UI (Type, Alter, Größe)
- Sort-Optionen
- Pagination
- Branding pro Site / Custom Theme
- HTML-Microdata / `itemtype`-Markup
- i18n (deutsch / englisch) — bleibt auf Englisch
