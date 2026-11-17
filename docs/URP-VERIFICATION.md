# URP verification — 2026-11-17

Environment: Linux, Unity **6000.6.7f1**, URP **17.6.0**, Vulkan.
Approved plan and implementation details: [URP-FINALIZATION.md](URP-FINALIZATION.md).

## Passed

- Script/asset compile checks for Windows, macOS and Linux; valid saved Mirror scene IDs.
- URP build/Resources audit: **89 materials**, no known legacy/missing/erroneous shaders.
- Native Linux player build succeeded with **0 build errors**, about **631 MB**.
- Native `-verifyGraphics` exited **0**: one shared Base/Overlay stack, pistol single-shot latch/ammo,
  all shared effect bindings, AO, Base-supplied Off/FXAA/dithering, all quality levels at native scale,
  source-profile isolation, GPU exposure and rendered bloom-halo pixels.
- Native 640x360 resolution applied; confirmation persisted/reapplied; timeout rolled back; settings
  stayed inside the screen. The final image has readable arrows and a visible scrollbar.
- Real Forward+ pixel probe: six-light contribution **5.866x** one-light contribution. Approaching
  from 3 to 0.35 preserved **0.836426** measured brightness, without per-object light popping.
- Dissolve/outline render probe: **4096** visible pixels, **0** fully dissolved, **243** outline pixels.
- Separate native host and editor client: **2 players**, initialized local/remote weapons, correct
  local camera stack and Base FXAA. Physical trigger presses consumed one round on both peers.
- Settings blocked firing and Backspace disconnection. Escape closed settings and kept pause active;
  keyboard navigation stayed inside the modal. Original muzzle flash inspected with/without bloom,
  with no black rectangle. Candle hotspot correction inspected near/far.
- Existing metadata/GUIDs preserved; unrelated existing migration changes retained.

## Local artifacts

Ignored files in Logs include `graphics-verification.json`, `urp-light-probe.json`,
`urp-two-peer-final.json`, `urp-shader-probe.json`, `urp-validation.txt`, compile/build reports,
`graphics-player-final-ui.log`, `settings-effects-640.png`, `urp-settings-readable.png`,
`urp-candles-close.png`, `urp-muzzle-after.png`, `urp-muzzle-no-bloom.png` and `urp-native-host.png`.

Run the reusable pixel probes by passing the method bodies in `Tools/urp_shader_probe.csx` and
`Tools/urp_light_probe.csx` to MCP `execute_code` in Edit mode. Run player checks as documented in
[GRAPHICS_SETTINGS.md](GRAPHICS_SETTINGS.md). Add `-keepVerificationHost` to keep a passing test's
host alive on port 7799 for a second peer; a failed test still exits with status 1.

## Remaining QA

A complete multi-room multiplayer match, all weapon/revive transitions, Windows/macOS native
players/graphics APIs and performance on other hardware remain manual acceptance. Script checks
are not runtime platform checks. Source settings from `297345e` were used for comparison; no
pixel-identical historical rendering is claimed. Unverified imported demos/dependencies are retained.

Pre-existing issues observed: Rider plugin assembly validation, old imported model/NavMesh formats,
negative-scale colliders, and the compile harness's known TMP editor-package warning.
