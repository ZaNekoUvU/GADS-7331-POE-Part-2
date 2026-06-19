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
- **`PlayerMovement2D`** — player locomotion; `IsPlayerCharacterCollider` excludes companion followers
- **`Inventory` / `InventoryPanelToggle`** — inventory (hold **Tab**); gold shown bottom-right of inventory panel
- **`PickupLogUI`** — bottom-right pickup feed (`[Pickup]` / `[Gather]` debug logs also go to Console via `Inventory.TryAdd`)
- **`BlacksmithMaster`**, **`ForgeQuestManager`**, **`BlacksmithQuestGiver`** — forge economy and commissions
- **`PlayerMiningController`**, **`IronVein`** — mining loop; gather prompt UI lives in `PlayerMiningController`
- **`QuestLogUI`**, **`QuestWaypointArrow`**, **`QuestWaypointDirector`** — dynamic objectives + floating waypoint arrow
- **`RiskyGroundEncounter2D`** — random encounter rolls on risky ground (10% per tick with cooldown + movement gate)
- **`MercenaryCampSpawner`**, **`CompanionRecruiter`**, **`HiredCompanionManager`** — hire and manage companions (max 3)
- **`CompanionTalkMenuController`** — press **C** to pick a hired mercenary and open `CompanionConversationUi`
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
- **`ForgeQuestChoiceUI`** — quest / NPC choice overlay (also used by mercenary picker)
- **`CompanionConversationUi`** — free-text Ollama chat with hired mercenaries (morale skills)
- **`PauseMenuController`** — pause flow; **Controls** screen lists keys via `GameControlsReference`
- **`MainMenuController`**, **`GameplaySessionReset`** — menu and session reset

### Art & character visuals
Custom **AI-generated character art** is imported in Unity and wired to exploration sprites / animators (replacing placeholder squares on key NPCs and the player).

| Location | Contents |
|----------|----------|
| `Assets/Sprites/` | Per-character folders (e.g. **Aelric**, **Tobin**, **Garron**, **Sterk**, **Brynja**, **Kaela**) with `removalai_preview` source sheets sliced into walk animations |
| `Assets/Sprites/Mercenaries/` | One folder per hireable merc (**Rook**, **Kaela**, **Mira**, **Brynja**, **Vex**, **Silas**, **Tomas**) with `*_Walk_Spritesheet.png` and `*_BattleReady.png` |
| Player | Walk sprites + animator on Player prefab / Exploration Scene instance |
| Blacksmith | `Blacksmith_Idle_Breathing_Spritesheet.png` and related idle art |

Import settings use **Point** filter where pixel art is intended. Some camp mercs may still use tinted placeholder sprites until each roster entry is assigned the new sheets on `CompanionRecruiter` / unit prefabs.

---

## 4. Documentation map

| Document | Path | Purpose |
|----------|------|---------|
| High concept | `docs/high-concept.txt` | Ideation, LLM role, design guardrails |
| Setup guide | `docs/setup.md` | Unity + Ollama install and troubleshooting |
| Ollama plan | `docs/ollama-plan.md` | Model choice, timing, prompts, risks |
| Refinements log | `docs/refinements-changes.md` | Scope changes and implementation decisions |
| Prompt archive | `docs/prompts-used.md` | Tested prompts and iteration notes |
| Cursor handoff | `docs/cursor-handoff.txt` | Dense AI/teammate handoff: recent features, gotchas, file map |
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

1. **Onboarding** — controls and objectives need in-game explanation without verbal help (partially addressed: pause **Controls** screen, quest log, gather prompt)
2. **Wayfinding** — quest waypoint arrow helps; ground pathing still requested by playtesters
3. **Encounter pacing** — risky-ground rolls now use 3s interval, 45s post-fight cooldown, and movement gate; zone collider in Exploration Scene is very large (~62× scale) — shrink in editor if fights still feel frequent
4. **Combat targeting** — red outline on selected enemy exists; clarity may still need polish
5. **Flee button** — investigate reliability (`CombatBattleHud` / turn flow)
6. **Art pass** — many characters now have generated sprites in Unity; finish assigning merc walk sheets to all camp followers / combat prefabs
7. **Main menu UI bug** — in-game HUD appearing on main menu (likely session/UI reset issue)
8. **LLM visibility** — concept praised but players may not distinguish AI dialogue from scripted lines without clearer framing
9. **Mercenary duplicate triggers** — fixed: companion colliders no longer fire quest pickups / risky-ground rolls; leader-only detection on `PlayerMovement2D`

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
│       ├── Quest/           # Forge quests, mineral pickups, quest log, waypoint
│       ├── SceneTransition/ # Portals, return placement
│       └── UI/              # Shared menu styling, pickup log, controls reference
│   ├── Sprites/             # Generated character art, merc sheets, environment
│   │   └── Mercenaries/     # Per-merc walk + battle-ready PNGs
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

## 10. Exploration controls (quick reference)

| Key | Action |
|-----|--------|
| WASD / arrows | Move |
| E | Interact — NPCs, dialogue advance, hold to gather |
| **C** | Talk to a hired mercenary (pick from party list) |
| Tab (hold) | Inventory + gold |
| Esc / P | Pause |

Hired mercenaries are **not** talked to with E in the field anymore — **C only**.

---

## 11. How to use this file

- **Before planning changes:** read §5–6 for playtest signal, then cross-check `feedback-summary.md`
- **Before touching LLM features:** read `docs/high-concept.txt` and `docs/ollama-plan.md`
- **Before combat/UI work:** check refinements log for prior fixes (`docs/refinements-changes.md`)
- **For AI assistants / teammates in Cursor:** read **`docs/cursor-handoff.txt`** first for the latest implementation notes and gotchas; then this file + `feedback-summary.md`. Prefer minimal diffs and existing conventions in `Assets/Scripts/`
