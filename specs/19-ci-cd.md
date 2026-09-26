# 19 — CI/CD Pipeline

**Stand:** 2026-09-26 · v1.1 (lock)

## Changelog

- **2026-09-26 (Übergang in Feature-Modell):** Aus `specs/ci-cd-pipeline.md` v1.1 extrahiert.
- **v1.1 (vorherige Versionen):** Self-contained + trimmed Publish (`win-x64`).
- **v1.0:** Initiale Spec.

## Ziel

Tag-basierte Releases auf GitHub mit self-contained, single-file, getrimmtem Windows-Artefakt (`WebHosterMcp.Host-<tag>-win-x64.zip`) als GitHub-Release-Asset.

## Workflows

### 1) Release Workflow

- **Datei:** `.github/workflows/release.yml`
- **Trigger:** Tag-Push `v*` (z. B. `v1.0.0`)
- **Runner:** `windows-latest`
- **Permissions:** `contents: write` (für Release-Erstellung + Asset-Upload)
- **Schritte:**
  1. Checkout
  2. Setup .NET 8
  3. Restore, Build, Test (Release-Konfiguration)
  4. Self-contained Publish (`win-x64`, `PublishTrimmed=true`, `PublishSingleFile=true`)
  5. ZIP erzeugen: `WebHosterMcp.Host-<tag>-win-x64.zip`
  6. GitHub Release erstellen/aktualisieren, ZIP als Asset anhängen

### 2) PR-CI (entfernt)

Stand `d47ab5e` ist **kein** dedizierter PR-CI-Workflow im Repo. Build- und Test-Verifikation läuft auf Windows-Releases via Tag-Push; PR-Check via `dotnet test` lokal. Out-of-Scope für Server-Automatisierung.

## Release-Prozess

```bash
git tag v1.0.0
git push origin v1.0.0
```

Danach läuft `release.yml` automatisch und erzeugt das Release-Asset.

## Release-Asset-Spec

```
Filename:   WebHosterMcp.Host-<tag>-win-x64.zip
Inhalt:     WebHosterMcp.Host.exe   (single-file, self-contained, trimmed)
            appsettings.json         (Repo-Default-Config)
            README.md (optional, falls vorhanden)
```

`PublishTrimmed=true` + `PublishSingleFile=true` + `RuntimeIdentifier=win-x64`. Kein separates .NET-Runtime-Install beim Endanwender nötig.

## Hinweise

- Self-contained-Publish vergrößert das Artefakt vs. framework-dependent, macht das Deployment aber unabhängig von einer installierten .NET-Runtime
- Trimming reduziert die Artefaktgröße (Reflection-basiertes JSON / etc. wurde bereits über `DefaultJsonTypeInfoResolver` / `JsonTypeInfoResolver.Combine` Trimming-kompatibel gemacht)
- Bei Reflection-intensiven Erweiterungen ggf. Trimming-Kompatibilität testen bzw. linker-Konfiguration ergänzen — derzeit nicht relevant für den Code-Bestand

## Endanwender-Installation

```powershell
$tag = (Invoke-RestMethod https://api.github.com/repos/schirkan/web-hoster-mcp/releases/latest).tag_name
Invoke-WebRequest "https://github.com/schirkan/web-hoster-mcp/releases/download/$tag/WebHosterMcp.Host-$tag-win-x64.zip" -OutFile "$env:USERPROFILE\Downloads\WebHosterMcp.zip"
Expand-Archive "$env:USERPROFILE\Downloads\WebHosterMcp.zip" -DestinationPath "$env:LOCALAPPDATA\web-hoster-mcp"
```

→ ergibt z. B. `C:\Users\<USER>\AppData\Local\web-hoster-mcp\WebHosterMcp.Host.exe`.

## Cross-References

- `.github/workflows/release.yml` (Source-of-Truth für Workflow-Definition)
- [README.md (Repo-Root)](../../README.md) → "Lokale Installation" / "CI/CD" Abschnitte
- Implementation-Status (in der [Übersicht](./README.md))

## Out of Scope

- Cross-Platform-Builds (macOS / Linux) — `win-x64`-only aktuell
- Container-Builds (Docker) — Out of Scope, würde separater Pipeline-Definition bedürfen
- NPM-Wrapper
- Canary / Beta-Channel — single `v*`-Tag-Pfad
- Auto-Tag-Bump bei Merge auf `main` (nur manuelle Tags) — bewusst manuell, damit der Operator die Version kontrolliert
- Signed Commits / Releases — Out of Scope (kein Repo-Secret konfiguriert)
