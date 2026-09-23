# CI/CD Pipeline — Web Hoster MCP

Stand: 2026-09-23 · v1.0

## Ziel

Automatisierte Builds/Tests auf GitHub sowie tag-basierte Releases mit self-contained Windows-Artefakt.

---

## Workflows

### 1) CI Workflow

- **Datei:** `.github/workflows/ci.yml`
- **Trigger:**
  - Push auf `main`
  - Pull Request auf `main`
- **Runner:** `windows-latest`
- **Schritte:**
  1. Checkout
  2. Setup .NET 8
  3. `dotnet restore WebHosterMcp.sln`
  4. `dotnet build WebHosterMcp.sln --configuration Release --no-restore`
  5. `dotnet test WebHosterMcp.sln --configuration Release --no-build`
  6. `dotnet publish src/WebHosterMcp.Host/WebHosterMcp.Host.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false --output out/WebHosterMcp.Host-win-x64`
  7. Upload Artifact `WebHosterMcp.Host-win-x64`

### 2) Release Workflow

- **Datei:** `.github/workflows/release.yml`
- **Trigger:** Tag-Push `v*` (z. B. `v1.0.0`)
- **Runner:** `windows-latest`
- **Permissions:** `contents: write`
- **Schritte:**
  1. Checkout
  2. Setup .NET 8
  3. Restore, Build, Test (Release)
  4. Self-contained Publish (`win-x64`)
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
- `PublishTrimmed=false` ist bewusst gesetzt, um Reflection-Probleme zu vermeiden.
