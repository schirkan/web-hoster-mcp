# MVP2 — Directory Listing

Stand: 2026-09-24 · v1.7 (lock)

## Changelog

- **v1.7 (2026-09-24):** Heading der File-Index-Seite ist jetzt auf einer Zeile. Back-Link (`← All Sites`) und `<h1>Index of /site/</h1>` sind in einen `<header class="page-header">` Flex-Wrapper gepackt mit `flex-wrap: nowrap; align-items: center; gap: 0.5rem`. Der `border-bottom`-Divider ist auf den Wrapper gewandert; der `<h1>`-Text ellipsiert auf extrem schmalen Viewports via `text-overflow: ellipsis`. Mobile-Touch-Target bleibt `≥44 px` (Back-Link-Höhe).
- **v1.6 (2026-09-24):** Alle UI-Texte auf Englisch (`<html lang="en">`, Button-aria-labels/titles, Bestätigungs-Dialoge, Empty-States); Layout ist jetzt eine Full-Width-„Tabelle" mit rechtsbündigen Meta-/Button-Spalten — `.listing-name` hat `flex: 1 1 auto` (nimmt den verbleibenden Platz, linksbündig), `.meta` und Delete-Button rechtsbündig (`flex: 0 0 auto` + `text-align: right`).
- **v1.5 (2026-09-24):** Site-Listing hat jetzt einen „← All Sites"-Backlink zur Site-Index-Seite (Klasse `.back-link`); Delete-Buttons zeigen ein Trash-Can-SVG-Icon (Feather-Style, `aria-hidden`) statt Text, mit `aria-label` + `title` für Screen-Reader und Tooltip; Single-Row-Layout auf allen Viewports — die `@media`-Row-Stack-Variante entfällt, Items bleiben immer auf einer Zeile (sehr lange Namen werden per `text-overflow: ellipsis` gekürzt).
- **v1.4 (2026-09-24):** Mobile-Optimierung der Listings: `<meta name="viewport" content="width=device-width, initial-scale=1">`; mobile-first CSS (`<640px` Stack-Layout mit `flex-wrap: wrap`, ab `640px` Side-by-Side); Touch-Targets ≥44x44 px (Apple HIG); `:focus-visible` für Tastatur-Navigation; semantische Klassen `listing-name` / `meta` / `empty`.
- **v1.3 (2026-09-23):** Hinweis auf Delete-Buttons via JS in MVP2 §4/§5 (statt plain GET-Links); kein `?confirm=yes`-Pattern mehr.
- **v1.2 (2026-09-23):** Lock-Semantik-Footer.
- **v1.1 (2026-09-22):** Type-aware Verhalten — kein Listing bei `a2ui`/`schema-form`.
- **v1.0 (2026-09-22):** Initiale Spec.

## Ziel

Wenn der User im Browser eine Site-Root-URL aufruft
(`http://<ip>:<port>/<site_path>/`) oder die Server-Root
(`http://<ip>:<port>/`), bekommt er je nach `type` der Site
verschiedene Antworten:

- `files` / `folder` → **HTML-Verzeichnislisting**
- `a2ui` / `json-schema-form` → **kein Listing**, sondern gerenderte UI (siehe MVP4)

## Verhalten pro Type

| Request | Response bei `type: "files"` / `folder` | Response bei `type: "a2ui"` / `schema-form"` |
|---------|----------------------------------------|------------------------------------------------|
| `GET /<site_path>/` | Site-Listing (rekursiv) | Render-UI (A2UI/RJSF) |
| `GET /<site_path>/<file>` | Datei serven | **404** |
| `GET /<site_path>` (ohne `/`) | `301` → `.../<site_path>/` | `301` → `.../<site_path>/` |
| `GET /` | Sites-Index | Sites-Index (kein Site-Listing) |
| `GET /<site>/<unknown>` | `404` | `404` |

