# URP migration

The desktop game uses Unity 6000.6.7f1 and URP 17.6.0 with the Universal Forward+ renderer.
Pipeline assets and quality assignments are stored under `Assets/URPDefaultResources/`.
Builds validate persisted assets; they never run a material/scene converter.

## Rendering

- World Base + first-person WeaponCamera Overlay use one shared post-processing pass and native
  resolution. The minimap retains its own depth/stencil target; the HUD renders after 3D effects.
- All quality levels use Forward+ and per-pixel additional lights, avoiding candle-group light popping.
- Standard, legacy particles, vegetation, glass and brick materials use URP shaders. Enemy/particle
  dissolve, QuickOutline and volumetric explosions have URP passes. The six-sided starfield remains.
- Effects have common toggles and Off/FXAA; no FSR, TAA, legacy presets or separate weapon controls.
  Resolution changes have a 15-second confirmation/rollback. See [GRAPHICS_SETTINGS.md](GRAPHICS_SETTINGS.md).
- Gameplay Skybox ambient is restored; capture candle intensity is calibrated for URP attenuation.
  Switchable lights remain realtime. No unsupported Enlighten or baked candle light is introduced.

## Legacy dependencies

Gameplay prefabs/scenes no longer contain PPSv1 behaviours. The old Light/Ultra/Weapon profiles,
duplicate NightmareVolume, isolated weapon renderer/compositor and one-off conversion scripts are
removed after checking their references. `Assets/PostProcessing/` remains because imported office
and CustomMotionTexture demo scenes still reference it. Its shaders are stripped from URP builds.
Unverified demo shaders/assets are retained, as are used custom shaders and their metadata.

## Repeatable checks

- **The Nightmare → URP → Validate build assets** checks build/resource materials and used custom shaders.
- **The Nightmare → URP → Audit** writes `Logs/urp-audit.txt`.
- **The Nightmare → URP → Configure switchable effects** maintains the shared profile/Forward+ assets.
- **The Nightmare → UI → Rebuild settings prefab** regenerates the reusable uGUI shell.
- In Play mode, **The Nightmare → URP → Smoke test host** exercises a host through the first fight.
  Reports/screenshots are written to Logs; saved scenes are not modified by the smoke test.
- `-verifyGraphics` runs the opt-in runtime integration checks, including GPU/image assertions.
- `Tools/urp_shader_probe.csx` checks dissolve/outline; `Tools/urp_light_probe.csx` measures actual
  overlapping-light contributions and camera-distance stability through MCP.
- Compile checks, native builds and host/client gameplay remain separate checks; use [BUILDING.md](BUILDING.md).

The approved repair plan and results are in [URP-FINALIZATION.md](URP-FINALIZATION.md).
