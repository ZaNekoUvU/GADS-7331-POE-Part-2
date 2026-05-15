# Ollama integration plan — Back To The Forge

Technical plan for **local inference**: model choice, timing expectations, data flow through Unity, prompt contracts, and risks. Implementation lives primarily in **`Assets/Scripts/Dialogue/OllamaDialogueService.cs`** and call sites (NPC dialogue, forge quests, combat encounter intro, mercenary hire opening).

---

## 1. Model choice

### Default assumption in repo

- **Transport:** HTTP **`POST {host}/api/chat`** (non-streaming JSON).
- **Host:** `http://127.0.0.1:11434` (local Ollama).
- **Model string:** Must match **`ollama list`** exactly (scene default often **`qwen3:8b`** — configurable on **`OllamaDialogueService`** or **`Ollama Dialogue Settings`** asset).

### Selection criteria

| Criterion | Why it matters |
|-----------|----------------|
| **Parameter size (e.g. 3B–8B)** | Short RPG lines + small JSON; large models increase RAM and cold latency. |
| **Instruction following** | Forge quest path expects **strict JSON** (`materialName`, `requestLine`). |
| **English fluency & brevity** | Encounter narrator and merc lines are **one beat**, not essays. |
| **VRAM / RAM budget** | Unity Editor + play mode + Ollama share the machine; leave headroom. |

### Recommendation for coursework demos

- Pick **one pinned tag** (e.g. `qwen3:8b`) and document it in **`refinements-changes.md`** so graders can reproduce.
- Avoid swapping models mid-demo without re-testing **JSON quest** and **sanitizer** behavior.

---

## 2. Inference timing

### Configured limits (code defaults)

From **`OllamaDialogueService`** / **`OllamaDialogueSettings`**:

| Parameter | Typical value | Role |
|-----------|----------------|------|
| **`requestTimeoutSeconds`** | **45** (clamp 5–120 on asset) | Unity `UnityWebRequest.timeout` — hard cap wait |
| **`maxTokens`** | **140** (asset clamp 32–512) | Bounds completion length |
| **`temperature`** | **~0.85** | Higher = more variation; JSON path still relies on prompt discipline |

### Expected wall-clock (order of magnitude)

Highly hardware-dependent. Rough bands for **single non-streaming** `/api/chat` call:

- **Warm model, small output:** ~**0.5–3 s**
- **Cold load / first prompt after idle:** ~**several seconds+**
- **Under load (CPU-only, large context):** can approach **timeout**

### Unity integration model

- All calls are **coroutine-driven** (`yield return SendWebRequest`) — **main thread** waits without blocking physics loop, but **gameplay may feel paused** while dialogue shows **“…”** or encounter intro waits.
- **`_busy` gate:** only **one** Ollama request at a time through this service instance; overlapping callers get **`busy`** error and should use **fallback** paths (implemented at call sites).

### UX implications

- **Mercenary / NPC:** `ShowAwaitingLine` masks short waits.
- **Random encounter:** player waits on staging line **before** combat loads — acceptable if latency stays low; annoying if timeout → fallback every time.

---

## 3. Data flow

### High-level

```
Unity gameplay event
    → Build system + user strings (prompt contract)
    → JSON body for /api/chat (model, messages, options)
    → UnityWebRequest POST to Ollama
    → Parse assistant message from JSON response
    → SanitizeLine / strict JSON parse (feature-specific)
    → SimpleRpgDialogueUI OR game state (quest begin)
```

### Major call paths

| Feature | Entry | Output use |
|---------|--------|------------|
| **Generic NPC line** | `RequestNpcLineCoroutine(NpcDialogueProfile)` | Spoken line → dialogue UI |
| **Roleplay line** | `RequestRoleplayLineCoroutine(systemPrompt, userPrompt)` | Freeform line → dialogue UI / encounter text |
| **Forge quest offer** | `RequestForgeQuestOfferCoroutine(...)` | Parsed **`ForgeQuestOfferDto`** → quest manager |
| **Combat encounter intro** | `CombatStarter` → roleplay coroutine | One narrator line → dialogue UI → then combat |
| **Mercenary opening** | `CompanionRecruiter` → roleplay coroutine | Hire pitch → dialogue UI → hire menu |