Für `a2ui`/`schema-form`: das `GET /<site>/` rendert direkt die UI. Es
gibt **keine** File-Liste im UI; der User interagiert mit der gerenderten
Komponente direkt.

## Sites-Index (`GET /`)

Funktioniert für alle Types — Liste aller Sites mit Link auf deren
Root-URL + Modified-Time + Datei-Anzahl.

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Web Hoster — Sites</title>
  <style>
    body { font-family: system-ui; max-width: 800px; margin: 2em auto; padding: 0 1em; }
    h1 { font-size: 1.1em; border-bottom: 1px solid #ccc; padding-bottom: 0.3em; }
    ul { list-style: none; padding: 0; }
    li { padding: 0.3em 0; display: flex; gap: 1em; }
    a { color: #0066cc; text-decoration: none; }
    a:hover { text-decoration: underline; }
    span { color: #666; font-size: 0.9em; }
  </style>
</head>
<body>
  <h1>Web Hoster — Sites</h1>
  <ul>
    <li>
      <a href="/demo-001/">demo-001</a>
      <span>files · 3</span>
      <span>2026-09-22 16:50</span>
    </li>
    <li>
      <a href="/docs-001/">docs-001</a>
      <span>folder · 12 Dateien</span>
      <span>2026-09-22 16:45</span>
    </li>
    <li>
      <a href="/ui-001/">ui-001</a>
      <span>a2ui</span>
      <span>2026-09-22 14:00</span>
    </li>
  </ul>
</body>
</html>
```

## Site-Listing (`GET /<site_path>/`)

Nur für `type: "files"` und `folder`. Für `a2ui`/`schema-form` siehe
MVP4-Render-Pipeline.

### Für `type: "files"` (Site-Folder im Server)

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <title>Index of /demo-001/</title>
  <style>
    /* gleicher CSS-Block wie oben */
  </style>
</head>
<body>
  <h1>Index of /demo-001/</h1>
  <ul>
    <li>
      <a href="/demo-001/index.html">index.html</a>
      <span>2026-09-22 16:50</span>
    </li>
    <li>
      <a href="/demo-001/css/style.css">css/style.css</a>
      <span>2026-09-22 16:48</span>
    </li>
  </ul>
</body>
</html>
```

Rekursiv — alle Files inkl. Subfolder werden gelistet.

### Für `type: "folder"` (Host-Folder)

Identische Struktur, aber Files kommen aus `<host-path>/` statt
`<SitesRoot>/<site>/`.

## Empty States

**Site-Listing without files:**

```html
<h1>Index of /demo-001/</h1>
<p>This site contains no files.</p>
```

**Sites-Index without sites:**

```html
<h1>Web Hoster — Sites</h1>
<p>No sites available.</p>
```

## Eigenschaften

- **HTML-Escaping** aller Pfade (XSS-Schutz, Pflicht)
- **Modified-Time** pro Eintrag im Format `YYYY-MM-DD HH:MM` (lokale Server-Zeit)
- **Alphabetische Sortierung** (Top-Level zuerst, dann Subfolders)
- **Inline CSS** (~300 Byte, system-ui Font)
- **Type-Label** in Sites-Index (`files` · `folder` · `a2ui` · `schema-form`)

## Out of Scope

- Subdirectory-Browsing (Klick auf Subfolder → neues Listing dieses Subfolders)
- Custom Titles / Branding pro Site
- File-Größen (per „API schlanker")
- Suche / Filter im Listing
- Thumbnail-Vorschau
- Hidden-Files-Logik (`.`-Präfix)
- Sort-Optionen (Datum, Größe)
- i18n (deutsch / englisch)

**Delete-Buttons (Browser-UI):** werden in MVP2 §4/§5 definiert (JS-Buttons mit `fetch` + DELETE, keine GET-Confirm-Links mehr).

> Versionierung: v1.0 = final; Änderungen → v1.1/v2.0-Bump mit Changelog oben.
