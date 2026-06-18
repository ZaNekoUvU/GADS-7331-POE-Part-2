# GADS 7331 POE Part 2 — *Back To The Forge*

Unity 6 **2D RPG** prototype: exploration, forge economy, mercenary companions, additive combat, and **optional local LLM** dialogue via **Ollama**.

---

## Documentation

| Document | Description |
|----------|-------------|
| [**Project context**](docs/context.md) | Overview, architecture, playtest state, known priorities |
| [**High concept**](docs/high-concept.txt) | Ideation, LLM role, why local models fit the project |
| [**Setup guide**](docs/setup.md) | Unity + Ollama install, models, troubleshooting |
| [**Refinements log**](docs/refinements-changes.md) | Scope changes & AI-assisted decisions |
| [**Ollama plan**](docs/ollama-plan.md) | Model choice, timing, data flow, prompts, risks |
| [**Prompt archive**](docs/prompts-used.md) | Tested prompts, examples, iteration notes |
| **This README** | Overview, install, dependencies, credits |

---

## Overview

- **Exploration** — village/wilds navigation, inventory (**hold Tab**), economy tied to **`BlacksmithMaster`**.
- **Combat** — **`CombatAdditiveCoordinator`** loads **Combat Scene** additively and isolates exploration renderers/cameras so only combat content draws.
- **Companions** — **`MercenaryCampSpawner`** + **`HireableCompanionOffer`** assets; hire flow **`CompanionRecruiter`**.
- **LLM features** — **`OllamaDialogueService`** talks to **Ollama** on your PC (`http://127.0.0.1:11434`). NPC dialogue, forge commissions (JSON), random **bandit** encounter intros, and mercenary **openings** use it when available.

If **Ollama is not installed**, **not running**, or the **model is missing**, the game **still runs** and falls back to **scripted dialogue** where implemented — but you will **not** get AI-generated lines until Ollama is set up correctly.

---

## Installation

1. **Clone** this repository.
2. Install **Unity 6** editor version matching **`Back To The Forge/ProjectSettings/ProjectVersion.txt`** (currently **6000.4.6f1**).
3. Open the **`Back To The Forge`** folder in Unity Hub.
4. Add scenes to **Build Settings** if prompted (Main Menu, Exploration, Combat).

### Ollama (required for AI dialogue features)

1. **Install Ollama** from **[https://ollama.com](https://ollama.com)** for your OS (Windows / macOS / Linux).
2. **Keep Ollama running in the background** while you play or develop — leave the app open (Windows system tray / macOS menu bar). If Ollama is closed, HTTP requests from Unity fail and the game uses fallback text.
3. **Pull the model this project is configured for:** **`qwen3:8b`**  
   In a terminal:
   ```bash
   ollama pull qwen3:8b
   ```
4. Confirm it appears under **`ollama list`** (the name must match **exactly**, including tag).
5. In Unity, the scene/component **`OllamaDialogueService`** (or an **`Ollama Dialogue Settings`** asset) should use **model `qwen3:8b`** and host **`http://127.0.0.1:11434`** unless you intentionally switch models — if you change the model string, pull that tag first.

More detail and troubleshooting: **[docs/setup.md](docs/setup.md)**.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| **Unity 6** | Editor & runtime |
| **Unity Input System** | Player / UI actions |
| **UI Toolkit** | Menus, dialogue, HUD panels (`FfStyleMenuUi`) |
| **Ollama** | Local LLM server — **install from [ollama.com](https://ollama.com)**; **keep running in background** during play. Reference model: **`qwen3:8b`** (`ollama pull qwen3:8b`). Used by `OllamaDialogueService` over `http://127.0.0.1:11434`. |
| **TextMeshPro** | Legacy UI text where still referenced in scenes |

---

## Credits

- **Course / team** — GADS 7331 POE Part 2 (course attribution per syllabus).
- **Engine & middleware** — Unity Technologies; Ollama project for local inference.
- **AI-assisted development** — Cursor IDE agents / chat used for implementation support, refactors, and documentation drafts; design authority remains with the project authors.

---

## AI tools used

- **Cursor** — multi-file edits, codebase navigation, debugging assistance.
- **Ollama / local LLMs** — in-game generative dialogue and encounter text (see **high concept** doc).

---

## License

*(Add your course or team license here if required.)*
