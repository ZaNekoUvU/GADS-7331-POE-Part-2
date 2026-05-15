# GADS 7331 POE Part 2

## AI gateway

The Unity project now calls a hosted AI gateway instead of talking directly to Ollama on the player's machine.

- Unity client config lives in `Back To The Forge/Assets/Settings/AiServerSettings.asset`
- The gateway project lives in `ai-server`
- The gateway exposes:
  - `POST /api/npc/line`
  - `POST /api/blacksmith/offer`
  - `POST /api/blacksmith/roleplay`

### Running the gateway locally

From `ai-server`:

```powershell
dotnet run
```

Optional environment variables:

- `OLLAMA_BASE_URL` - defaults to `http://127.0.0.1:11434`
- `OLLAMA_MODEL` - defaults to `qwen3:8b`
- `OLLAMA_TIMEOUT_SECONDS` - defaults to `60`
- `AI_SHARED_SECRET_HEADER` - defaults to `X-Game-Api-Key`
- `AI_SHARED_SECRET` - optional shared secret required by the gateway

Point `AiServerSettings.asset` at your deployed gateway URL for production builds.

