# 20 — Submission Notifications (Callback-Pfad)

**Stand:** 2026-09-27 · v0.1 (Draft, **nicht gelockt**)

> **Status:** Entwurf. Diskussion nötig vor Lock. Noch nicht implementiert. Companion zu [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) und [08 Tool: `get_submissions`](./08-tool-get-submissions.md).

## Ziel

Wenn ein Form-Submit auf `POST /<site>/submit` landet, soll die **KI-Agent-Session, die das Form deployt hat**, proaktiv benachrichtigt werden — statt pollen zu müssen via `get_submissions(since=…)`. Optional: Single-Shot-Antwort der KI über einen eindeutigen Callback-Token.

## Motivation

Aktuell gibt es nur Polling:

```
[Browser]  POST /<site>/submit     → [Host speichert Submission-File]
[Agent]    get_submissions(site)   ← liest Liste, sieht neue Einträge
```

Probleme:

1. **Latenz** — Polling-Intervall vs. Echtzeit. Bei 30 s Polling verpasst der Agent schnelle Folgesubmits.
2. **Energie** — MCP-Tool-Calls verbrauchen Tokens und Modell-Aufmerksamkeit, auch wenn nichts Neues da ist.
3. **UX** — Form-Submitter wartet auf KI-Antwort, aber die KI weiß nicht, dass sie antworten soll.

Spec-Lücke: [`specs/17-hosting-type-schema-form.md`](./17-hosting-type-schema-form.md) listet **„Submit-Webhooks (Server postet Submission-Events zu externem Endpoint)"** aktuell explizit als Out-of-Scope. Diese Spec füllt die Lücke.

## Anforderungen

| # | Anforderung | Quelle |
|---|---|---|
| R1 | Submission-Event erreicht **die richtige KI-Session** (nicht eine beliebige) | Martin |
| R2 | **Single-Shot-Antwort** möglich, eindeutig identifizierbar via Token | Martin |
| R3 | Token ist **nicht replay-bar** (verbraucht sich nach erster Antwort) | Martin (implizit: "unique callback token") |
| R4 | Funktioniert im LAN-Setup wie aktuell (kein Public-HTTPS-Pflicht) | Spec 01 |
| R5 | Polling (`get_submissions`) bleibt **erste Quelle** der Wahrheit — Notifications sind Optimierung | Spec 08 |
| R6 | Authentizität des Callbacks ist überprüfbar (HMAC) | Spec 18-Spirit |
| R7 | Notification-Pfad ist **non-blocking** für `POST /<site>/submit` (HTTP-Antwort kommt in < 100 ms) | Spec 17 (Submit-Verhalten) |

## Drei Optionen

Alle drei erfüllen R1 (mit unterschiedlichem Aufwand), R4–R7. R2 + R3 sind **nur mit zusätzlichem Reply-Endpoint** erfüllbar — siehe jeweils am Ende.

---

### Option A — MCP server→client `notification` (in-process)

**Architektur:** Host sendet eine JSON-RPC-Notification über den bestehenden MCP-Stdio-Transport an den OpenClaw-Gateway. OpenClaw müsste diese in die laufende Session injizieren.

**Protokoll:**

```jsonc
// Frame auf stdin des MCP-Servers (= stdout des Hosts)
{
  "jsonrpc": "2.0",
  "method": "notifications/web-hoster-mcp/submission",
  "params": {
    "site_path": "form-test-001",
    "submission_id": "2026-09-27T11-30-00_a8f2k1d3",
    "received_at": "2026-09-27T11:30:00",
    "callback_token": "a1b2c3...",     // R2, R3 — siehe unten
    "callback_url": "https://192.168.178.104:3000/form-test-001/reply",
    "data_preview": { /* Top-Level Felder, max 1 KB */ }
  }
}
```

**Method-Naming:** MCP-Spec (2025-06-18) kennt nur `notifications/cancelled` und `notifications/progress` als server→client. Eigene Methoden müssen namespaced sein (`<vendor>/<name>`). Hier: `web-hoster-mcp/submission`.

