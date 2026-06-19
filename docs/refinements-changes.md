# Refinements & scope changes log

Continuous record of notable adjustments and **AI-assisted** implementation decisions during Back To The Forge / POE Part 2 development. (Companion chat sessions + Cursor-assisted edits.)

---

## Status snapshot (early playtest build → current)

| Area | A few weeks ago (post–first playtest) | Current build |
|------|----------------------------------------|---------------|
| **Onboarding** | Objectives and controls mostly explained verbally during playtest | Quest log, waypoint arrow, gather prompt, pause **Controls** screen |
| **Encounters** | High roll frequency; could chain fights back-to-back | 3s roll interval, 45s shared cooldown, movement gate; **60s grace** after death/end-day teleport |
| **Mercenaries** | Camp spawns + hire flow; tinted placeholders common | Generated walk/battle art wired; **C-key** field chat; chain follow formation; scale/walk-direction fixes |
| **Death** | Player defeat ended combat only (or soft-lock risk) | 5s death screen → respawn at session start → lose inventory, **keep gold**, advance day, new commission |
| **Audio** | Silent or minimal | Menu / exploration / combat music + SFX (Pixabay sources); ducking during dialogue |
| **Art** | Placeholder squares on many NPCs | AI-generated player, NPC, merc, and enemy sprites in `Assets/Sprites/` |
| **Still open** | — | Ground pathing, flee reliability, main-menu HUD leak, some combat targeting clarity |

---

## Rendering & additive combat

| Phase | Change | Rationale |
|-------|--------|-----------|
| Combat isolation | **`CombatAdditiveCoordinator`** walks loaded objects and disables **cameras, listeners, renderers, and `Canvas`** whose `gameObject.scene` is **not** the combat scene; restores on unload. | Exploration terrain/sprites stayed visible under additive combat; combat camera drew both scenes. |
| Scene tweaks | Combat background **Z** normalized; combat camera **occlusion culling** disabled where it hid the BG. | Background disappeared or sorted incorrectly. |
| API hygiene | Replaced obsolete **`FindObjectsByType(..., FindObjectsSortMode)`** with two-arg overload **`*FindObjectsInactive.Include)`** only. | Unity 6 deprecation warnings (CS0618). |
| Combat pause | Exploration time paused when combat loads; combat HUD fades when pause menu open during fights. | Prevent double encounters; pause readable over battle UI. |

---

## Mercenary camp & spawning

| Phase | Change | Rationale |
|-------|--------|-----------|
| Spawn clearance | **`MercenaryCampSpawner`** uses **`Physics2D.OverlapCircle`** with **triggers included**, ignores **`CompanionRecruiter`** colliders, enforces **minimum separation** between hires. | Props like trees used large **trigger** volumes; ignoring triggers spawned inside canopy; merc triggers didn't block each other. |
| Editor anchors | **`MercenarySpawnPoints`** child transforms under **`MercenaryCamp`**; roster slot index maps to child index; optional physics solver flag. | Designer-placed squares / clearing layout vs catalog-only coords. |
| Pose & DDOL | **`Instantiate(template, position, rotation, parent)`** + **`CompanionRecruiter.CommitSpawnPoseSnapshot()`** after configure. | Awake captured wrong home pose before transform assignment; return-home could snap wrong. |
| Data sync | **`MercenaryRosterCatalog`** world positions aligned with anchor layout as fallback. | Ensures spawns even if inspector references fail. |
| Follower triggers | Hired merc **colliders disabled** in world; **`PlayerMovement2D.IsPlayerCharacterCollider`** leader-only. | Followers duplicated quest pickups and risky-ground rolls. |
| Follow formation | **`CompanionFollower2D`** chain follow (each merc trails the one ahead) with gap + lateral offset. | Hired party stacked on the player. |
| Visual scale | **`MercenaryVisualApplier`** scales from **walk-frame** reference height (not battle-ready portraits). | Camp/return-home scale drifted tiny or huge between day cycles. |
| Walk direction | Swapped L/R sheet columns and animator clip mapping; followers animate from **movement direction** while moving. | Side walks looked backwards or moonwalked while catching up. |

