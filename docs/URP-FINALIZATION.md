# URP finalization

Accepted implementation plan, 2026-11-17. Reference: last Built-in state at `297345e`.
Target: Unity 6000.6.7f1, URP 17.6. Keep changes local because other development is ongoing.

## Findings

1. Graphics preferences are connected to PlayerPrefs and runtime pipeline/profile copies, but the
   first-person weapon has a separate HDR target, post-processing profile and UI compositor.
   This adds unnecessary settings and alpha-compositing failure modes. TAA cannot use camera stacking.
2. Settings use IMGUI while the main/pause menus use uGUI and existing artwork/ fonts. Settings and
   PauseGame independently handle Escape, so closing settings can also resume the game.
3. The pistol prefab defaults to single fire; its fire/burst logic is unchanged from the reference.
   Triple and automatic modes can be selected with B. Measure actual ammo and shot calls before
   changing weapon logic: the reported three flashes are not yet confirmed as three bullets.
4. ParticleFirework uses an RGB flare texture on black, intended for additive blending. Its alpha
   accumulates in the separate weapon target and the UI compositor then masks the world with it.
   Repair the render path; retain the original texture, particle effect and additive material.
5. Gameplay ambient changed from Skybox to Trilight, lighting data was disconnected, and Forward
   limits additional lights to 1/2/3/4 on Medium through Ultra. New eye adaptation samples a cropped
   world view twice per second. Light selection, ambient and exposure need separate comparisons.
6. Legacy PPSv1 components/profiles still have serialized references, including imported examples.
   Do not delete uncertain dependencies. Resources.Load and Shader.Find also count as usage.

## Implementation order

1. Reproduce and measure shots; preserve the initial working-tree state and reference images.
2. Use the native Base-world/Overlay-weapon stack and one shared effects profile. Remove the weapon
   texture/compositor, FSR/sharpening, Original Light/Ultra presets and separate weapon controls.
   Offer Off/FXAA; migrate saved TAA to FXAA and ignore retired preference keys.
3. Keep bloom, vignette, automatic/manual exposure, DOF, motion blur, Neutral tonemapping,
   chromatic aberration and dithering. Offer shared AO, default off. Extra effects default off;
   bloom/vignette/adaptation remain on. Exclude minimap and UI from world post-processing.
4. Verify muzzle flash and fix only a demonstrated pistol fault; retain weapon modes, networking,
   reload and swap behavior. Distinguish shot events/ammo use from particle emissions.
5. Use Forward+ for desktop quality levels; restore Skybox ambient and validate reflections/probes.
   Compare fixed-exposure lighting first, then adaptation and bloom. Never compensate missing
   ambient by increasing candle intensity. Enlighten is unsupported. Bake only stable environment
   lighting if comparison demonstrates missing indirect light; exclude switchable capture candles.
6. Replace the settings view with a reusable uGUI prefab using existing Nevermore/menu artwork.
   Keep gameplay/display/effects tabs, scrolling, keyboard navigation and 15-second display rollback.
   Settings owns Escape while open; closing it must leave pause active and block input underneath.
7. Remove proven obsolete references/assets; update graphics/build verification documentation.

## Acceptance and regression checks

- Every option affects the correct runtime parameter and visible image; survives quality changes,
  scene transitions, respawn and restart without mutating source assets.
- A single-mode pistol click consumes one round and emits one shot event on host and client.
  Holding does not burst. Check B-selected modes, rifle, reload and swap separately.
- Original muzzle flash has no black rectangle on light/dark backgrounds, with bloom on/off;
  smoke and impacts remain correct on local and remote weapons.
- No candle light pops while approaching/turning; interiors match the reference atmosphere without
  clipped hotspots. Captured/extinguished candles leave no baked direct-light ghost.
- Settings match other menus, work with mouse/keyboard at 640x360, 1280x800 and 1920x1080, and
  correctly confirm/revert resolutions on timeout, close, Escape, repeated trials and restart.
- Test MainMenu, LobbyTests and ExtremeHomeMakeover across all quality levels. Run native player
  plus editor host/client, compile checks, URP asset audit and `Tools/assign_scene_ids.py --check`.
- Use image comparisons, Frame/Rendering Debugger and GPU profiling; component flags alone cannot
  establish visual correctness. Record untested platforms and any outstanding visual acceptance.

## Cleanup gates and risks

