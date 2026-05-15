# Refinements & scope changes log

Continuous record of notable adjustments and **AI-assisted** implementation decisions during Back To The Forge / POE Part 2 development. (Companion chat sessions + Cursor-assisted edits.)

---

## Rendering & additive combat

| Date / phase | Change | Rationale |
|--------------|--------|-----------|
| Combat isolation | **`CombatAdditiveCoordinator`** walks loaded objects and disables **cameras, listeners, renderers, and `Canvas`** whose `gameObject.scene` is **not** the combat scene; restores on unload. | Exploration terrain/sprites stayed visible under additive combat; combat camera drew both scenes. |
| Scene tweaks | Combat background **Z** normalized; combat camera **occlusion culling** disabled where it hid the BG. | Background disappeared or sorted incorrectly. |
| API hygiene | Replaced obsolete **`FindObjectsByType(..., FindObjectsSortMode)`** with two-arg overload **`*FindObjectsInactive.Include)`** only. | Unity 6 deprecation warnings (CS0618). |

---

## Mercenary camp & spawning

| Phase | Change | Rationale |
|-------|--------|-----------|
| Spawn clearance | **`MercenaryCampSpawner`** uses **`Physics2D.OverlapCircle`** with **triggers included**, ignores **`CompanionRecruiter`** colliders, enforces **minimum separation** between hires. | Props like trees used large **trigger** volumes; ignoring triggers spawned inside canopy; merc triggers didn’t block each other. |
| Editor anchors | **`MercenarySpawnPoints`** child transforms under **`MercenaryCamp`**; roster slot index maps to child index; optional physics solver flag. | Designer-placed squares / clearing layout vs catalog-only coords. |
| Pose & DDOL | **`Instantiate(template, position, rotation, parent)`** + **`CompanionRecruiter.CommitSpawnPoseSnapshot()`** after configure. | Awake captured wrong home pose before transform assignment; return-home could snap wrong. |
| Data sync | **`MercenaryRosterCatalog`** world positions aligned with anchor layout as fallback. | Ensures spawns even if inspector references fail. |

---

## Random encounters & tone

| Phase | Change | Rationale |
|-------|--------|-----------|
| Encounter intro | **`CombatStarter.StartRandomEncounterWithLlmIntro`** — Ollama **one-line** staging; enemies framed as **bandits**; **`RiskyGroundEncounter2D`** calls this instead of immediate fight; guard flag prevents stacked intros. | Wanted narrative beat before combat without blocking non-random fights. |
| Narrator style | Prompts tightened for **short, BG3-like** narrator economy (clipped, low word count). | First pass was too descriptive. |

---

## Mercenary dialogue & LLM

| Phase | Change | Rationale |
|-------|--------|-----------|
| Persona pipeline | **`HireableCompanionOffer.PersonalityTrait`** + optional **`personalityVoice`**; **`PersonaForLlm`** selects richer text for prompts. | Use designer-written personalities for AI-spoken hire pitches. |
| Recruiter flow | **`CompanionRecruiter`** optional **`useOllamaMercenaryOpening`** — ShowAwaitingLine → **`RequestRoleplayLineCoroutine`** → advance; fallback to scripted **`openingLine`**. | Same UX pattern as other Ollama NPCs; offline-safe. |

---

## HUD / UI Toolkit

| Phase | Change | Rationale |
|-------|--------|-----------|
| Gold visibility | **`GoldDisplayUI`** UIDocument **sorting order** raised **above** Tab inventory (`4550` vs `4500`). | Full-screen inventory layer drew over gold; currency vanished while holding Tab. |

---

## AI tools used (process)

- **Cursor / Composer-style agent** — code search, multi-file edits, scene YAML awareness (manual verification in Unity recommended).
- **Local LLM (Ollama)** — runtime narrative generation; not used to author this markdown automatically in-repo.

---

## How to extend this log

Add a row or subsection per milestone: **feature**, **files touched**, **playtest notes**, **model name used** (e.g. `qwen3:8b`).