---

## Random encounters & tone

| Phase | Change | Rationale |
|-------|--------|-----------|
| Encounter intro | **`CombatStarter.StartRandomEncounterWithLlmIntro`** — Ollama **one-line** staging; enemies framed as **bandits**; **`RiskyGroundEncounter2D`** calls this instead of immediate fight; guard flag prevents stacked intros. | Wanted narrative beat before combat without blocking non-random fights. |
| Narrator style | Prompts tightened for **short, BG3-like** narrator economy (clipped, low word count). | First pass was too descriptive. |
| Pacing (playtest) | **10% per roll tick** (default every **3s**), **45s shared cooldown** after any risky-ground fight, **movement gate** (~1.1 units between rolls). | Playtesters hit encounter loops; chance was per tick while standing in a huge zone. |
| Death / teleport grace | **`RiskyGroundEncounter2D.NotifyPlayerSafeTeleport()`** — **60s** suppression + clear stale zone tracking after **player death respawn** or **end forging day**. | Respawn at home could instantly re-trigger combat. |

---

## Player death & session reset

| Phase | Change | Rationale |
|-------|--------|-----------|
| Death flow | **`PlayerDeathController`** — 5s realtime death overlay, end combat, apply penalties, respawn. | Clear feedback when the hero falls in combat. |
| Penalties | **`BlacksmithMaster.ApplyDeathDayAdvance()`** — clear inventory (**no sell gold**), increment day, restore veins, clear hires, roll market; **gold kept**. | Harsh but fair penalty distinct from voluntary end-of-day sell. |
| Respawn | **`PlayerSessionStartRecorder`** + **`PlayerStartLocation`** capture exploration start pose; respawn on death and end-of-day. | Return player to village start after bad run or day turnover. |
| UI block | Death sequence blocks pause, movement, and gathering. | No menu overlap during death overlay. |

---

## Mercenary dialogue & LLM

| Phase | Change | Rationale |
|-------|--------|-----------|
| Persona pipeline | **`HireableCompanionOffer.PersonalityTrait`** + optional **`personalityVoice`**; **`PersonaForLlm`** selects richer text for prompts. | Use designer-written personalities for AI-spoken hire pitches. |
| Recruiter flow | **`CompanionRecruiter`** optional **`useOllamaMercenaryOpening`** — ShowAwaitingLine → **`RequestRoleplayLineCoroutine`** → advance; fallback to scripted **`openingLine`**. | Same UX pattern as other Ollama NPCs; offline-safe. |
| Field chat | **`CompanionTalkMenuController`** — **C** opens party picker → **`CompanionConversationUi`** free-text Ollama chat; **E** no longer talks to hired mercs in field. | Single clear input for ally dialogue; removed duplicate **`HiredCompanionDialogue`**. |
| Morale skills | Dialogue JSON parsed into **`CompanionMoraleState`**; combat handoff buffs/debuffs party ATK/HP/MP regen. | "Allies judge your answers" design goal from later iteration. |

---

## Onboarding, quest HUD & wayfinding

| Phase | Change | Rationale |
|-------|--------|-----------|
| Quest log | **`QuestLogUI`** top-right objective panel (dialogue-style **hud-panel**); hidden during combat. | Playtesters lacked obvious objectives. |
| Waypoint arrow | **`QuestWaypointDirector`** + **`QuestWaypointArrow`** — blue fill, white border; blacksmith → commission ore → supplementary veins. | Direction without verbal explanation. |
| Gather prompt | **`PlayerMiningController`** bottom-center "Hold E to gather" (merged; **`GatherResourcePromptUI`** removed). | Mining loop not self-evident. |
| Controls reference | **`PauseMenuController`** + **`GameControlsReference`** — in-game key list including **C** for merc talk. | Reduce reliance on team explaining controls at booth. |
| Pickup feed | **`PickupLogUI`** bottom-right `+N Item` / `+N Gold` on inventory events. | Confirm economy actions without opening Tab. |
| Blacksmith talk | Forge small-talk system prompt includes **live inventory** facts to reduce contradictory LLM lines. | LLM invented items player didn't have. |

