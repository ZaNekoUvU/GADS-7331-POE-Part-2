# Prompt archive — prompts-used.md

Archive of **Ollama `/api/chat`** prompts used in **Back To The Forge**, plus **test outcomes**, **failure modes**, and **iteration reasoning**.  
Prompt **sources of truth** are the C# files cited below — update this doc when those strings change.

---

## Shared request envelope

All paths build JSON manually in **`OllamaDialogueService.BuildChatJsonManual`**:

- **`model`** — from inspector / `OllamaDialogueSettings`
- **`stream`: false**
- **`think`: false** (suppresses reasoning traces on compatible models)
- **`messages`** — `[{ role: system, content }, { role: user, content }]`
- **`options`** — `num_predict` ← **maxTokens** (default **140**), `temperature` (default **~0.85**)
- **Timeout** — UnityWebRequest, default **45 s**

---

## 1. NPC idle talk (`RequestNpcLineCoroutine`)

**Files:** `OllamaDialogueService.cs` (`BuildSystemPrompt`, `BuildUserPrompt`), `NpcOllamaDialogue.cs`

### System prompt (template)

```
You are {CharacterName}, an NPC in the retro fantasy game 'Back to the Forge' (mines, forge, iron ore, risky wilds).

{PersonaDescription}

Context you treat as true for your role:   ← optional block if LocalKnowledge non-empty
{LocalKnowledge}

CRITICAL — You write ONLY what this character says out loud in the game, 1-3 short sentences. Direct speech only. Do NOT plan, explain, or discuss instructions. Do NOT say: the user, okay, let me think, I need to, I should, wait, hmm, respond as, my reply, or anything about roleplaying or prompts. Never describe the scene from a writer's perspective. Start immediately with words spoken to the traveler.
```

### User prompt (fixed)

```
The traveler is standing with you. Speak your line now — only the words your character says aloud (greeting, gossip, warning, or complaint). Nothing else. No preamble.
```

### Tested / observed outcomes

| Outcome | Example model behavior | Notes |
|---------|------------------------|------|
| **Success** | *“Cold iron’s cheap today — mind your purse near the east gate.”* | Short, in-character, no meta. |
| **Success** | *“ForgeMaster wants ore before sundown. You hauling?”* | Uses tone from persona asset. |
| **Failure** | *“Okay, let me respond as the NPC…”* | Violates CRITICAL; **`SanitizeLine`** may trim some junk; **`NpcDialogueProfile.PickRandomFallback`** if request fails. |
| **Failure** | Empty / HTTP error when Ollama down | UI shows **fallback line** from profile. |
| **Failure** | `busy` — second NPC fires while first request runs | Second NPC skips HTTP; fallback line. |

### Iteration notes

- **CRITICAL block** added after early tests returned meta (“as an AI”, “here’s my line”).
- **User prompt** kept blunt (“nothing else”) to reduce preamble leakage.

---

## 2. Forge quest commission (`RequestForgeQuestOfferCoroutine`)

**File:** `OllamaDialogueService.cs`

### System prompt (fixed)

```
You output ONLY valid JSON with exactly two string keys: materialName and requestLine. No markdown, no code fences, no extra keys, no commentary. materialName: one invented fantasy ore or mineral name (2–6 words, no quotes inside the string). This exact name is what appears in the traveler's inventory when they collect the commission ore. requestLine: what the blacksmith says out loud asking the traveler to fetch that same material by name (1–3 short sentences, direct speech, same character voice as your persona — no meta, no 'the user').
```

### User prompt (template)

```
You are {blacksmithName}, a blacksmith quest giver.
Persona: {personaSummary}
The traveler just came to the counter. Output the JSON now for a new mining commission.
```

### Tested / observed outcomes

| Outcome | Example | Notes |
|---------|---------|------|
| **Success** | `{"materialName":"Ashglass Cinder Ore","requestLine":"I need Ashglass Cinder Ore from the wild seams — bring me chunks, I'll pay fair."}` | Parsed → **`ForgeQuestOfferDto`** → quest starts. |
| **Failure** | Markdown fences ```json … ``` | Parser strips fences in **`TryParseForgeQuestOffer`** path — still can fail if keys wrong. |
| **Failure** | Extra keys / prose outside JSON | **`TryParseForgeQuestOffer`** fails → scripted fallback commission (`BlacksmithQuestGiver`). |
| **Failure** | `busy` | Immediate fallback ore name path (see `BlacksmithQuestGiver`). |

### Iteration notes

- **Strict JSON-only** system prompt reduces but does not eliminate drift; **fallback** is mandatory for submissions.
- **`materialName`** tied to inventory display — prompt stresses **exact string** usage.

---

## 3. Random encounter narrator (`CombatStarter.RandomEncounterIntroThenFight`)

**File:** `CombatStarter.cs` — uses **`RequestRoleplayLineCoroutine`**

### System prompt (current)

