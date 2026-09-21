# 04 — Roadmap

Stand: 2026-09-21 · v0.1 (Entwurf)

---

## Phase 0 — Setup (heute)

- [x] Projektordner `projects/web-hoster-mcp/` angelegt
- [x] Specs v0.1 geschrieben
- [ ] GitHub-Repo angelegt
- [ ] `.gitignore` für .NET-Projekt committed
- [ ] Solution-Struktur committed
- [ ] Workboard mit Phasen-Karten angelegt

## Phase 1 — MVP (Ziel: lauffähige Site per KI deployen)

**Definition of Done:**
- KI kann via MCP eine Site anlegen, Dateien hochladen, Site starten
- `http://127.0.0.1:<port>/` antwortet mit `index.html`
- Persistenz über Neustart
- Mindestens 2 Tools funktionieren End-to-End: `deploy_site`, `list_sites`

**Schritte:**

1. Solution `WebHosterMcp.sln` mit Projekten:
   - `WebHosterMcp.Host` (Konsolen-EXE)
   - `WebHosterMcp.Core` (SiteManager, Registry, Validation)
   - `WebHosterMcp.Tests` (xUnit)
2. `WebHosterMcp.Host` mit McpServer-Setup + `appsettings.json`
3. `SiteRegistry` mit `registry.json`-Persistenz
4. `SiteManager.Create` + Schreiben von Files
5. Kestrel-Start/Stop je Site
6. Tool `deploy_site` (Core-Flow ohne alle Edge-Cases)
7. Tool `list_sites`, `get_site`
8. Tool `start_site`, `stop_site`, `delete_site`
9. `update_site_files` (merge-Modus)
10. `get_server_status`, `get_site_logs`
11. README + Install-Anleitung
12. Erster End-to-End-Test: KI erstellt eine Demo-Site

Geschätzter Aufwand: **3–5 Sessions** mit Sub-Agents.

## Phase 2 — Härtung

- [ ] `replace`- und `append`-Modus für `update_site_files`
- [ ] `set_site_headers` Tool
- [ ] `export_site` (ZIP)
- [ ] Bessere Fehlermeldungen + Code-Coverage ≥ 70 %
- [ ] Windows-Service-Variante
- [ ] Konfigurierbare Bind-Adresse mit Sicherheits-Hinweis
- [ ] `--doctor`-Flag: prüft Ports, Pfade, Permissions

## Phase 3 — Komfort

- [ ] `import_site` aus ZIP
- [ ] Hot-Reload via `FileSystemWatcher`
- [ ] Optionale Auth am MCP (Token / Pairing)
- [ ] Metriken: Anzahl Requests pro Site, Top-URLs
- [ ] Docker-Container für Linux-Mitschnitt
- [ ] CI/CD-Pipeline (GitHub Actions): Build + Tests + Release

## Phase 4 — Public (optional)

Nur wenn überhaupt gewünscht — diese Phase ist als **extern-publizierbar**
gedacht und benötigt separate Entscheidung.

- [ ] Repo public stellen
- [ ] Lizenz wählen (MIT? Apache-2.0?)
- [ ] NuGet-Paket für Embedding in andere .NET-Projekte
- [ ] Dokumentation auf eigener Seite

---

## Technische Risiken (früh prüfen)

| Risiko | Mitigation |
|--------|------------|
| MCP-C#-SDK noch in Bewegung (Stand 2026-Q3) | Version pinnen, in Phase 1 explizit testen |
| Port-Konflikte mit anderer Software | `PortRange` eng begrenzen (8100–8199) |
| File-Watcher auf Windows unzuverlässig | Phase 2, **nicht** im MVP |
| Path-Traversal trotz Sandbox | Defense-in-Depth: zusätzlich `Path.GetFullPath`-Check |
| Große Sites blockieren MCP-Event-Loop | Sync-Schreiben, async-Read; harte Größenlimits |
| Windows-Firewall poppt beim ersten LAN-Bind | Default ist `127.0.0.1` — kein Popup |

---

## Out-of-Scope (für immer)

Diese Features werden bewusst nicht gebaut:

- PHP / Node / .NET-Backends auf den Sites
- Datenbank-Anbindung
- User-Accounts / Multi-Tenant
- Custom Domains / DNS
- HTTPS / Let's-Encrypt
- Load-Balancing / Clustering

Wenn eines davon gebraucht wird: separates Tool, nicht hier reinpfriemeln.
