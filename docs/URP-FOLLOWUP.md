# URP follow-up

Unity 6000.6.7f1 / URP 17.6; follow-up to [URP-FINALIZATION.md](URP-FINALIZATION.md).
This records implementation decisions and verification separately from the original migration.

## Placeable rendering investigation

The reported symptom is disappearing/see-through blocks during preview and after placement.
Snares and SnaresModel reference the same embedded `klocki.fbx` materials: URP Lit, opaque queue
2000, depth writes on, alpha clipping off and back-face culling. Colors are preserved. Checked mesh
triangle winding agrees with normals, transforms have positive determinants, and preview meshes
render from both diagonal directions in isolated captures. Both preview and server-spawned piles
are visible in the gameplay scene (`Logs/followup-placed-blocks.png`). No material or mesh rewrite
was needed. A later test of the actual T input reproduced the missing preview; see below.

TeddyBear preview/placed materials use URP Lit, including embedded shield/sword materials. Barrel's
visible bin uses URP Simple Lit; the legacy Standard material is on a disabled root renderer.
The native integration test now renders all six prefabs from both directions and checks shader
support and non-background pixel coverage. All twelve captures passed on Linux/Vulkan; see
`Logs/placeable-<prefab>-<view>.png`. This catches missing models/materials, not every possible
camera-dependent targeting or grid-plane artifact during normal play.

### Placement input and candidate cell

The live T test exposed two causes that isolated model renders could not catch:

- FPS used `ScreenPointToRay(Input.mousePosition)` with a locked cursor. On this Linux Editor the
  cursor could be `(1242.5, 834.5)` for a 1280x720 render area, aiming at the ceiling instead of the
  crosshair. FPS now uses `ViewportPointToRay(0.5, 0.5)`; the overhead view retains pointer targeting
  and selects its own camera.
- The authored grid visual at Y=3 intersected the pile's bottom at Y=2.981, making blocks appear
  transparent/partly hidden. Each cell now places its visual 0.005 above its matching physical floor
  (Y=2.9175 on that floor), without moving logical grid points, colliders or the spawned objects.

T immediately creates the selected available item's preview. Free cells under the aim are yellow;
occupied cells stay red, and the previous candidate restores its normal sprite/color. The preview
remains visible over a floor without a grid cell, but cannot be placed there. Wall/sky misses hide it.
The yellow visual uses a shared runtime white square with the original cell dimensions: tinting
the green texture yellow would retain green RGB. It never modifies shared materials or source assets.

Preview colliders are disabled and rigidbodies made kinematic so they cannot catch the placement
ray. Remote players do not process local placement input. Switching items, T cancellation, placement,
pause/settings and component disable clear the local highlight. A click captures the position before
destroying the preview and places once on button-down; the existing mouse-release latch prevents a
gunshot from the same click.

Verified with physical keyboard/mouse events: T, moving aim, occupied red cells, wheel cycling through
all three models, selecting an available inventory slot, pause/resume and cleanup. One placement
changed count 3 to 2, spawned exactly one networked snare, retained 7 pistol rounds and left no yellow
cell. See `Logs/placement-preview-check.json`, `Logs/placement-after-click.json` and
`Logs/placement-t-after.png`. Desktop compile/asset checks passed for Windows, macOS and Linux.
The updated native Linux build succeeded with zero errors (14.3 seconds), and its full
`-verifyGraphics` run passed after these placement changes (`Logs/placement-graphics-player.log`).

### Two-peer placement verification

Placement retains the existing Mirror path: the owning client sends `CmdPlaceEntity`, the server
spawns with `NetworkServer.Spawn`, and `RpcPlaceEntity` marks the grid cell occupied on peers.
The preview and yellow candidate remain local presentation; they are not networked objects.

An Editor host and a separate native Linux client connected with two players. Physical keyboard
and mouse input in the native client placed a snare; host input placed a teddy bear and barrel.
Both peers reported the same three network IDs (11/12/13), item types and positions within 0.001,
and three occupied cells. See `Logs/placement-network-result.json`. Consuming the snare subsequently
removed it from the client and reduced its occupied count to two. This validates connected peers;
late joining remains outside this check (joining after match start is already disabled).

The opt-in `-verifyPlacementPeer` flag writes client snapshots to
`Logs/placement-network-client.json`. Add `-verifyPlacementInput` only for controlled input QA:
it teleports the local test player to room one, supplies items and freezes movement. Neither flag
changes normal gameplay. A full multiplayer playthrough, item interactions under latency and
disconnect handling remain manual QA.