### Failure paths

- HTTP error, API error field, empty content, malformed quest JSON → **logged** (optional) → **fallback string** or scripted line.
- **`IsBusy`** → skip HTTP; callers fall back without freezing the server.

### Persistence / privacy

- Prompts and replies stay **on localhost** unless Ollama is explicitly configured otherwise.
- No cloud API keys in this pipeline.

---

## 4. Prompt structure

### Shared mechanics

- **Chat-style messages:** system + user content assembled in code (see `BuildChatJsonManual` pattern in service).
- **Sanitization:** `SanitizeLine` trims wrappers/models sometimes emit (quotes, code fences) — **NPC / flavor** paths.
- **Strict paths:** Forge quest uses explicit **JSON-only** system instructions + parsing; mercenary / narrator prompts forbid meta (“as an AI…”).

### Pattern A — In-character speech (NPC / mercenary)

- **System:** Character name + game context + **persona** (from **`NpcDialogueProfile`** or **`HireableCompanionOffer.PersonaForLlm`**) + **CRITICAL** block: *only spoken words, 1–3 sentences, no meta*.
- **User:** Situational cue (traveler approached, hire price optional mention, etc.).

### Pattern B — Strict JSON (forge commission)

- **System:** Exact key names, no markdown, no extra keys.
- **User:** Persona + “output JSON now” style instruction.
- **Consume:** `TryParseForgeQuestOffer` → starts quest with invented material name.

### Pattern C — Narrator staging (random encounter)

- **System:** BG3-like **economy of words**, bandits as threat, single line.
- **User:** Motif hint + “don’t explain much.”

### Temperature & max tokens

- **Low max tokens** keeps costs and latency bounded.
- **Moderate-high temperature** increases variety for gossip / encounter lines; **JSON path** relies more on wording than on low temperature alone — if JSON fails often, lower temperature or tighten prompts.

---

## 5. Risks

| Risk | Impact | Mitigations already / suggested |
|------|--------|--------------------------------|
| **Ollama offline** | No AI lines; empty errors | Fallback lines / scripted **`OpeningLine`** / static encounter strings |
| **Latency spikes** | Player waits; encounter intro delays combat | Timeout + fallback; keep prompts short; smaller model |
| **`_busy` contention** | Second request fails | Queue UX or disable second talker; mercenary path checks busy |
| **JSON drift** | Quest offer parse fails | Strict prompt + parse failure → fallback commission + warning log |
| **Hallucinated facts** | Lore contradicts game | Keep prompts grounded; LLM only affects **flavor text** & quest names, not combat math |
| **Content safety** | Unexpected output | SanitizeLine; designer review; optional blocklist pass (not implemented) |
| **VRAM / RAM exhaustion** | Editor crash or swap thrashing | Close other apps; use smaller quant; document min specs in **`setup.md`** |
| **Upgrade drift** | Unity / Ollama API changes | Pin versions; integration is thin (`/api/chat` JSON) |

---

## 6. References in repo

| Asset / script | Purpose |
|----------------|---------|
| `OllamaDialogueService.cs` | HTTP client, sanitization, busy flag, coroutine APIs |
| `OllamaDialogueSettings.cs` | Shared ScriptableObject defaults |
| `NpcDialogueProfile.cs` | NPC name + persona + fallbacks |
| `NpcOllamaDialogue.cs` | Trigger → talk coroutine |
| `BlacksmithQuestGiver.cs` | Forge JSON quest flow |
| `CombatStarter.cs` | Random encounter narrator line |
| `CompanionRecruiter.cs` | Mercenary LLM opening |
| `docs/setup.md` | Install & configure Ollama |
| `docs/high-concept.txt` | Why local LLM for this game |

---

*Update this document when changing default model, timeout, or adding new `/api/chat` call sites.*
