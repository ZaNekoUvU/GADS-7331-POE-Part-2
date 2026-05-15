# GADS 7331 POE Part 2 — *Back To The Forge*

Unity 6 **2D RPG** prototype: exploration, forge economy, mercenary companions, additive combat, and **optional local LLM** dialogue via **Ollama**.

---

## Documentation

| Document | Description |
|----------|-------------|
| [**High concept**](docs/high-concept.txt) | Ideation, LLM role, why local models fit the project |
| [**Setup guide**](docs/setup.md) | Unity + Ollama install, models, troubleshooting |
| [**Refinements log**](docs/refinements-changes.md) | Scope changes & AI-assisted decisions |
| **This README** | Overview, install, dependencies, credits |

---

## Overview

- **Exploration** — village/wilds navigation, inventory (**hold Tab**), economy tied to **`BlacksmithMaster`**.
- **Combat** — **`CombatAdditiveCoordinator`** loads **Combat Scene** additively and isolates exploration renderers/cameras so only combat content draws.
- **Companions** — **`MercenaryCampSpawner`** + **`HireableCompanionOffer`** assets; hire flow **`CompanionRecruiter`**.
- **LLM (optional)** — **`OllamaDialogueService`** powers NPC lines, forge quest offers (JSON), random **bandit** encounter intros, and mercenary **opening** lines using ScriptableObject personas.

Without Ollama running, features fall back to **scripted dialogue** where implemented.

---

## Installation

1. **Clone** this repository.
2. Install **Unity 6** editor version matching **`Back To The Forge/ProjectSettings/ProjectVersion.txt`** (currently **6000.4.6f1**).
3. Open the **`Back To The Forge`** folder in Unity Hub.
4. Add scenes to **Build Settings** if prompted (Main Menu, Exploration, Combat).
5. **Optional — LLM:** Install [Ollama](https://ollama.com), pull a model (e.g. `qwen3:8b`), and align the model name on **`OllamaDialogueService`** in your scene or settings asset.

Full steps: **[docs/setup.md](docs/setup.md)**.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| **Unity 6** | Editor & runtime |
| **Unity Input System** | Player / UI actions |
| **UI Toolkit** | Menus, dialogue, HUD panels (`FfStyleMenuUi`) |
| **Ollama** (local, optional) | HTTP `/api/chat` from `OllamaDialogueService` |
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
