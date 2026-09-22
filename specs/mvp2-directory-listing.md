# MVP2 — Directory Listing

Stand: 2026-09-22 · v1.0 (lock)

## Ziel

Wenn der User im Browser eine Site-Root-URL aufruft
(`http://<ip>:<port>/<site_path>/`) oder die Server-Root
(`http://<ip>:<port>/`), bekommt er ein automatisch generiertes
HTML-Verzeichnislisting mit klickbaren Links. **Das Listing wird
immer angezeigt — auch wenn eine `index.html` existiert.** Um
`index.html` direkt zu sehen, muss sie explizit über den Link im
Listing aufgerufen werden.

## Verhalten

| Request | Response |
|---------|----------|
| `GET /<site_path>/` | Site-Listing (immer, auch wenn `index.html` existiert) |
| `GET /<site_path>/<file>` | Datei serven |
| `GET /<site_path>` (ohne `/`) | `301` → `…/<site_path>/` |
| `GET /<site_path>/<unbekannt>` | `404` |
| `GET /<site_path>/sub/` | `404` (keine Subdirs im MVP1) |
| `GET /` | Sites-Index (Liste aller Sites) |

## Sites-Index (`GET /`)

```html
<!DOCTYPE html>
<html lang="de">
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
      <span>3 Dateien</span>
      <span>2026-09-22 16:50</span>
    </li>
    <li>
      <a href="/a8f2k1d3/">a8f2k1d3</a>
      <span>1 Datei</span>
      <span>2026-09-22 16:45</span>
    </li>
  </ul>
</body>
</html>
```

## Site-Listing (`GET /<site_path>/`)

```html
<!DOCTYPE html>
<html lang="de">
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
      <a href="/demo-001/style.css">style.css</a>
      <span>2026-09-22 16:48</span>
    </li>
  </ul>
</body>
</html>
```

**Empty State** (Site ohne Files):

```html
<h1>Index of /demo-001/</h1>
<p>Diese Site enthält keine Dateien.</p>
```

**Empty State** (keine Sites):

```html
<h1>Web Hoster — Sites</h1>
<p>Keine Sites vorhanden.</p>
```

## Eigenschaften

- **IMMER Listing** bei Root-Aufruf einer Site — auch wenn `index.html`
  existiert
- **Modified-Time** pro Eintrag im Format `YYYY-MM-DD HH:MM` (lokale
  Server-Zeit, keine TZ-Info)
- **Alphabetische Sortierung**
- **HTML-Escaping** aller Pfade (XSS-Schutz, Pflicht)
- **Inline CSS** (~300 Byte, system-ui Font, kein externes Asset)
- **Flat** — keine Subdirectories
- **`index.html` als normaler Eintrag** im Listing (kein Sonder-Status,
  kein Default-Redirect)

## Out of Scope

- Subdirectories / verschachtelte Strukturen
- Custom Titles / Branding pro Site
- File-Größen (per „API schlanker")
- Suche / Filter im Listing
- Thumbnail-Vorschau
- Hidden-Files-Logik (`.`-Präfix)
- Sort-Optionen (Datum, Größe)
- i18n (deutsch / englisch)
