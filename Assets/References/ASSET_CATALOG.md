# IUT Saint-Die Pinball - Asset Catalog

The reference is a detailed vertical arcade pinball table. The project will use a hybrid asset strategy: specialized pinball parts where they save real modeling time, CC0 materials for the cabinet and winter environment, and the existing IUT model for the school identity.

## Recommended First Imports

| Role | Asset | Source | License / cost | Decision |
| --- | --- | --- | --- | --- |
| Pinball parts | [Pinball Table Elements](https://assetstore.unity.com/packages/package/196794) | Unity Asset Store | Paid, listed around $19.98 at research time | Best candidate for ramps, posts and table mechanisms if meshes match the reference |
| Pinball machine shell | [Pinball Machine](https://assetstore.unity.com/packages/package/78619) | Unity Asset Store | Paid, listed around $11.99 at research time | Useful only if its proportions and license fit; do not import blindly |
| Pinball prototype | [Simple Pinball](https://assetstore.unity.com/packages/package/80053) | Unity Asset Store | Free | Possible gameplay reference, not a visual source |
| Arcade environment props | [Mini Arcade](https://kenney.nl/assets/mini-arcade) | Kenney | CC0 | Background arcade props and scale references |
| Industrial/campus props | [City Kit (Industrial)](https://kenney.nl/assets/city-kit-industrial) | Kenney | CC0 | Exterior IUT surroundings and background buildings |
| Trees and forest props | [Mini Forest](https://kenney.nl/assets/mini-forest) | Kenney | CC0 | Snowy campus background and forest silhouettes |
| Snow material | [Snow015](https://ambientcg.com/a/Snow015) | ambientCG | CC0 | Playfield snow accents and exterior ground; 1K PBR download is enough for a student project |
| Wood material | [Wood095](https://ambientcg.com/a/Wood095) | ambientCG | CC0 | Cabinet and side rails |
| Metal material | [Metal063](https://ambientcg.com/a/Metal063) | ambientCG | CC0 | Chrome rails, screws and mechanical parts |
| Ice material | [Ice003](https://ambientcg.com/a/Ice003) | ambientCG | CC0 | Frozen glass-like decorative pieces and snow crystals |
| HDRI / lighting | [Poly Haven HDRIs](https://polyhaven.com/hdris) | Poly Haven | CC0 | Winter lighting reference and reflections |
| PBR library | [Poly Haven Textures](https://polyhaven.com/textures) | Poly Haven | CC0 | Alternative wood, metal and fabric materials |
| IUT identity | Existing [IUT.obj](../sprites/IUT.obj) | Project asset | Project-provided | Central campus building/signage element |

## Asset Store Search Results

The official Asset Store search returned these relevant candidates:

- [Pinball Creator](https://assetstore.unity.com/packages/package/74772): complete pinball creation template, paid.
- [ULTRA PINBALL](https://assetstore.unity.com/packages/package/349862): complete pinball template, paid.
- [Pinball Asset](https://assetstore.unity.com/packages/package/141633): pinball-oriented template/assets, paid.
- [Vintage Pinball Machine](https://assetstore.unity.com/packages/package/31521): audio, paid.
- [Arcade Machines Pack 01](https://assetstore.unity.com/packages/package/73020): free arcade machine props.
- [MK Glow](https://assetstore.unity.com/packages/package/90204): paid glow/post-processing tool; not required because URP can use emissive materials and bloom.
- [LED Studio](https://assetstore.unity.com/packages/package/379636): paid signage/VFX tool; optional only if the board needs animated LED text.

The search did not return a single free, professional, Unity 6-ready pinball table that should replace the scene wholesale. We should avoid mixing a full template with a manually built scene because that would recreate the original problem.

## License Notes

- Poly Haven assets are CC0: commercial and educational use is allowed without attribution.
- ambientCG assets are CC0: raw files may be included in a game project.
- Kenney assets are CC0: attribution is optional.
- Unity Asset Store packages remain subject to the Unity Asset Store EULA. The project should record the package name and purchase/import date if one is selected.

## Proposed Import Order

1. Keep the current reference image and IUT model.
2. Import one 1K wood, metal and snow material set only.
3. Import either `Pinball Table Elements` or model the first mechanical parts ourselves; do not import multiple full pinball templates.
4. Add Kenney arcade/forest props only after the table proportions are approved.
5. Build the first scene milestone: cabinet, inclined playfield, rails, glass, flippers and plunger.

## Environment (implemented)

The surrounding world is built by `Assets/Editor/BuildArcadeEnvironment.cs`
(menu `Flipper > Rebuild Arcade Environment`, also called at the end of `Rebuild Table Layout`).
Everything lives under an `Environment` root so it can be rebuilt on its own. It is **collider-free
on purpose** — it is dressing, and the table's physics tuning must not be disturbed by a stray
collider on a background cabinet.

| Catalog role | Asset | Where it lands |
| --- | --- | --- |
| Arcade environment props | Kenney Mini Arcade | `Environment/ArcadeFloor` (24 tiles), `ArcadeWalls` (31 panels), `ArcadeProps` (13 cabinets, prize stand, 2 characters) |
| Industrial/campus props | Kenney City Kit (Industrial) | `Environment/CampusExterior` ring of 10 buildings plus water tower, windmill, chimneys, containers, solar panels |
| Trees and forest props | Kenney Mini Forest | 26 trees in a 98-150 unit ring, plus rocks and ground patches |
| HDRI / lighting | Poly Haven `snowy_park_01_1k.hdr` | `Skybox/Panoramic` skybox, sky reflection probe, ambient 0.55 |
| Snow material | ambientCG Snow015 | narrow playfield accents only (its albedo is snow-over-grass) and a flat cold white for the exterior ground |
| Wood / Metal / Ice | ambientCG Wood095 / Metal063 / Ice003 | cabinet and rails / chrome parts / frozen glass inserts |
| IUT identity | `Assets/sprites/IUT.obj` | `Backbox_IUT` diorama on the backbox ledge |

### Scales and framing

Kenney authors each pack at "1 unit = 1 tile", which is far too large next to the table. The build
script uses `EnvScale = 6.5` for the arcade pack, `CityScale = 30` for the industrial pack and
`ForestScale = 36` for the forest. These were chosen against the game camera's actual frustum
(`CamLift` / `CamBack` / `CamPitch` / `CamFov` in `BuildPinballTable.cs`), not against real-world
measurements: at 40 degrees and 54 degrees pitch the camera sees roughly `x` in `[-8.2, 9.3]`, so the
bay's left wall face sits on the left frame edge and its floor fills the rest — the same composition
as the reference image.

Two settings elsewhere in the scene had to be corrected for the environment to read at all:

- **Fog.** The scene shipped with an exponential-squared fog at a near-black navy
  `(0.05, 0.10, 0.14)`, which swallowed everything beyond ~50 units and turned the campus into a dark
  smear. It is now a light winter haze `(0.80, 0.86, 0.94)` at density `0.0022`.
- **HUD.** The score/ball boxes were a fixed 420 px wide, so they overlapped in the middle of the
  637x471 game view. They now stretch over half the screen each with a 46 px inset.

### Kenney GLB caveat

Kenney's GLB export references **`Textures/colormap.png` as an external file**. Copying only the
`.glb` files leaves every material without a texture and the import reports failures. Each pack needs
its atlas next to the meshes:

```
Assets/Models/Kenney/Arcade/Textures/colormap.png
Assets/Models/Kenney/CityKit/Textures/colormap.png
Assets/Models/Kenney/Forest/Textures/colormap.png
```

After adding the atlas, force a reimport (`AssetDatabase.ImportAsset` with `ForceUpdate`); a plain
`Refresh` keeps the cached failure.

## Local Pipeline (implemented)

The mechanical parts are generated locally rather than bought, using a three-stage pipeline that
runs entirely on the RTX 4070. See the project skill `.github/skills/local-asset-pipeline/SKILL.md`
and `Tools/make_part.py`.

| Stage | Tool | Output |
| --- | --- | --- |
| Image | Krea 2 Turbo (GGUF Q5_K_M) in ComfyUI on port 8189 | 1024x1024 product render |
| Mesh | Hunyuan3D 2 Mini in Modly, API on port 8765 | geometry-only GLB |
| Cleanup | `Tools/decimate_glb.py` (pymeshlab quadric collapse) | ~8,000 tris per part |

Parts generated so far, all in `Assets/Models/Parts/`: bumper, slingshot, drop target, flipper bat,
plunger knob, ramp segment, lane guide, post rubber. Together they are 15 scene instances at
120,000 triangles.

No texture is generated on purpose: AI meshes carry no usable UVs, so the parts use the project's
solid PBR materials (`BumperRed_Mat`, `Metal_Mat`, `FlipperOrange_Mat`, `TargetYellow_Mat`,
`BallChrome_Mat`) and existing CC0 wood/metal maps.

This covers the "model the first mechanical parts ourselves" branch of step 3 above; the paid
`Pinball Table Elements` package is no longer required for ramps, posts and table mechanisms.