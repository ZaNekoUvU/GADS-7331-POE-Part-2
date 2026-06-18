# Project Context — Back To The Forge

**Course:** GADS 7331 POE Part 2  
**Engine:** Unity 6 (2D RPG prototype)  
**Repo:** [GADS-7331-POE-Part-2](https://github.com/ZaNekoUvU/GADS-7331-POE-Part-2.git)  
**Unity project folder:** `Back To The Forge/`

This document is the single entry point for project context: what the game is, how it is built, what is working, and what playtesters reported. Use it before interpreting feedback, planning changes, or onboarding collaborators / AI tools.

---

## 1. What this game is

**Back To The Forge** is a retro-styled 2D RPG about exploration, forge economy, risky overworld travel, mercenary companions, and turn-based combat.

**Core fantasy:** grounded travel and coin — hire muscle, talk to locals, mine and sell ore, commission work from a blacksmith, and survive random bandit encounters in the wilds.

**Pillars:**
- Exploration and village/wilds navigation
- Economy tied to mining, selling, and blacksmith commissions
- Additive turn-based combat (exploration scene stays loaded underneath)
- Optional **local LLM** dialogue via **Ollama** for flavor text and structured quest lines
- Scripted fallbacks everywhere so the game runs without Ollama

---

## 2. Tech stack

| Layer | Choice |
|-------|--------|
| Engine | Unity 6 (`6000.4.6f1` — see `ProjectSettings/ProjectVersion.txt`) |
| Input | Unity Input System |
| UI | UI Toolkit (`FfStyleMenuUi`, dialogue panels, HUD) |
| Text | TextMeshPro (legacy references in some scenes) |
| LLM | Ollama on `http://127.0.0.1:11434`, default model `qwen3:8b` |
| AI dev tools | Cursor IDE agents used for implementation support |

---

## 3. Key systems & architecture

### Scenes
- **Main Menu** — entry point
- **Exploration** — village, wilds, NPCs, economy, encounters
- **Combat** — loaded **additively** via `CombatAdditiveCoordinator`

### Exploration
- **`PlayerMovement2D`** — player locomotion
- **`Inventory` / `InventoryPanelToggle`** — inventory (hold **Tab**)
- **`GoldDisplayUI`** — currency HUD
- **`BlacksmithMaster`**, **`ForgeQuestManager`**, **`BlacksmithQuestGiver`** — forge economy and commissions
- **`PlayerMiningController`**, **`IronVein`** — mining loop
- **`RiskyGroundEncounter2D`** — random encounter triggers on risky ground
- **`MercenaryCampSpawner`**, **`CompanionRecruiter`**, **`HiredCompanionManager`** — hire and manage companions
- **`ScenePortalTrigger2D`**, **`SceneTransitionStore`** — scene transitions and return placement

### Combat
- **`CombatStarter`** — starts fights; random encounters can use an LLM intro line first
- **`CombatAdditiveCoordinator`** — isolates combat rendering from exploration
- **`CombatTurnManager`**, **`CombatUnit`**, **`CombatBattleHud`** — turn flow, units, HUD
- **`ExplorationCombatParty`** — party state carried into combat

### LLM integration (`OllamaDialogueService`)
The LLM is a **creative writer only** — it never rolls dice, moves units, or mutates save-critical state.

| Feature | Purpose |
|---------|---------|
| NPC dialogue | Persona-driven spoken lines from `NpcDialogueProfile` |
| Forge commissions | Structured JSON (`materialName`, `requestLine`) parsed strictly |
| Random encounters | One terse narrator line before bandit fights |
| Mercenary hiring | Opening pitch in designer-authored voice |

All call sites have **scripted fallbacks** when Ollama is offline, busy, or returns bad output.

### UI / session
- **`SimpleRpgDialogueUI`** — RPG dialogue display
- **`ForgeQuestChoiceUI`** — quest choice blocking
- **`PauseMenuController`** — pause flow
- **`MainMenuController`**, **`GameplaySessionReset`** — menu and session reset

---

## 4. Documentation map

| Document | Path | Purpose |
|----------|------|---------|
| High concept | `docs/high-concept.txt` | Ideation, LLM role, design guardrails |
| Setup guide | `docs/setup.md` | Unity + Ollama install and troubleshooting |
| Ollama plan | `docs/ollama-plan.md` | Model choice, timing, prompts, risks |
| Refinements log | `docs/refinements-changes.md` | Scope changes and implementation decisions |
| Prompt archive | `docs/prompts-used.md` | Tested prompts and iteration notes |
| Feedback summary | `Back To The Forge/feedback-summary.md` | Structured playtest feedback (unbiased capture) |
| **This file** | `docs/context.md` | Project context entry point |

---

## 5. Playtest context (Farrell & Mia)

**Session purpose:** External playtest to validate clarity, pacing, combat, and first impressions before further iteration.

**Playtesters:** Farrell, Mia  
**Full structured record:** `Back To The Forge/feedback-summary.md`

### What went well
- **Art style** — both responded positively (liked / cute)
- **UI legibility** — Farrell noted UI was clear and readable even without glasses
- **Combat simplicity** — Farrell appreciated the simple combat loop
- **Concept** — Farrell said the game has a **cool and different concept** (relates to overall pitch, LLM-driven flavor, and narrative framing)

### Issues raised by both
| Theme | Detail |
|-------|--------|
| **Wayfinding** | No clear visual guidance on where to go; both asked for pathing (e.g. paths on the ground) |
| **Random encounters** | Frequency felt too high; source of frustration |
| **Controls & objectives** | Not self-explanatory; team had to explain during playtest |
| **Combat targeting** | Both were confused as to which enemy they were targeting |

### Issues raised by one playtester
| Playtester | Issue |
|------------|-------|
| Farrell | In-game UI appeared on the main menu (bug) |
| Mia | Placeholder assets break immersion |
| Mia | Flee button almost never worked |

### Team initial reactions (not action items)
- Positive validation on art direction and UI readability
- Concern about navigation, encounter pacing, onboarding, and combat targeting clarity
- Functional concern around flee reliability and UI state leaking to main menu

---

## 6. Known issues & open priorities

Derived from playtest feedback and development notes. Not a committed roadmap.

1. **Onboarding** — controls and objectives need in-game explanation without verbal help
2. **Wayfinding** — add visual pathing or ground cues for navigation
3. **Encounter pacing** — reduce or rebalance random encounter frequency on risky ground
4. **Combat targeting** — make selected enemy obvious in the combat UI
5. **Flee button** — investigate reliability (`CombatBattleHud` / turn flow)
6. **Placeholder art** — replace assets that break immersion
7. **Main menu UI bug** — in-game HUD appearing on main menu (likely session/UI reset issue)
8. **LLM visibility** — concept praised but players may not distinguish AI dialogue from scripted lines without clearer framing

---

## 7. Code layout (quick reference)

```
Back To The Forge/
├── Assets/
│   ├── Scenes/              # Main Menu, Exploration, Combat
│   └── Scripts/
│       ├── Combat/          # Turn combat, units, HUD
│       ├── Companions/      # Hire flow, camp spawner, roster
│       ├── Dialogue/        # Ollama service, NPC profiles
│       ├── Inventory/       # Items, market pricing
│       ├── MainMenu/        # Menu bootstrap and controller
│       ├── Quest/           # Forge quests, mineral pickups
│       ├── SceneTransition/ # Portals, return placement
│       └── UI/              # Shared menu styling
├── feedback-summary.md      # Playtest feedback capture
└── ProjectSettings/
```

**Primary LLM entry point:** `Assets/Scripts/Dialogue/OllamaDialogueService.cs`  
**Random encounter hook:** `Assets/Scripts/RiskyGroundEncounter2D.cs` → `CombatStarter.cs`  
**Additive combat:** `Assets/Scripts/CombatAdditiveCoordinator.cs`

---

## 8. Running the project

1. Open `Back To The Forge/` in Unity Hub (Unity 6 matching project version).
2. Add scenes to Build Settings if needed: Main Menu, Exploration, Combat.
3. For AI dialogue features:
   - Install [Ollama](https://ollama.com)
   - Keep Ollama running in the background
   - Run `ollama pull qwen3:8b`
4. Play from Main Menu. If Ollama is unavailable, fallbacks still allow core gameplay.

See `docs/setup.md` for full setup and troubleshooting.

---

## 9. Design guardrails (do not break)

- Gameplay authority stays in **C#** — LLM output is display text or parsed DTOs only
- **Fallback lines** must remain wherever Ollama is called
- Prompts enforce **short in-character speech** — no meta commentary
- Combat scene must stay **visually isolated** from exploration when loaded additively
- Pin one Ollama model tag for reproducible coursework demos

---

## 10. How to use this file

- **Before planning changes:** read §5–6 for playtest signal, then cross-check `feedback-summary.md`
- **Before touching LLM features:** read `docs/high-concept.txt` and `docs/ollama-plan.md`
- **Before combat/UI work:** check refinements log for prior fixes (`docs/refinements-changes.md`)
- **For AI assistants:** treat this file + `feedback-summary.md` as current project state; prefer minimal diffs and existing conventions in `Assets/Scripts/`