```
You are the narrator in Baldur's Gate 3: bone-dry, clipped, present tense. Output ONE short line only — aim under ~14 words, often starting with You / Your / They're / Something. The threat is bandits; name them bandits once (or imply them clearly). Hint one sharp sensory beat at most — no lore, no staging directions, no metaphors piled up. No quotes, no markdown, no second sentence.
```

### User prompt (template)

```
Bandit encounter. One narrator line. Optionally nod at: {motif}. Don't explain much.
```

**Motifs** (random one per roll):  
`wrong silence`, `too-quiet birds`, `fresh hoofprints`, `broken cage-straps`, `cold ash`, `a snapped branch`, `dust kicking up`, `eyes from the ditch`, `steel catching sun`, `someone counted steps wrong`

### Scripted fallbacks (no LLM)

- `You've walked into bandits.`
- `Bandits rise — no preamble.`
- `Ambush. They were waiting.`
- `Steel answers before words do.`
- `They're already closing.`

### Tested / observed outcomes

| Outcome | Example | Notes |
|---------|---------|------|
| **Success** | *“You're spotted — bandits peel off the ridge.”* | Fits BG3-ish economy + bandits. |
| **Success** | *“Something snaps behind you — bandits.”* | Single beat. |
| **Failure** | Two paragraphs of scenery | **Earlier** prompt allowed ~35 words + “atmospheric”; players asked **less descriptive** → tightened to **~14 words**, BG3 voice. |
| **Failure** | Wolves / generic monsters | **Earlier** iteration; prompt now **forces bandits**. |
| **Failure** | Timeout / `busy` | **`FallbackBanditEncounterLines`** used. |

### Iteration notes

1. **v1** — Long JRPG staging (~35 words), terrain + cause; felt overwritten.  
2. **v2** — **BG3 narrator**: dry, present tense, **~14 words**, bandits explicit.  
3. **Motif injection** — reduces repetition without long prose.

---

## 4. Mercenary hire opening (`CompanionRecruiter.YieldMercenaryOpeningFromOllama`)

**File:** `CompanionRecruiter.cs` — **`RequestRoleplayLineCoroutine`**

### System prompt (template)

```
You are {characterName}, an NPC mercenary for hire in the retro fantasy game "Back to the Forge" (mines, forge, risky roads).
Persona:
{persona}

{Optional: Designer tone hint (do not quote verbatim; match vibe): {openingLine from ScriptableObject}}

CRITICAL — Output ONLY what this character says out loud, 1–3 short sentences. Direct speech only. Do NOT plan, explain, or discuss instructions or prompts. Do NOT say: the user, okay, let me think, I should, respond as, my reply. Start immediately with spoken words to the traveler.
```

**Persona source:** **`HireableCompanionOffer.PersonaForLlm`** — **`personalityVoice`** if set, else **`personalityTrait`**, else short default blurb.

### User prompt (template)

```
The traveler just stepped up to your posting. Pitch yourself — your hire fee today is {hireCostGold} gold (mention it only if it fits naturally). Invite them to hire you or ask where they're headed.
```

### Tested / observed outcomes

| Outcome | Example | Notes |
|---------|---------|------|
| **Success** | Voice matches **personalityTrait** (“cynical rogue”) without copying **`openingLine`** verbatim. | Tone-hint clause steers without locking text. |
| **Success** | Mentions coin naturally when cost fits personality. | User prompt allows optional price mention. |
| **Failure** | Prints JSON / bullet list | Rare; treat as error → **scripted `openingLine`** shown via **`SetDialogueLineAndAllowAdvance`**. |
| **Failure** | `busy` | Skips LLM branch; uses **static opening line** only (`CompanionRecruiter`). |

### Iteration notes

- **Designer-authored personas** on **`HireableCompanionOffer`** assets (e.g. “Cheerful scout — …”) feed **`PersonaForLlm`** so Ollama matches established merc identities.
- **`openingLine`** is **hint only** — avoids stale duplicate text while preserving vibe.

---

## 5. Roleplay line helper (`RequestRoleplayLineCoroutine`)

**File:** `OllamaDialogueService.cs`

Generic entry used by **encounter intro** and **mercenary opening**. No fixed strings inside the service — callers supply **system** + **user**. Failures surface as `onError`; **`SanitizeLine`** cleans assistant content on success.

---

## Appendix — quick failure checklist

| Symptom | Likely cause |
|---------|----------------|
| Always fallback | Ollama not running, wrong **model** tag, timeout |
| Always `busy` | Overlapping requests on single **`OllamaDialogueService`** |
| Quest stuck on fallback ore | JSON parse fail — tighten model or lower temperature for JSON path |
| NPC speaks meta | Add forbidden phrases to CRITICAL; reduce temperature slightly |

---

## Related docs

- **`docs/ollama-plan.md`** — architecture, timing, risks  
- **`docs/refinements-changes.md`** — feature-level change log  
- **`docs/setup.md`** — install Ollama / Unity  

---

*Maintainers: when you change a prompt string in code, paste the new version here and add one success/fail row from playtest.*
