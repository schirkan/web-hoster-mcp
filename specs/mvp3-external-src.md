# MVP3 — External Files via `src`-Pfad

Stand: 2026-09-22 · Entwurf v0.1

> **Status:** Idee + Pro/Contra. Implementierung bewusst aufgeschoben.

## Ziel

MCP-Tools (`deploy_site`, `update_files`) sollen Dateien aus dem lokalen
Filesystem referenzieren können, **ohne den Content im Tool-Call zu
senden**. Nützlich für Binaries (PNG, Fonts, PDFs) und große Files, bei
denen base64-Encoding im Tool-Payload ineffizient ist.

## API-Erweiterung

In `files[]` zusätzlich zu `content` ein optionales Feld `src`:

```json
{
  "path": "logo.png",
  "src": "C:/local/logo.png"
}
```

`content` und `src` schließen sich gegenseitig aus — entweder/oder,
nicht beides.

**Verhalten zur Laufzeit:**

- `src` ist **absoluter Pfad** (relativ wäre mehrdeutig)
- MCP-Server liest die Datei zur Deploy-Zeit (kein Pre-Caching)
- Größe unterliegt dem Per-File-Limit aus MVP1 (z. B. 512 KB)
- Atomic-Write-Semantik wie bei `content` — Site ist konsistent oder
  gar nicht deployt

## Pro

- **Binaries ohne base64** — PNGs, Fonts, PDFs ohne 33%-Overhead
- **Kleinere Payloads** — 1-MB-Logo wird ~30 Zeichen Pfad-String
- **KI-Local-Files direkt verlinken** — wenn die KI ein Bild o. ä.
  lokal generiert hat, kann sie direkt darauf zeigen, ohne Upload-Logik
- **Source-of-truth auf Disk** — die Datei auf der Platte IST die Version,
  kein Drift zwischen KI-Vorstellung und MCP-Inhalt

## Contra

- **Security-Surface** — MCP-Server muss Pfade validieren: kein Zugriff
  auf System-Ordner (`C:\Windows`, `C:\Program Files`), kein
  Path-Traversal, idealerweise Whitelist erlaubter Roots
- **Weitreichende Read-Rechte** — die KI kann potenziell jeden Pfad
  spezifizieren, auf den der MCP-Server-User Lese-Rechte hat; deutlich
  weiter als „eigener Projektordner"
- **Cross-Plattform-Pfade** — `C:\` (Windows), `/` (Linux),
  `\\server\share` (UNC) — Validation muss alle drei handeln oder
  explizit einschränken
- **Versionsdrift** — File auf Disk ändert sich zwischen KI-Look und
  MCP-Read; was ankommt, ist nicht deterministisch identisch zur
  KI-Sicht
- **Atomarität gebrochen** — Path-Read + Write sind nicht atomar; wenn
  die Datei zwischen Read und Write verschoben/gelöscht wird, failt das
  Deploy mit `file_not_found`
- **MCP-Host-Abhängigkeit** — funktioniert nur, wenn die KI auf
  demselben Host läuft oder einen Trusted-Mount-Pfad sieht; nicht für
  Cloud-KIs ohne Filesystem

## Empfehlung

**MVP1 / MVP2:** nur `content` (plain string), kein `src`. Begründung:
Der primäre Use-Case ist HTML/CSS/JS, das braucht kein `src`. Die
gesamte Security- und Path-Validation-Komplexität entfällt, MVP bleibt
schlank.

`src` als **optionales zweites Field** neben `content` nachrüsten —
kein API-Bruch, sondern Erweiterung. Eingeführt in MVP3, sobald der
erste reale Binary-Use-Case auftaucht.

## Offene Punkte (für die MVP3-Umsetzung)

1. **Pfad-Whitelist** — spezifische Roots in `appsettings.json`
   (z. B. `["C:/projects/assets"]`) oder ganzes Filesystem mit
   Blocklist? **Empfehlung:** Whitelist.
2. **Symlinks** — folgen oder grundsätzlich ablehnen? **Empfehlung:**
   folgen, aber mit Whitelist-Schutz.
3. **UNC-Pfade** — erlaubt? **Empfehlung:** nein, nur lokale Paths.
4. **File-Watcher** — Site automatisch neu deployen, wenn das
   Source-File sich ändert? **Empfehlung:** nein, manuelle
   `update_files`-Aufrufe reichen; Auto-Watch erzeugt versteckte
   Side-Effects.
5. **Read-Rechte-Audit** — wie verhindern wir, dass die KI versehentlich
   sensible System-Files liest? **Empfehlung:** Whitelist +
   Pfad-Blocklist für `C:\Windows`, `C:\Program Files`,
   `~/.ssh/`, `~/.aws/` etc.
