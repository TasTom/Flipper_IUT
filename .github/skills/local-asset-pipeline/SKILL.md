---
name: local-asset-pipeline
description: "Generate 3D game assets locally for Flipper_IUT: Krea 2 Turbo images via ComfyUI, then image-to-3D meshes via Modly/Hunyuan3D, then import into Unity."
---
# Local Asset Pipeline (image -> 3D -> Unity)

Fully local generation, no cloud API keys. GPU: RTX 4070 (12 GB) — Krea 2 Q5_K_M fits at 8.8 GB.

## One command

[make_part.py](./make_part.py) chains all stages:

```powershell
python Tools/make_part.py --name bumper --subject "a pinball bumper cap, round red mushroom ..."
python Tools/make_part.py --all                      # built-in pinball part set
python Tools/make_part.py --skip-image path/to.png   # reuse an existing render
python Tools/make_part.py --all --faces 12000        # looser triangle budget
```
Result lands in `Assets/Models/Parts/Krea_<name>.glb`, ready for glTFast.

Both servers must be up (the script checks and tells you which is missing).

## Triangle budget

Hunyuan3D mini at octree resolution 380 emits **0.2-1M triangles per prop** — 80x more than a
pinball table can afford (measured: 5.04M tris across 8 parts). [decimate_glb.py](./decimate_glb.py)
runs quadric edge collapse via pymeshlab and gets that to 64k total with no visible loss at game
scale:

| Part | before | after |
|---|---|---|
| bumper | 924,514 | 8,000 |
| slingshot | 751,760 | 8,000 |
| drop_target | 480,308 | 8,000 |
| flipper_bat | 593,114 | 8,000 |
| plunger_knob | 603,446 | 8,000 |
| ramp_segment | 193,322 | 8,000 |
| lane_guide | 910,560 | 8,000 |
| post_rubber | 582,172 | 8,000 |

pymeshlab and trimesh live only in Modly's venv, so run the script with
`~/Documents/Modly/dependencies/venv/Scripts/python.exe` (or let `make_part.py` shell out to it).
pymeshlab cannot write glTF, hence the PLY round-trip through trimesh.

## Stage 1 — Images with Krea 2 Turbo (ComfyUI)

Server: `http://127.0.0.1:8189` (the GGUF-capable standalone install, NOT Comfy Desktop which owns 8188 and lacks ComfyUI-GGUF).

Start:
```powershell
Set-Location "C:\Users\tomta\ComfyUI"
& "C:\Users\tomta\ComfyUI\.venv\Scripts\python.exe" main.py --port 8189 --listen 127.0.0.1 --preview-method none
```

Generate: [krea2_generate.py](./comfyui/krea2_generate.py)
```powershell
python Tools/comfyui/krea2_generate.py --name bumper --prompt "a pinball bumper cap, ..." --seed 777
python Tools/comfyui/krea2_generate.py --batch            # built-in pinball part set
```
Output: `Tools/comfyui/out/<name>.png`. ~35-70 s per 1024x1024 image (8 steps, er_sde).

Model files (already installed):
| Slot | File |
|---|---|
| `models/unet/` | `krea2_turbo-Q5_K_M.gguf` (8.26 GB) |
| `models/text_encoders/` | `qwen3vl_4b_fp8_scaled.safetensors` |
| `models/vae/` | `qwen_image_vae.safetensors` |

Workflow template: [krea2_workflow.json](./comfyui/krea2_workflow.json) — `UnetLoaderGGUF` -> `CLIPLoader(type=krea2)` -> `KSampler(cfg=1.0, steps=8)`.

Re-download weights: [fetch_krea2.py](./fetch_krea2.py) (`--check` to inspect, `--quant Q4_K_M|Q5_K_M|Q6_K`).

## Stage 2 — Mesh with Modly (Hunyuan3D 2 Mini)

Modly desktop must be running (API on `8765`). Launch: `%LOCALAPPDATA%\Programs\Modly\Modly.exe`.

**Normalize the image first — this is mandatory.** Hunyuan3D crashes Modly's FastAPI bridge
(silent exit, port 8765 dies, no stack trace in `modly.log`) when fed a render with soft contact
shadows or a near-white gradient background. [normalize_part_image.py](./normalize_part_image.py)
thresholds the background to pure white, closes highlight holes, crops to the subject and
re-centres it with an 8% margin:

```powershell
python Tools/normalize_part_image.py Tools/comfyui/out/bumper.png
# -> Tools/comfyui/out/normalized/bumper.png
```

Then convert:

```powershell
python Tools/modly-cli/agent.py generate --image Tools/comfyui/out/normalized/bumper.png `
  --output Tools/modly-output/krea_bumper.glb --no-texture --remesh quad
```
~60-90 s per mesh, 8-24 MB GLB. Check the result with `"status": "done"` in the JSON.

## Stage 3 — Import into Unity

Copy the GLB under `Assets/Models/Parts/`, then glTFast imports it on the next refresh.

Placing a part with UnityMCP, per target object:

1. `manage_gameobject(action=create, parent=<host>, prefab_path=Assets/Models/Parts/Krea_x.glb)`
2. reset the transform — the GLB root carries baked position/rotation/scale
   (`position 0,0,0`, `rotation 0,0,0`, then a scale matched to the placeholder it replaces)
3. assign a material — **trimesh writes no materials, so the decimated GLBs import with a null
   material and render magenta**. Use the existing URP set: `BumperRed_Mat`, `Metal_Mat`,
   `FlipperOrange_Mat`, `TargetYellow_Mat`, `BallChrome_Mat`.
4. hide the placeholder: disable the host's `MeshRenderer` only — never the GameObject, or the
   `ScoreTarget` / `SubjectTarget` / `DrainZone` scripts and colliders stop working.

## Prompting notes

- Wrap subjects in the product-render style (white background, centered, orthographic) — `STYLE` in the script.
- Flat/planar parts (flipper bat) need an explicit three-quarter view or the mesh comes out flat; use `--raw` with a hand-written prompt.
- Keep `--no-texture` for geometry-only smoke tests; textures need a separate paint extension.
- AI meshes carry no usable UVs, so solid PBR materials read better than generated textures.

## Constraints

- Comfy Desktop (port 8188) has no ComfyUI-GGUF — always target 8189 for Krea 2.
- Only one heavy job at a time. Krea 2 keeps ~8.8 GB resident after inference; free it with
  `POST /free {"unload_models":true,"free_memory":true}` on 8189 before starting Hunyuan3D,
  otherwise the converter crashes. Verify with `nvidia-smi --query-gpu=memory.used --format=csv`.
- Modly exits when idle and also dies on a bad input image; relaunch and wait ~25 s for the
  FastAPI bridge before calling the CLI.
- Replacing a GLB file resets the names of its prefab instances back to the file's root node name.
  Re-check names after a reimport.
- Swapping materials on renderers while URP **GPU Resident Drawer** is active floods the console with
  `A BatchDrawCommand was submitted with an invalid Batch, Mesh, or Material ID`. It is disabled on
  `Assets/Settings/PC_RPAsset.asset` (`m_GPUResidentDrawerMode = 0`); a 60-object table gains nothing
  from instanced drawing. Re-enable it only if the scene grows past a few thousand objects.
- Port 8188 belongs to Comfy Desktop, 8189 to the standalone install — do not confuse them.
