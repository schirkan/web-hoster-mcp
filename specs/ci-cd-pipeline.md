# CI/CD Pipeline — Web Hoster MCP

Stand: 2026-09-23 · v1.1

## Ziel

Tag-basierte Releases auf GitHub mit self-contained + trimmed Windows-Artefakt.

---

## Workflows

### 1) Release Workflow

- **Datei:** `.github/workflows/release.yml`
- **Trigger:** Tag-Push `v*` (z. B. `v1.0.0`)
- **Runner:** `windows-latest`
- **Permissions:** `contents: write`
- **Schritte:**
  1. Checkout
  2. Setup .NET 8
  3. Restore, Build, Test (Release)
  4. Self-contained Publish (`win-x64`, `PublishTrimmed=true`)
  5. ZIP erzeugen: `WebHosterMcp.Host-<tag>-win-x64.zip`
  6. Workflow Artifact Upload
  7. GitHub Release erstellen/aktualisieren und ZIP anhängen

---

## Release-Prozess

```bash
git tag v1.0.0
git push origin v1.0.0
```

Danach läuft `release.yml` automatisch und erzeugt das Release-Asset.

---

## Hinweise

- Self-contained Publish erhöht Artefaktgröße, macht Deployment aber unabhängig von installiertem .NET Runtime-Paket.
- Trimming ist aktiviert (`PublishTrimmed=true`) für kleinere Release-Artefakte.
- Bei Reflection-intensiven Erweiterungen ggf. Trimming-Kompatibilität testen bzw. linker-Konfiguration ergänzen.
