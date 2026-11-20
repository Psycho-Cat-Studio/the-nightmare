# Player damage feedback

The local player's HUD adds a brief red vignette when replicated health decreases. It is a
uGUI mesh overlay, independent of URP post-processing and the graphics Vignette toggle.

## Behavior

- `HealthBar.Start` creates one `PlayerDamageOverlay` under its existing root Canvas.
- The overlay observes only the player assigned to that local health bar. It does not change
  damage, health replication or the enemy attack rate, and adds no networking messages.
- A health decrease raises opacity using `0.04 + 0.66 * (1 - health/maxHealth)²`, making high-HP hits subtle and low-HP hits strong.
  Repeated/continuous damage keeps the edge visible rather than restarting a flashing animation.
- After 0.12 seconds without damage, opacity fades proportionally to zero over 0.55 seconds.
  Unscaled time prevents a stuck fade.
- The central area is transparent. Red becomes stronger toward the perimeter and corners.
- Healing/revive, player reassignment, death, pause, settings, welcome/instruction and result
  screens clear the effect. Health changes while hidden do not trigger a delayed pulse on return.
- Raycasts are disabled, so the overlay cannot intercept mouse/touch input.

## Rendering budget

One procedural UI mesh: 425 vertices / 768 triangles, using Unity's existing UI material.
There are no textures, image effects, extra cameras, lights or full-screen render passes.
Anchors stretch to the root Canvas; normalized coordinates preserve the effect at 720p,
other resolutions and different aspect ratios. Fading changes CanvasRenderer alpha rather
than rebuilding vertices every frame. No prefab/scene YAML or serialized references are changed.

## Validation

The latest HP/audio/alarm tuning and acceptance results are in [FEEDBACK-TUNING.md](FEEDBACK-TUNING.md).
The original fixed-strength baseline results below predate the HP curve.

- Editor: real server health decreases produced an immediate pulse (opacity 0.65); continuous
  damage held it visible; fade, healing, pause/resume and player reassignment cleared it. The
  mesh had 425 vertices / 768 triangles, a transparent center and raycastTarget disabled
  (`Logs/player-damage-editor-qa.json`, `Logs/player-damage-720p.png`).
- Separate Linux client: server damage replicated from 100 to 88 HP, gave opacity 0.67, then
  faded to zero at both 1280×720 and 1920×800. The host retained 100 HP / zero overlay alpha
  (`Logs/player-damage-native-qa.json`, `Logs/player-damage-native-wide-qa.json`).
- Existing network death/revive path: the client observed death, a cleared overlay, then
  revive at 100 HP without an extra damage pulse. The first network fixture entered with a
  host/client health-state mismatch and was reset via the existing revive command before
  these checks; the test does not establish fresh-join health initialization correctness.
- Windows/macOS/Linux compile and asset/scene-ID checks passed; the existing TMP editor
  harness warning remains (`Logs/player-damage-compile.log`).
- The regular Linux build also succeeded with zero build errors in 30.9 seconds
  (`Logs/player-damage-release-build.json`). The pre-existing standalone PlacementController
  NetworkIdentity validation message still appears during the build baker.
- The separate development QA build succeeded without errors. Runtime Pipeline was enabled
  temporarily for inspection and removed from the project defines after building.

Mobile frame-rate and final audio listening acceptance remain manual checks.

The initial-health mismatch was subsequently fixed and verified on fresh peers in
[FRIENDLY-FIRE-EXPLOSIONS.md](FRIENDLY-FIRE-EXPLOSIONS.md); earlier reset-based tests above are historical.