---

## HUD / UI Toolkit

| Phase | Change | Rationale |
|-------|--------|-----------|
| Gold visibility | **`GoldDisplayUI`** UIDocument **sorting order** raised **above** Tab inventory (`4550` vs `4500`). | Full-screen inventory layer drew over gold; currency vanished while holding Tab. |
| Gold placement | Gold shown **inside inventory panel** bottom-right via **`InventoryPanelToggle`**. | Consolidated economy readout with Tab HUD. |
| Pause blocking | Esc pause blocked during dialogue, forge choices, **companion talk flow**, tutorial, and **death sequence**. | Accidental pause over modal UI; race on C-menu close. |
| Dialogue shell | **`SimpleRpgDialogueUI`** DDOL overlay relocated off world-space Game Manager canvas. | Dialogue invisible or main-menu HUD leak. |

---

## Audio

| Phase | Change | Rationale |
|-------|--------|-----------|
| Runtime audio | **`GameAudioController`** (DDOL) — main menu, exploration, and combat music; quest/engage/death/attack SFX from **`Assets/Resources/Audio/`**. | Game was silent during demo/playtest builds. |
| Sources | Music and SFX from **Pixabay** (see **`docs/reference-list.txt`**); clips loaded via Resources + optional **`GameAudioLibrary`** asset. | Licensing and citation for coursework. |
| Mix behaviour | Exploration music pauses in combat; **dialogue ducking** lowers music during NPC/combat-intro lines; main menu intro trim. | Readability of LLM lines; long silent lead on menu track. |

---

## Character art (AI-generated)

| Phase | Change | Rationale |
|-------|--------|-----------|
| Exploration sprites | Player, village NPCs, merc walk sheets, and environment pieces under **`Assets/Sprites/`** (Cursor-generated, cited in reference list). | Playtest "placeholder" feedback; art direction validated positively. |
| Combat portraits | `*_BattleReady.png` per merc/enemy for additive combat units. | Battle scene uses dedicated poses vs exploration walk sheets. |
| Import pipeline | Point filter / PPU on pixel sheets; **`MercenaryVisualApplier`** applies camp, follow, and combat visuals consistently. | Prevent blurry or wrong-scale merc art. |

---

## AI tools used (process)

- **Cursor / Composer-style agent** — code search, multi-file edits, scene YAML awareness (manual verification in Unity recommended).
- **Cursor image generation** — character and environment sprites (attributed in **`docs/reference-list.txt`**).
- **Local LLM (Ollama, `qwen3:8b`)** — runtime dialogue, forge commissions, encounter intros, mercenary chat; **not** gameplay authority (C# + fallbacks).
- **Pixabay** — licensed audio downloads; implementation wired via AI-assisted **`GameAudioController`** setup.

---

## How to extend this log

Add a row or subsection per milestone: **feature**, **files touched**, **playtest notes**, **model name used** (e.g. `qwen3:8b`).

**Primary scripts added or heavily touched since playtest:** `PlayerDeathController.cs`, `PlayerSessionStartRecorder.cs`, `PlayerStartLocation.cs`, `GameAudioController.cs`, `QuestLogUI.cs`, `QuestWaypointDirector.cs`, `CompanionTalkMenuController.cs`, `CompanionConversationUi.cs`, `MercenaryVisualApplier.cs`, `RiskyGroundEncounter2D.cs` (grace/cooldown), `PauseMenuController.cs`.