### Placement reach

Placement is limited to a circular radius of two grid tiles (currently two world units), measured
from the player's X/Z position to the snapped cell center. Two tiles allow nearby defensive
placement without reaching across the room; diagonal distance uses the same circle. This applies
to all three items and both camera modes. Outside the radius the local model and candidate
highlight disappear, and clicking cannot place anything.

The server repeats the range check and verifies the cell is free before spawning. Inventory and
the success sound wait for a targeted confirmation; rejection preserves stock and cell occupancy.
One outstanding request per player prevents repeated clicks spending unconfirmed inventory.
The authoritative spawn and occupied-cell RPC still update all connected peers.

Verification: the exact two-tile boundary passed; 2.01 tiles and the (1.5, 1.5) diagonal failed.
A direct distant command was rejected without spawning, occupying a cell or consuming inventory
(`Logs/placement-radius-check.json`). Live preview checks confirmed hide/clear outside reach and
recovery on returning; acceptance consumed exactly one item, while an occupied-cell retry consumed
nothing (`Logs/placement-radius-preview.json`). An updated Linux build succeeded with zero errors;
a native client's physical-input placement and an Editor host placement matched IDs, positions and
two occupied cells on both peers (`Logs/placement-radius-network.json`).
Desktop compile/asset checks passed for Windows, macOS and Linux, including saved Mirror scene IDs;
the previously documented TMP editor harness warning remains (`Logs/placement-radius-compile.log`).

## Candle mood

Losing **one** capture area loses the match. Therefore the mood signal uses the lowest fraction
of lit candles among the current room's areas, not the room average. `CaptureArea.CandleCount` and
`LitCandleCount` expose the existing flame quantization without adding network state or altering
capture rules. `RoomLighting` reads that synchronized progress locally.

Ambient intensity scales from 100% to 75% in linear space; the additional post-exposure offset ranges from 0 to -0.45 EV.
Both ease toward their targets with an exponential time constant of one third of a second. The
exposure offset is applied after eye adaptation/manual exposure; it cannot be compensated away by
the automatic meter. It affects the shared 3D view, not Canvas HUD. Existing local candle lights are
unchanged. Preparation/room changes, victory, component shutdown and scene changes reset the effect;
losing retains it. Unity retains ownership of the ambient probe; no per-frame GI rebake or GPU
environment readback is required.

### Preserve night brightness between phases

The first version incorrectly round-tripped `RenderSettings.ambientProbe`: its getter already
included environment intensity, and assigning those coefficients applied intensity again. With the
scene's intensity 1.5, the red L0 coefficient jumped from 0.108275 to 0.264197 at full candles
(roughly 2.44x), despite the light count staying at 84. Repeated phase resets could compound it.

`RoomLighting` now changes only ambient intensity and restores its captured scalar baseline.
In the project's Gamma color space it converts that scalar to linear before scaling, then back.
It never assigns SH coefficients. Preparing and fighting now both retain L0=0.108275, intensity=1.5;
automatic exposure stayed approximately -0.73 EV across the transition. The existing manual
exposure preference and eye-adaptation algorithm were preserved rather than hiding the issue with
a global brightness override. Captures: `Logs/night-prepare-after.png` / `night-fight-after.png`.

All three rooms were checked at full and one-of-twelve candles: ambient ratios approached 0.773,
exposure offset -0.409 EV, and victory restored the original baseline without brightness ratcheting.
`-verifyGraphics` now includes an assertion that all three prepare/fight transitions leave the
original ambient coefficients and intensity unchanged at full candles.

### Green foam mat

The embedded green in `puzzle_dywan.fbx` was `(0, 0.153, 0.072)`, substantially darker than the
green toy bricks `(0, 1, 0)`. `Assets/Materials/ToyMatGreen.mat` copies the existing URP material,
changes only its base/legacy color to the toy green and remaps only the model's `green` slot through
ModelImporter. Other colors, geometry and lighting remain unchanged. An external material makes
the correction survive reimports; the importer metadata was upgraded by Unity while preserving
the model GUID.

### Candle-driven dark fog

For mobile-oriented atmosphere, extend the gameplay scene's existing ExponentialSquared fog
rather than adding volumetric ray marching, transparent fog cards or a fullscreen depth pass.
URP's installed `ShaderVariablesFunctions.hlsl` already blends surface color with fog; updating
the existing uniforms adds no geometry, render pass or render texture. No package or shader changes.

