# 01 — Vision & Scope

Stand: 2026-09-21 · v0.1 (Entwurf)

---

## 1. Ziel

Ein **MCP-Server**, der einer KI (z. B. Claude, OpenClaw-Pia) Werkzeuge
gibt, um **statische Web-Inhalte lokal auf einem Windows-Host
bereitzustellen**. Die KI soll Sites eigenständig anlegen, befüllen,
aktualisieren und wieder entfernen können — ohne dass der Mensch manuell
Files kopiert oder Server startet.

## 2. Konkrete Use Cases

| ID | Use Case | Akteur | Ergebnis |
|----|----------|--------|----------|
| UC-1 | Landingpage für ein Hobby-Projekt anlegen | KI auf Nutzer-Anweisung | Site erreichbar unter `http://localhost:8123/` |
| UC-2 | HTML-Snippet ausprobieren und im Browser zeigen | KI während Code-Iteration | Live-Preview ohne manuellen Reload |
| UC-3 | Mehrere parallele Demos / Mockups hosten | KI verwaltet mehrere Sites | Jede Site eigener Port, eigene Files |
| UC-4 | Site nach Demo wieder abräumen | KI auf Aufräum-Befehl | Site gelöscht, Port frei |

## 3. Nicht-Ziele (MVP)

Diese Features sind **bewusst NICHT** im MVP enthalten:

- **Dynamische Backends** (PHP, Node, .NET-Apps) — nur statische Files
- **Custom Domains / DNS** — nur `localhost`
- **HTTPS / TLS** — Klartext im lokalen Netz
- **Authentifizierung** — Bindung auf `127.0.0.1` reicht als Schutz
- **Multi-Tenant / mehrere Nutzer** — Single-User auf eigenem Host
- **CDN / Caching-Layer** — Kestrel reicht für lokales Hosting
- **Upload > 100 MB pro Site** — Begrenzung im Manifest
- **Git-basierte Deploys** — Nur direkter File-Upload per MCP

## 4. Annahmen

- Host: Windows 10/11 oder Windows Server
- Laufzeit: .NET 8 SDK + Runtime installiert
- Nutzer hat Admin-Rechte für Installation
- KI kann MCP über stdio oder localhost-HTTP sprechen
- Speicherplatz für Sites: mind. 1 GB freier RAM/Festplatte

## 5. Erfolgskriterien

- [ ] KI kann eine Site mit 5 HTML-Dateien + 2 Bildern anlegen und
      unter einem konfigurierten Port erreichbar machen — in unter
      **30 Sekunden** Tool-Aufrufe.
- [ ] Sites überleben einen **Neustart** des MCP-Servers
      (Persistenz über Manifest).
- [ ] Konfliktfreier Betrieb von **mindestens 5 parallelen Sites**.
- [ ] Konfiguration vollständig in **einer JSON-Datei** änderbar.
