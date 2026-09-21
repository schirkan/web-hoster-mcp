# Specs — Web Hoster MCP

Übersicht der Spec-Dokumente für das Projekt **Web Hoster MCP**.

| # | Dokument | Inhalt |
|---|----------|--------|
| 01 | [Vision & Scope](./01-vision.md) | Ziel, Use Cases, Nicht-Ziele |
| 02 | [Architektur](./02-architecture.md) | Tech-Stack, Komponenten, Storage, Security |
| 03 | [MCP-Tools](./03-mcp-tools.md) | Tool-Liste, JSON-Schemas, Beispiele |
| 04 | [Roadmap](./04-roadmap.md) | Phasen, MVP-Definition |

Status: **Entwurf v0.1** (2026-09-21)
Verantwortlich: Martin / Pia (Assistent)

---

## Kurzfassung (TL;DR)

Ein **MCP-Server in C# (.NET 8)** für Windows, der einer KI folgende
Werkzeuge bereitstellt, um **statische Web-Inhalte zu hosten**:

- Site anlegen / Dateien hochladen
- Site starten / stoppen
- Liste aller Sites, Site-Info abfragen
- Site aktualisieren / löschen
- Server-Logs und -Status

Daten liegen lokal unter `data/sites/<site-id>/`. HTTP-Bindung
standardmäßig nur auf `127.0.0.1`, Port je Site konfigurierbar.