`RoomLighting` captures the scene fog color/density at fight start. Full candles retain the exact
authored values (RGB 0.138/0.138/0.217, density 0.08). The weakest candle area's loss interpolates
the color toward RGB 0.008/0.008/0.012 and density toward 0.12, using the same one-third-second
time constant as ambient. At one of twelve candles the density approaches 0.11972; nearby surfaces
remain readable while distance falls into darkness. This is distance haze, not moving smoke.

Only already enabled Exp2 fog participates. Other modes and disabled scenes remain authored;
denser starting fog is never reduced. Preparation, victory and component shutdown restore exact
baseline values. Scene loading relinquishes ownership so gameplay values cannot overwrite menu
RenderSettings. Losing keeps the darkened mood. No new graphics preference or network state.

Target: mid-range phones at 30 FPS. Mobile acceptance requires A/B profiling on the same real
device, resolution and warmed-up scene: hold camera, candle state and ambient/exposure fixed and
compare only the authored versus darkened fog uniforms. Added GPU cost must be at most 0.5 ms
and 5% of baseline.
Desktop results do not certify that budget or overall mobile frame rate. Check nearby enemies,
placement cells, muzzle flashes and HUD with manual/automatic exposure on the device as well.

Research: [Unity fog density API](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/rendersettings/fogdensity)
and [URP performance guidance](https://docs.unity.com/en-us/engine/6000.6/manual/render-pipelines/universal-render-pipeline/introduction/configure-for-better-performance).

The initial 0.10 density cap was increased to 0.12 after visual feedback, preserving the full-candle baseline and rendering cost.
The response subsequently changed from linear candle loss to `1 - fraction * fraction` for
ambient, exposure and fog together: 25% candle loss gives 43.75% of the mood, and half loss gives
75%. Full/max endpoints and the smoothing speed stay unchanged. Editor checks measured fog
density 0.08 / 0.09748 / 0.10998 / 0.11999 at 100/75/50/0% candles, then verified preparation reset
(`Logs/candle-curve-check.json`). Earlier density readings below describe the previous linear curve.
The stronger version passed Editor compilation, automatic/manual exposure visual checks at full,
half and one-of-twelve candles (`Logs/fog-strong-*.png`), and full-candle/reset checks across all
three rooms (`Logs/fog-strong-reset.json`). Low-candle density reached approximately 0.1166.

Initial verification (0.10 cap): Editor captures at 100%, 50% and one-of-twelve candles passed visual review with
automatic/manual exposure and bloom disabled (`Logs/fog-*.png`). Low-candle density reached
0.09833. Component-disable restoration, disabled-fog preservation and gameplay-to-menu scene
reset passed (`Logs/fog-lifecycle.json`, `Logs/fog-scene-reset.txt`). The Linux build succeeded
with zero errors (40.5 seconds), and native `-verifyGraphics` passed fog bounds and full-candle,
preparation and victory invariants in all three rooms. Render-texture counts stayed 28/28,
25/25 and 25/25; draw counts fluctuated by one around 2010/2011, with no added fog pass.
See `Logs/graphics-verification.json` and `Logs/fog-graphics-player.log`. Windows/macOS/Linux
compile and asset checks passed; the existing TMP editor harness warning remains.

An Editor host check reached 0.773 ambient and -0.409 EV at one of twelve candles, verified the
weakest-point rule in a multi-area room and resets at the next two preparations. Early probes let
enemies consume test candles; the repeatable check freezes simulation while testing presentation.

## Crosshair at low resolution

The HUD's horizontal bars were only 1.25 reference units thick; the 1920x1080 Canvas scaler
reduced them to approximately 0.833 screen pixels at 1280x720, allowing them to disappear between
pixel rows. `AimScaler` now keeps the shorter axis at least two physical pixels on the existing
Overlay Canvas, preserving the authored length, spread and hit marker. It runs in LateUpdate after
PlayerShoot changes spread, avoiding script-order dependence. No HUD hierarchy or texture changes.
Editor screenshots and geometry checks passed at 1280x720, 1280x800, 1920x1080 and 640x360
(`Logs/crosshair-fixed.json`, `Logs/crosshair-fixed-*.png`); spread/hit-marker thickness and hiding
were also checked. The original Game view size was restored after QA.
The updated Linux build succeeded with zero errors (33.9 seconds); native `-verifyGraphics`,
launched at 1280x720, passed with the earlier candle curve and all three room resets
(`Logs/crosshair-graphics-player.log`). No runtime exceptions were logged.

## Display and persistence

Previously borderless always used desktop resolution and `RuntimeGraphics` forced scale 1. Now
windowed resolution controls window dimensions; borderless resolution controls URP scale with
aspect-preserving choices. UI stays at desktop resolution and uses bilinear world upscaling, no FSR.
Quality changes preserve the selected render scale.

Confirmed output dimensions retain `display.width/height/mode`; new `display.renderWidth/Height`
store internal dimensions. Missing new keys retain native rendering. Trials modify runtime state;
Keep persists it. Cancel, timeout, Escape, scene changes and closing Settings restore both the original
output mode and scale. Startup fits saved dimensions to the current monitor.

`DisplayVerification` is opt-in: launch with `-verifyDisplayWrite`, then `-verifyDisplayRead`. The
second process checks actual startup state rather than invoking startup methods manually. Only its
touched keys are backed up in ignored Logs and restored after the read stage (also on test failure).
Never run the two test stages concurrently with another instance changing the same player prefs.

## Lobby

Extra address/connect controls previously expanded a vertically centered legacy column, putting
the list over the baked Join Game title and Refresh beyond the paper. The column now has a bounded
top-aligned area, explicit control sizes and a flexible list. The list viewport now stretches inside
its frame instead of having a zero-sized rectangle. Existing paper art, fonts and input bindings stay.
Room-name fields use a contrasting 2-unit caret at one blink per second; the address clone inherits it.
The address input needs at least 72 units of text height for its inherited 60-point font: a one-unit
shortfall previously removed the entire placeholder line. Its row is now 90 units tall. The list's
content sizes to its rows, which use non-wrapping, bounded text instead of overflowing their buttons.

## Removed dependencies

The old stack was vendored PPSv1 under `Assets/PostProcessing`, not a UPM dependency. A GUID scan
found only the OfficeProps demo scene/profile outside its own examples. Removed that scene/profile,
the complete old stack/examples/metas and `UrpLegacyShaderStripper`. OfficeProps gameplay assets stay.
URP/Core post-processing remains required. Unity Pipeline, the Linux compiler toolchain and backup
Coplay MCP remain; no unrelated engine modules were removed.

## Verification record

- Desktop compile/asset checks: Windows, macOS and Linux passed; the existing TMP editor harness
  limitation remains a warning. Mirror scene IDs passed.
- Editor lobby checks: focused caret blink toggled and width was 2. Controls, visible address glyphs
  and server-row bounds passed at 1280x800, 1280x720 and 1920x1080. Captures: `Logs/lobby-*.png`.
- Native Linux build: succeeded with zero errors (final build 42.3 seconds).
- Two independent launches: `Logs/display-write.txt` and `Logs/display-read.txt` both PASS, including
  startup effects, manual exposure, quality/FPS, borderless scale, rollback and quality independence.
  The original preferences were restored and the temporary backup was removed.
- Updated native `-verifyGraphics`: PASS, including all twelve placeable captures, shared camera
  effects, GPU bloom/adaptation, quality switching, 640x360 window changes and timeout rollback.
- Native host + Editor client: connected with two players and the client's WeaponCamera Overlay
  present (`Logs/followup-two-peer.json`). A full multi-room multiplayer playthrough remains manual QA.
- Follow-up T-input reproduction identified and repaired cursor-based aiming and a grid plane
  intersecting low models. Shader/material-only probes had correctly passed but missed this flow.
- Night/mat follow-up: Linux build succeeded with zero errors (14.0 seconds). Native
  `-verifyGraphics` passed, including all three preparation/fight ambient invariants;
  `Logs/night-graphics-player.log` contained no exceptions. Desktop compile checks passed again.
- Two-peer placement: native client snare and Editor host teddy/barrel matched network IDs,
  positions and occupied cells (`Logs/placement-network-result.json`).

No dangling references to removed asset GUIDs were found. Known unrelated Editor issues remain:
the local Rider DLL name mismatch, TMP editor compile harness warning, and a legacy PlacementController
prefab without NetworkIdentity reported while the existing build ID baker scans all prefabs. These
were not introduced or changed here. The user's console was not cleared. Native final verification
logged no exceptions.

Local Rider DLL changes and scene recovery copies are outside this work.