**API in C# (MCP SDK 0.4.0-preview.1):**

```csharp
// IMcpServer.SendNotificationAsync(method, payload)
await mcpServer.SendNotificationAsync(
    "notifications/web-hoster-mcp/submission",
    payloadObject);
```

**Session-Routing (R1):** **Heute nicht möglich.** Recherche-Ergebnis: Der OpenClaw-Plugin-API fehlt komplett der Mechanismus, MCP-Notifications vom Server zu empfangen. Siehe [openclaw/openclaw#21251](https://github.com/openclaw/openclaw/issues/21251) („feat: plugin API support for MCP resource subscriptions") — der Issue ist offen und beschreibt exakt diesen Use-Case. Der dokumentierte Workaround ist Polling via `api.registerService`. Die Notification würde zwar im MCP-Stdio-Stream beim Gateway ankommen, wird aber nur geloggt, nicht an eine Plugin-Callback weitergeleitet. **Ohne externes Hook-Plugin (siehe Option B) gibt es heute keinen Pfad in eine Session.**

**Retry:** keine (fire-and-forget). Wenn der Agent nicht live ist, geht die Notification verloren — Polling bleibt Fallback.

**Single-Shot-Antwort (R2, R3):** Nicht möglich ohne zusätzlichen HTTP-Reply-Endpoint. A allein liefert nur „KI weiß, dass etwas passiert ist" — sie kann nicht direkt antworten.

| | |
|---|---|
| **Aufwand** | 10–20 LoC im Submit-Handler |
| **Pros** | Null Infrastruktur, MCP-nativ, in-process |
| **Cons** | OpenClaw-Plugin-API empfängt heute KEINE generischen MCP-Notifications ([#21251](https://github.com/openclaw/openclaw/issues/21251) offen); Notification kommt im Gateway-Log an, aber keine Bridge zur Session; kein Single-Shot-Reply |
| **Lock-Empfehlung** | **v0.2** wenn OpenClaw #21251 implementiert ist; heute **nicht funktional** |

---

### Option B — OpenClaw `/hooks`-HTTP-Endpoint + Custom Hook-Plugin

**Architektur:** Host POSTet nach `POST http://<gateway>:<bind-port>/hooks/<plugin-name>` mit Bearer-Token aus `openclaw.json → hooks.token`. Ein dediziertes Hook-Plugin im OpenClaw-Format (`~/.openclaw/hooks/<name>/` mit `package.json`, `handler.js`, `HOOK.md`) nimmt den POST entgegen und ruft intern OpenClaws Automation-API auf.

**HTTP-Format:**

```http
POST /hooks/web-hoster-submission HTTP/1.1
Host: 192.168.178.104:18789
Authorization: Bearer f9a02a76160b70d6a9bc73d984862de6
Content-Type: application/json
X-WebHoster-Signature: sha256=<hex>            # HMAC-SHA256(secret, body)
X-WebHoster-Timestamp: 1758969000              # unix seconds
X-WebHoster-Submission-Id: 2026-09-27T11-30-00_a8f2k1d3

{
  "schema_version": "1.0",
  "site_path": "form-test-001",
  "submission_id": "2026-09-27T11-30-00_a8f2k1d3",
  "received_at": "2026-09-27T11:30:00",
  "callback_token": "a1b2c3...",
  "callback_url": "https://192.168.178.104:3000/form-test-001/reply",
  "data": { /* full submission body */ }
}
```

**Hook-Plugin-Pflichtbestandteile:**

```
~/.openclaw/hooks/web-hoster-submission/
├── package.json    # openclaw.hooks config, declares events
├── handler.js      # default export: async (event, ctx) => void
└── HOOK.md         # Plugin-Doku (analog headroom-openclaw)
```

`handler.js`-Pseudocode:

```js
export default async function handle(event, ctx) {
  // event.body ist das obige JSON
  const { site_path, submission_id, callback_token, callback_url, data } = event.body;

  // OpenClaw-Automation enqueuen, die einen Agent-Turn auslöst
  await ctx.automations.add({
    name: `submission-${submission_id}`,
    schedule: { kind: 'at', at: new Date().toISOString() },  // one-shot, sofort
    sessionTarget: event.body.session_key || 'main',
    payload: {
      kind: 'agentTurn',
      message: buildPrompt(site_path, submission_id, callback_token, callback_url, data)
    },
    delivery: {
      mode: 'announce',
      channel: 'telegram',
      to: event.body.reply_target  // z. B. topic_id
    }
  });
}
```

**Session-Routing (R1):** Explizit via `sessionTarget` im Automation-Payload. Werte: `"current"`, `"isolated"`, `"main"`, `"session:<key>"`. `session_key` muss vom Host beim `deploy` mitgeschickt werden — Strategie siehe unten.

**Auth (R6):**

- **Bearer** = `hooks.token` aus `openclaw.json` (shared secret, alle Hook-Plugins)
- **Zusätzlich HMAC** `X-WebHoster-Signature` (per-Plugin-Secret) verhindert, dass ein anderes Hook-Plugin eine Notification fälschen kann

**Retry:** Im Hook-Plugin implementierbar. Standard-Vorschlag: 4 Versuche (1 s, 5 s, 25 s, 125 s, ±20 % Jitter), dann Persist in `<SitesRoot>/.notifications/<id>.failed.json` für manuellen Re-Trigger.

**Single-Shot-Antwort (R2, R3):** Der Host braucht zusätzlich einen `POST /<site>/<submission_id>/reply`-Endpoint, der den Token validiert (siehe Spec-Abschnitt „Callback-Token"). Hook-Plugin muss Reply-Information in den Agent-Turn-Prompt einbacken.

| | |
|---|---|
| **Aufwand** | ~100–150 LoC Host (HMAC-Sign + Retry) + 60–80 LoC Hook-Plugin (Node.js) + 30 LoC Reply-Endpoint |
| **Pros** | OpenClaw-nativ, explizites Session-Routing, voller Automation-Feature-Set (Delivery, Retry, History) |
| **Cons** | Zweites Repo/Artefakt (Hook-Plugin), zweite Update-Pipeline, neues Failure-Mode (Gateway-Restart → verpasste Notifications, falls nicht persisted) |
| **Lock-Empfehlung** | sauber, aber relativ schwer für „nur eine Notification" |

---

### Option D — MCP `resources/subscribe` + `notifications/resources/updated` (spec-konform)

**Architektur:** Der Host nutzt das MCP-Resources-Subsystem (statt Tools/Notifications) als standardisierten Push-Mechanismus.

**Setup:**

```jsonc
// Server-Capabilities (im initialize-Response)
{
  "capabilities": {
    "resources": {
      "subscribe": true,    // Client darf subscriptions registrieren
      "listChanged": true   // Server pusht bei Resource-Liste-Änderung
    }
  }
}

// Statische Resource-Liste pro Site
resources/list → {
  "resources": [
    {
      "uri": "web-hoster-mcp://<site>/submissions",
      "name": "Submissions",
      "mimeType": "application/json"
    }
  ]
}

// Client abonniert (einmal pro interessierter Site)
resources/subscribe { uri: "web-hoster-mcp://<site>/submissions" }

// Server pusht bei neuer Submission
notifications/resources/updated {
  "uri": "web-hoster-mcp://<site>/submissions",
  "_meta": {
    "submission_id": "2026-09-27T11-30-00_a8f2k1d3",
    "received_at": "2026-09-27T11:30:00",
    "callback_token": "a1b2c3...",
    "callback_url": "http://192.168.178.104:3000/<site>/reply"
  }
}

// Client liest die Resource für volle Daten
resources/read { uri: "web-hoster-mcp://<site>/submissions" }
  → { "contents": [{ "uri": "...", "mimeType": "application/json", "text": "[{...}]" }] }
```

**Spec-Quelle:** [MCP-Server/Resources (2025-06-18)](https://modelcontextprotocol.io/specification/2025-06-18/server/resources) — `resources/subscribe`, `resources/unsubscribe`, `notifications/resources/updated`. C#-SDK (`ModelContextProtocol.Core` 0.4.0-preview.1) hat First-Class-Support via `McpServerResource` + `[McpResourceSubscribe]`-Attribute.

**Vorteile gegenüber Option A:**

- **Spec-konform** — alle standardkonformen MCP-Clients verstehen es ohne Custom-Logik
- **Klare Semantik:** Resource = state, Notification = state-change → Idiom der Wahl
- **Self-describing** — `resources/list` zeigt dem Client, was beobachtbar ist
- **Idempotent** — bei Verbindungsabbruch re-subscribt der Client, Server hat stabilen State

**Single-Shot-Antwort (R2, R3):** Wie B/C — braucht zusätzlichen `POST /<site>/<submission_id>/reply`-Endpoint mit Token-Validierung.

**Session-Routing (R1):** Identisches Problem wie Option A. Benötigt OpenClaw-Plugin-Support für `subscribeToResource` — wird über [Issue #21251](https://github.com/openclaw/openclaw/issues/21251) explizit angefragt (siehe Issue-Body: „agenr memory plugin" als konkretes Beispiel, unser Use-Case passt dazu).

| | |
|---|---|
| **Aufwand** | ~150 LoC Host (Resource-Provider mit `McpServerResource`, Subscribe-Handler, Update-Notification-Dispatcher) |
| **Pros** | Spec-konform, alle MCP-Clients verstehen es, klare Semantik, kein Custom-Namespace-Hack |
| **Cons** | Selbe OpenClaw-Blockade wie A; Issue #21251 muss erst implementiert werden |
| **Lock-Empfehlung** | ✅ **v0.2 sobald OpenClaw #21251 implementiert ist** — Vote + ggf. PR anbieten |

---

### Option D — MCP `resources/subscribe` + `notifications/resources/updated` (spec-konform)

**Architektur:** Der Host nutzt das MCP-Resources-Subsystem (statt Tools/Notifications) als standardisierten Push-Mechanismus.

**Setup:**

```jsonc
// Server-Capabilities (im initialize-Response)
{
  "capabilities": {
    "resources": {
      "subscribe": true,    // Client darf subscriptions registrieren
      "listChanged": true   // Server pusht bei Resource-Liste-Änderung
    }
  }
}

// Statische Resource-Liste pro Site
resources/list → {
  "resources": [
    {
      "uri": "web-hoster-mcp://<site>/submissions",
      "name": "Submissions",
      "mimeType": "application/json"
    }
  ]
}

// Client abonniert (einmal pro interessierter Site)
resources/subscribe { uri: "web-hoster-mcp://<site>/submissions" }

// Server pusht bei neuer Submission
notifications/resources/updated {
  "uri": "web-hoster-mcp://<site>/submissions",
  "_meta": {
    "submission_id": "2026-09-27T11-30-00_a8f2k1d3",
    "received_at": "2026-09-27T11:30:00",
    "callback_token": "a1b2c3...",
    "callback_url": "http://192.168.178.104:3000/<site>/reply"
  }
}

// Client liest die Resource für volle Daten
resources/read { uri: "web-hoster-mcp://<site>/submissions" }
  → { "contents": [{ "uri": "...", "mimeType": "application/json", "text": "[{...}]" }] }
```

**Spec-Quelle:** [MCP-Server/Resources (2025-06-18)](https://modelcontextprotocol.io/specification/2025-06-18/server/resources) — `resources/subscribe`, `resources/unsubscribe`, `notifications/resources/updated`. C#-SDK (`ModelContextProtocol.Core` 0.4.0-preview.1) hat First-Class-Support via `McpServerResource` + `[McpResourceSubscribe]`-Attribute.

**Vorteile gegenüber Option A:**

- **Spec-konform** — alle standardkonformen MCP-Clients verstehen es ohne Custom-Logik
- **Klare Semantik:** Resource = state, Notification = state-change → Idiom der Wahl
- **Self-describing** — `resources/list` zeigt dem Client, was beobachtbar ist
- **Idempotent** — bei Verbindungsabbruch re-subscribt der Client, Server hat stabilen State

**Single-Shot-Antwort (R2, R3):** Wie B/C — braucht zusätzlichen `POST /<site>/<submission_id>/reply`-Endpoint mit Token-Validierung.

**Session-Routing (R1):** Identisches Problem wie Option A. Benötigt OpenClaw-Plugin-Support für `subscribeToResource` — wird über [Issue #21251](https://github.com/openclaw/openclaw/issues/21251) explizit angefragt (siehe Issue-Body: „agenr memory plugin" als konkretes Beispiel, unser Use-Case passt dazu).

| | |
|---|---|
| **Aufwand** | ~150 LoC Host (Resource-Provider mit `McpServerResource`, Subscribe-Handler, Update-Notification-Dispatcher) |
| **Pros** | Spec-konform, alle MCP-Clients verstehen es, klare Semantik, kein Custom-Namespace-Hack |
| **Cons** | Selbe OpenClaw-Blockade wie A; Issue #21251 muss erst implementiert werden |
| **Lock-Empfehlung** | ✅ **v0.2 sobald OpenClaw #21251 implementiert ist** — Vote + ggf. PR anbieten |

---

### Option C — Konfigurierbarer HTTP-Webhook (empfohlen)

**Architektur:** Host POSTet an eine pro Deployment (oder pro Site) konfigurierte URL. Empfänger ist beliebig — OpenClaw-Hook-Plugin (B), Discord, Telegram-Bot, eigener Service, Webhook-Inspector (webhook.site), etc.

**Konfiguration (pro Deployment):**

```jsonc
// appsettings.json
{
  "Notifications": {
    "Enabled": true,
    "HookUrl": "https://192.168.178.104:18789/hooks/web-hoster-submission",
    "HookSecret": "<32-byte-hex>",                 // für HMAC-SHA256
    "TimeoutSeconds": 10,
    "RetryMaxAttempts": 4,                          // 1, 5, 25, 125 s ± Jitter
    "FailedDeliveryDir": "<SitesRoot>/.notifications/"
  }
}
```

**Pro-Site-Override (optional, v0.2+):**

```jsonc
// payload.json (zukünftig)
//   { ..., "notifications": { "hook_url": "...", "hook_secret": "..." } }
```

Wenn `notifications.hook_url` gesetzt ist, überschreibt es die globale `HookUrl`. So kann ein Site-Owner seine eigene Empfänger-URL registrieren.

**HTTP-Format:** identisch zu B (gleicher Body, gleiche HMAC-Header).

**Session-Routing (R1):** Verantwortung des Empfängers. Für OpenClaw als Empfänger gilt die B-Logik (Hook-Plugin → Automation → `sessionTarget`). Für andere Empfänger gilt deren eigenes Routing.

**Auth (R6):** HMAC-SHA256 (Pflicht), Bearer (optional, wenn Empfänger es will):

```
X-WebHoster-Signature: sha256=<hex>
X-WebHoster-Timestamp: <unix-seconds>     # ±300 s Replay-Window
X-WebHoster-Submission-Id: <id>           # Idempotency-Hint
X-WebHoster-Delivery-Attempt: <1..4>
```

`hex = HMAC_SHA256(HookSecret, body_bytes)` — byte-genau über den rohen Body, nicht das re-serialisierte JSON.

**Retry (R7):** Exponential-Backoff im Background-Task (fire-and-forget):

| Attempt | Delay (jitter ±20 %) |
|---|---|
| 1 | sofort (im Background-Task, nicht im HTTP-Pfad) |
| 2 | 1 s |
| 3 | 5 s |
| 4 | 25 s |
| 5 | 125 s (final, dann Fail-Persist) |

Persistierte Failed-Deliveries in `FailedDeliveryDir` als JSON:

```json
{
  "submission_id": "2026-09-27T11-30-00_a8f2k1d3",
  "site_path": "form-test-001",
  "attempts": 4,
  "last_error": "HTTP 502 from receiver",
  "last_attempt_at": "2026-09-27T11:30:45"
}
```

Optionaler MCP-Tool `get_failed_notifications(site_path)` listet diese für manuelles Re-Trigger oder Diagnose.

**Single-Shot-Antwort (R2, R3):** Wie B — Host braucht zusätzlich `POST /<site>/<submission_id>/reply`-Endpoint. Der Token wird in `callback_url` der Notification mitgegeben, der Agent folgt dem Link.

| | |
|---|---|
| **Aufwand** | ~120–150 LoC im Host (HMAC-Sign + Retry-Task + Failed-Delivery-File-Writer + Reply-Endpoint) |
| **Pros** | Flexibel (jeder Empfänger), keine OpenClaw-Lock-in, kann mit B kombiniert werden wenn Empfänger = OpenClaw |
| **Cons** | Erfordert *einen* Empfänger um nützlich zu sein; ohne `HookUrl` ist die Funktion effektiv no-op (gewollt) |
| **Lock-Empfehlung** | ✅ **empfohlen als v0.1** — deckt 80 % der Use-Cases ab |

---

## Cross-cutting: Session-Routing (R1)

Das Host muss wissen, **welche Session** zu benachrichtigen ist. Vorschlag:

### Strategy: Session-Capture bei Deploy

1. Beim MCP-Tool-Call `deploy` ist die aufrufende Session in OpenClaw per `sessionKey` bekannt.
2. Der MCP-Server-Host hat aber **keinen direkten Zugriff** auf den `sessionKey` des Aufrufers — MCP-Stdio-Transport hat kein Session-Konzept auf Application-Layer (Spezifikation: `initialize`-Handshake enthält nur `clientInfo`, keine Session-ID).
3. Workaround: optionaler Parameter `session_hint` im `deploy`-Tool (vom Agent selbst gesetzt). Per Konvention propagiert der Agent seine eigene `sessionKey` aus seinem Kontext.
4. Persistenz: `session_hint` wird in `payload.json` (oder neuer `meta.json`) der Site gespeichert.
5. Notification sendet `X-WebHoster-Session-Hint: <hint>` als zusätzlichen Header; Empfänger (OpenClaw-Hook-Plugin) mappt ihn auf `sessionTarget`.

### Fallback-Hierarchie

1. `site.metadata.session_hint` (gesetzt beim Deploy)
2. `Notifications.DefaultSession` (Global-Config, default: `main`)
3. `main` (Hard-Coded Fallback)

Wenn alles unbekannt → Notification geht an `main`-Session. Agent entscheidet dann, was zu tun ist.

### Out-of-Scope (v0.1)

- **Cross-User-Multi-Tenancy** — eine Site kann nicht an mehrere Sessions gleichzeitig liefern (z. B. wenn 2 Agenten denselben Form deployt haben). Wer zuerst `deploy`-Hint setzt, gewinnt.
- **Session-Discovery** — der Host fragt nicht aktiv bei OpenClaw nach, *welche* Sessions existieren. Das wäre ein weiterer Endpoint.

---

## Cross-cutting: Unique Callback-Token (R2, R3)

### Generation

```csharp
// Bei POST /<site>/submit, direkt nach `JsonDocument.Parse` (vor Atomic-Write):
var callbackToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
// → 32-char hex, kryptographisch stark (RNGCryptoServiceProvider-Quelle)
```

### Persistenz

Token wird im Submission-JSON mitgespeichert (neues Feld, **additive change, kein Schema-Bruch**):

```json
{
  "callback_token": "a1b2c3d4e5f6...",
  "data": { /* original submission body */ },
  "_meta": {
    "received_at": "2026-09-27T11:30:00",
    "consumed": false,
    "consumed_at": null,
    "consumed_by": null
  }
}
```

`_meta` ist **intern**, wird nicht über `get_submissions` exposed (in `SiteManager.GetSubmissionsAsync` rausgefiltert — analog zu `payload.json`).

### Reply-Endpoint

`POST /<site>/<submission_id>/reply` mit Header `X-WebHoster-Callback-Token: <token>`:

| HTTP | Wann |
|---|---|
| `204 No Content` | Token valide, noch nicht verbraucht, Reply akzeptiert. Token wird auf `consumed=true` gesetzt (atomic write) |
| `410 Gone` | Token verbraucht (`consumed=true`) oder Submission existiert nicht |
| `401 Unauthorized` | Token fehlt oder falsch |
| `429 Too Many Requests` | Reply-Rate-Limit (z. B. max 1 Reply pro Submission, **hard limit**) |

### Optional: Reply-Logging

Reply wird im Site-Folder geloggt als `<submission_id>.reply.json` (atomic write) für Audit-Zwecke. Inhalt: `{ "replied_at": ..., "reply_body": <bytes> }`. Per Default aus, per Site-Flag aktivierbar (`notifications.log_replies: true`).

### Lifecycle

- Token gilt bis zur Retention-Expiry der Submission (siehe [12 Retention](./12-retention.md))
- Beim Hard-Delete der Submission wird auch der Token ungültig (atomar)
- Token kann **nicht rotiert** werden — bewusst, damit Reply eindeutig der ursprünglichen Submission zugeordnet bleibt

---

## Empfehlung

**Lock-Vorschlag für v0.1: Option C + minimaler B-Anteil.**

Konkret:

1. **Implementiere Option C** im Host (~120 LoC, neuer `NotificationsHostedService` + Reply-Endpoint).
2. **Stelle ein fertiges OpenClaw-Hook-Plugin** bereit (`plugins/web-hoster-submission/`), das die `POST /hooks/web-hoster-submission` empfängt und via `automations.add({...agentTurn})` in die richtige Session injiziert. Das ist *nur* Konfig, nicht zwingend Teil der Web-Hoster-Library.
3. **Dokumentiere** in der README: wer *kein* OpenClaw hat, kann beliebigen HTTP-Empfänger eintragen (Discord, Telegram, eigener Service).

**Roadmap v0.2 — sobald OpenClaw-Issue [#21251](https://github.com/openclaw/openclaw/issues/21251) implementiert:**

- **Option D aktivieren** (Resources/Subscribe): Host deklariert `capabilities.resources.subscribe: true` und exponiert Submissions als MCP-Resources. Erste echte MCP-native Push-Lösung.
- Option A (custom namespaced Notification) wird dadurch obsolet — D ist die saubere Variante.
- OpenClaw-Plugin kann via `api.subscribeToResource(...)` direkt auf Updates lauschen und sie in die Session injizieren (geplant per #21251).

**Issue-Tracking-Empfehlung:**

- 👍 Vote auf [openclaw/openclaw#21251](https://github.com/openclaw/openclaw/issues/21251) — der Use-Case passt exakt zum Issue-Text
- Watch das Issue, ggf. PR anbieten wenn Maintainer Signal geben

**Begründung:**

- Option C ist die einzige, die ohne OpenClaw-Spezifika funktioniert (Lock-in-Vermeidung).
- Hook-Plugin für OpenClaw-Empfang ist Konfig, nicht Library-Code → klare Trennung.
- Option A und D sind beide blockiert durch dieselbe OpenClaw-Lücke (#21251) — sie sind langfristig die saubere MCP-native Lösung.
- Option B ohne C ist zu schwer (Library + Plugin parallel entwickeln).

---

## Out of Scope (v0.1)

- **WebSocket/SSE-Stream** vom Host zum Agent (Long-Lived-Connection statt Request/Response)
- **Multi-Receiver-Fanout** (eine Submission an mehrere URLs) — v0.2+
- **Per-Field-Validation/Transform** der eingehenden Daten vor Notification
- **Bi-direktionale Konversation** (mehrere Round-Trips pro Submission) — explizit nur Single-Shot
- **Token-Refresh** / Re-Issue eines neuen Callback-Tokens für eine alte Submission
- **Notification-Filter** (z. B. „nur senden wenn `data.priority > 5`") — Empfänger-seitig lösbar
- **A2UI-Action-Callbacks** (siehe Spec 16) — separate Spec, falls überhaupt gewünscht
- **MCP-Sampling** (`sampling/createMessage`) — Server würde beim Client Modell-Calls anfordern, anderer Use-Case

---

## Offene Design-Fragen

| # | Frage | Vorschlag |
|---|---|---|
| Q1 | Hook-Plugin-Repo: gleiches Repo wie Web Hoster oder separates? | separates (`schirkan/web-hoster-mcp-openclaw-hook`) — unabhängige Releases, klare License-Trennung |
| Q2 | `session_hint` via `deploy`-Parameter — bricht `deploy`-Signatur? | additive (neuer optionaler Parameter), kein Bruch |
| Q3 | Persistierte Failed-Deliveries — wie lange aufheben? | bis zur Submission-Retention, analog |
| Q4 | HMAC über raw body oder canonicalized JSON? | raw body (byte-genau), wie Stripe/GitHub |
| Q5 | Replay-Window — 5 min oder 1 min? | 5 min (kompromiss zwischen Sicherheit und Clock-Skew-Toleranz) |
| Q6 | Retry-Task läuft in welchem Prozess? | In-Process-BackgroundService im Host (einfachster Fall); v0.2+ optional als externer Worker |
| Q7 | Token-Encryption-at-Rest? | Klar nein — Token ist nicht-gehheim; Authentizität kommt von HMAC, nicht vom Token selbst |
| Q8 | Reply-Rate-Limit: 1 pro Submission oder konfigurierbar? | Default 1, konfigurierbar pro Site |
| Q9 | Was passiert bei Gateway-Restart zwischen Submission und Notification-Delivery? | Retry-Task im Host versucht 4×, bei Total-Fail Persist + `get_failed_notifications`. **Risiko: Window zwischen Submission und 4. Retry = ~156 s.** |

---

## Cross-References

- [08 Tool: `get_submissions`](./08-tool-get-submissions.md) — Polling-Fallback bleibt Quelle der Wahrheit
- [12 Retention](./12-retention.md) — Token-Lifecycle folgt Submission-Retention
- [17 Hosting Type: `json-schema-form`](./17-hosting-type-schema-form.md) — Submit-Endpoint, Out-of-Scope-Aufhebung
- [18 Authorization (Bearer Token)](./18-authorization.md) — Auth-Muster (HMAC statt Bearer, aber Form-Familie)

## Changelog

- **2026-09-27 (v0.1 Draft):** Initiale Spec. Drei Optionen ausgearbeitet, Session-Routing-Strategie + Unique-Callback-Token skizziert, Empfehlung Option C formuliert. **Status: nicht gelockt — Diskussion nötig.**
- **2026-09-27 (Recherche-Update):** [OpenClaw-Issue #21251](https://github.com/openclaw/openclaw/issues/21251) bestätigt: Plugins können heute KEINE MCP-Notifications empfangen. Option A (custom namespaced notification) damit heute nicht funktional. **Neue Option D** (MCP `resources/subscribe` + `notifications/resources/updated`) als spec-konforme v0.2-Kandidatin hinzugefügt — leidet unter derselben OpenClaw-Blockade wie A. **Status: nicht gelockt — Diskussion nötig.**
