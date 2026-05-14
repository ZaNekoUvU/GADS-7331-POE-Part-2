# Village scene — pixel images + Unity scene (single deliverable)

## Emphasis (per your latest note)

- **Images**: Produce real **pixel-art PNGs** in the repo (houses, forge, trees, ground/path tiles as needed), generated with explicit pixel-art constraints (limited palette, crisp edges, no photorealism).
- **Scene**: **Fully set up** `VillageScene.unity` — camera, URP 2D lighting, hierarchy, sprite references to those PNGs, sorting orders, and **empty anchor transforms** for you to parent existing gameplay prefabs.

There is **no** “blockout only first” phase: placeholders are optional only if an asset fails once; the intent is **final-looking pixel sprites + scene** in one implementation pass.

## Technical context

- **Unity 6** (`6000.4.6f1`), **URP 2D** ([GraphicsSettings](Back%20To%20The%20Forge/ProjectSettings/GraphicsSettings.asset)).
- **New scene** (does not replace [Exploration Scene](Back%20To%20The%20Forge/Assets/Scenes/Exploration%20Scene.unity)) — e.g. `Assets/Scenes/VillageScene.unity`.
- **Blacksmith**: **visual-only** building art in-scene; **you** place/link your existing `BlacksmithMaster` (or other) prefab at `Anchor_BlacksmithGameplay` (or equivalent).

## Asset plan (PNG + `.meta`)

| Asset group | Examples | Notes |
|-------------|----------|--------|
| Ground | `village_ground.png` — tiling-friendly grass/dirt | May use one large texture or a few tiles; import **Point** filter, chosen **PPU** (16 or 32) |
| Forest | `tree_A.png`, `tree_B.png` (2 variants) | Repeated instances around clearing edge |
| Village | `house_small.png` × reuse for 6 placements (or 2–3 variants if time) | Small silhouettes, readable at game scale |
| Forge | `forge_building.png` | Visually distinct from houses (sign, chimney, anvil silhouette, darker wood, etc.) |
| Optional | `path_tile.png`, fence strip | If composition needs a “main path” |

Each texture gets a Unity `.meta` with **Filter Mode: Point**, **Compression: None** (or Low Crunch only if file size demands), consistent **sprite mode** (Single vs Multiple for tiles).

## Scene plan (`VillageScene.unity`)

- **Main Camera**: Orthographic, tag `MainCamera`, size tuned to village footprint.
- **Global Light 2D**: Neutral; optional very subtle warm tint for “village” mood.
- **Hierarchy** (illustrative):
  - `Environment` — ground sprite(s) / optional Tilemap
  - `Forest` — tree instances in a ring / two crescents around village
  - `VillageBuildings` — six houses + one forge (sprite children, ordered draw)
  - `Anchors` — empty transforms: `Anchor_PlayerSpawn`, `Anchor_BlacksmithGameplay`, optional `Anchor_Encounter_*`
- **Sorting**: Ground < trees trunks < buildings < roof overlays if layered; document `sortingOrder` conventions in scene notes or README snippet if needed.

## Build Settings

- Add `VillageScene` to [EditorBuildSettings](Back%20To%20The%20Forge/ProjectSettings/EditorBuildSettings.asset) **disabled** until you choose it as a startup or additive scene.

## Image generation approach (implementation)

- Use **image generation** with tight prompts: top-down or 3/4 **low-res pixel** (e.g. 64×64 / 128×128 canvas), **orthogonal**, **limited palette**, **no soft gradients**, **no text**.
- Post-process if needed: ensure dimensions are power-of-two where helpful; keep **readable silhouettes** at target **PPU** in-scene.

## Risks

- Generated art may need **one revision pass** if scale reads wrong in Unity; camera size + sprite scale can fix most issues without regenerating everything.

## Todos (execution order)

1. **pixel-pngs** — Generate and save PNGs + `.meta` under e.g. `Assets/Art/Village/Pixel/`.
2. **sprites-import** — Configure import settings (Point, PPU, pivots for buildings).
3. **village-scene** — Author `VillageScene.unity` (camera, light, layers, all sprite placements, anchors).
4. **build-settings** — Register scene (disabled).
5. **polish-pass** — Adjust orthographic size, positions, sorting; swap any weak sprite after quick in-editor check (if you report scale issues).