Remove WeaponEffectsCamera, WeaponEffects/Weapon_Renderer, WeaponComposite and their metadata only
after replacing their callers and serialized references. Remove NightmareVolume and its scene object
after establishing a single profile owner. Retire one-off converters once their jobs are persisted.
Delete PPSv1/profile assets only after all consumers are verified; keep uncertain demo dependencies.
Keep used dissolve, outline, particle and skybox shaders.

Watch Forward+ GPU cost, shared DOF/motion blur on weapons, PPSv1/URP algorithm differences and
switchable-light bake artifacts. Preserve .meta files and Mirror IDs. Do not mass-reserialize scenes,
reset existing changes, or commit unrelated work. Implementation/test results belong below.

## Implementation results

Implemented and verified locally on Linux/Vulkan (2026-11-17):

- Shared Base/Overlay rendering, one profile, Off/FXAA, shared AO, native render scale, and retired
  FSR/weapon/preset code. AA and dithering are supplied by the Base camera as required by URP;
  post-processing runs once on the final 3D Overlay.
- uGUI prefab with existing artwork/Nevermore, regular UI font for values/arrows, visible scrollbar,
  scoped arrow/Tab navigation, display confirmation and modal input protection. Escape leaves pause
  active; Backspace cannot disconnect while editing the name. The network IMGUI panel is hidden
  while Settings is open. The prefab builder uses a temporary scene, preserving the user's scene.
- Fresh pistol: physical Input Manager click uses one round; a held single-mode trigger does not
  repeat. Two separate processes (native host/editor client) both used one round per press.
  Firing/burst logic and B-selected modes were retained. Paused original muzzle particles were
  inspected with/without bloom: soft additive flare, no black rectangle, original assets preserved.
- Forward+ on all qualities, Skybox ambient/environment refresh, full-view exposure metering,
  CaptureArea candle intensity 0.032/range 1. Candle correction is scene-specific calibration,
  not a universal conversion factor. No bake or permanent light contribution was added.
- Removed old gameplay PPSv1 components/profiles, duplicate NightmareVolume, isolated weapon
  renderer/profile/compositor, retired conversion scripts and the unused weapon-effects layer.
  Retained PPSv1 code/resources for verified demo consumers, used custom shaders and uncertain assets.

Acceptance evidence:

- Native Linux build: succeeded with zero build errors. Latest runtime test exited 0; all effect
  bindings, every quality level, native 640x360 bounds, saved-resolution reapply and timeout rollback
  passed. GPU exposure measured about -1.45 EV for white and +0.73 EV for black geometry. Bloom
  surrounding-pixel sum changed from 0 to 774.863. See `Logs/graphics-verification.json`.
- Actual six-light pixel probe: ambient 0.472656, one light 0.534668, six lights 0.836426; incremental
  contribution ratio 5.866. Moving the camera from distance 3 to 0.35 preserved the measured value
  0.836426. See `Tools/urp_light_probe.csx` and `Logs/urp-light-probe.json`.
- Dissolve/outline probe: 4096 visible pixels, 0 fully dissolved, 243 outline pixels; passed.
- Two peers connected, both weapons initialized; editor client had one Overlay and Base FXAA.
  Editing Backspace and pressing fire inside Settings preserved connection/ammo; Escape closed only
  Settings. See `Logs/urp-two-peer-final.json` and native/client screenshots.
- Desktop script/asset checks passed WIN/OSX/LINUX; saved Mirror scene IDs are valid. URP audit:
  89 build/Resources materials, zero errors. Existing metadata hashes/GUIDs were preserved. MainMenu,
  LobbyTests, ProjectSettings and QualitySettings files are unchanged from the start of this task.

Screenshots and reports are ignored local artifacts in Logs, including `settings-effects-640.png`,
`urp-settings-readable.png`, `urp-candles-close.png`, `urp-muzzle-after.png`, `urp-muzzle-no-bloom.png`
and `urp-native-host.png`. The unchanged source scene files and original hashes were checked before
finishing; unrelated pre-existing migration work was retained. The prefab builder was also tested
inside a clean, unsaved scene: it preserved cleanliness, the active scene and the user scene dirty flag.

Remaining manual acceptance: Windows/macOS runtime and graphics APIs, a complete multi-room
multiplayer match (including every weapon/revive transition), exact artistic comparison with an
external reference build, and GPU performance across target hardware. The checked-in pre-URP scene
settings served as the reference; there is no claim of a pixel-identical historical rendering.
Known pre-existing warnings include the Rider editor plugin, old imported model/NavMesh formats and
negative-scale colliders; the compile harness also reports its known TMP editor-package limitation.
