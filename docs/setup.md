# Technical setup guide — Back To The Forge

This guide covers environment setup for **Unity 6** development and **local LLM (Ollama)** integration used by dialogue, forge quests, encounter intros, and mercenary greetings.

---

## 1. Prerequisites

| Requirement | Notes |
|-------------|--------|
| **Unity Editor** | **6000.4.6f1** (Unity 6). Match `ProjectSettings/ProjectVersion.txt` if upgrading. |
| **Git** | For cloning this repository. |
| **Disk space** | Unity project + Library cache + Ollama models (each model often **2–8 GB+**). |
| **Ollama** | Optional for LLM features; game falls back to scripted lines without it. |

---

## 2. Clone and open the Unity project

1. Clone the repo:
   ```bash
   git clone <your-repo-url>
   cd "GADS 7331 POE Part 2"
   ```
2. Open **`Back To The Forge`** as the Unity project folder (**File → Open Project**).
3. Allow Unity to import assets and regenerate **Library** (first open can take a while).

---

## 3. Build settings & scenes

Ensure **File → Build Settings** lists at least:

- Main Menu  
- **Exploration Scene**  
- Combat Scene  

Mercenary camp, risky-ground encounters, and economy hooks live primarily in **Exploration Scene**.

---

## 4. Install Ollama (local LLM server)

### Windows

1. Download and install from [https://ollama.com](https://ollama.com).
2. Confirm the CLI works:
   ```powershell
   ollama --version
   ```
3. Pull a model (example tags vary by machine):
   ```powershell
   ollama pull qwen3:8b
   ```
4. Keep **Ollama running** in the background while testing LLM features.

### macOS / Linux

Install per [Ollama docs](https://github.com/ollama/ollama), then `ollama pull <model>`.

---

## 5. Configure the game to talk to Ollama

The project uses **`OllamaDialogueService`** (`Assets/Scripts/Dialogue/OllamaDialogueService.cs`), typically bound to:

- **Host:** `http://127.0.0.1:11434`  
- **Model:** must match `ollama list` exactly (e.g. `qwen3:8b`)

Optional shared asset: **Ollama Dialogue Settings** (create via menu described on the ScriptableObject). Inline defaults on the scene component are fine for local dev.

**Quick verification**

1. Start Ollama.
2. Enter Play Mode in **Exploration Scene**.
3. Talk to an Ollama-enabled NPC or trigger features that call the service (mercenary with LLM opening enabled, risky-ground encounter intro).
4. If responses fail, enable logging on `OllamaDialogueService` and check Unity Console for HTTP errors.

---

## 6. Input / UI notes

- **Interact** — NPC hire flow and dialogue advance (see Input System asset bindings).
- **Hold Tab** — inventory overlay (UI Toolkit). Gold HUD uses a **higher panel sort order** so currency stays visible over the inventory panel.

---

## 7. System specs (rough guidance)

| Role | Minimum | Comfortable |
|------|---------|-------------|
| **Unity Editor** | CPU **4c/8t**, **16 GB RAM**, SSD | **32 GB RAM**, dedicated GPU for Editor |
| **Play Mode + Ollama** | **16 GB RAM** | **32 GB RAM** — model + Editor share RAM |
| **GPU** | Integrated OK for this 2D project | Discrete GPU helps Editor/UI |

Small models (e.g. **3B–8B** class) are appropriate for short RPG lines and JSON quest payloads.

---

## 8. Troubleshooting

| Issue | Things to check |
|-------|-----------------|
| No LLM replies | Ollama running? Correct **model** string? Firewall blocking localhost? |
| Fallback lines only | `IsBusy` double-requests; wait or disable overlapping LLM calls. |
| Wrong scene | LLM hooks expect exploration/economy objects (**BlacksmithMaster**, etc.). |
| Combat looks mixed | Additive combat uses **`CombatAdditiveCoordinator`** isolation; ensure Combat Scene loads correctly. |

---

## 9. Repository hygiene

- Do **not** commit **`Library/`**, **`Logs/`**, **`Temp/`**, or huge artifacts unless your course requires otherwise.
- Prefer documenting **exact model name + version** used for demos in `refinements-changes.md` or course submissions.
