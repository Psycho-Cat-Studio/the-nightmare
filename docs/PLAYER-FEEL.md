# Player movement and weapon feel

Unity 6000.6.7f1 / Mirror. This pass keeps the Rigidbody controller, existing animation clips,
2.5 m/s walking, 7 m/s sprinting, weapon damage, fire modes, ammunition and reload durations.
No new packages, networking messages, camera bob or gameplay movement mechanics.

## Input and camera

`PlayerController` reads input before `PlayerShoot` (`DefaultExecutionOrder(-200)`). Sprint is
computed from input and weapon availability, not an Animator flag. Releasing Shift, aiming or
firing cancels sprint immediately. Animation follows that decision. Analog movement is clamped
to unit length rather than normalized, preserving partial joystick movement.

`PlayerMotor` retains interpolated physics movement/rotation in FixedUpdate. Its absolute view
yaw and pitch reach the camera every rendered frame; world camera rotation cancels inherited
interpolation lag. Movement uses view yaw, and the shot samples the updated camera before adding
its new recoil impulse. Both mouse axes use raw deltas; joystick look is a time-scaled rate.
The original recoil impulse and continuous equivalent of its 50 Hz decay are preserved.
Pause, settings, disable/death and building transitions clear pending motion and recoil.

## Local weapon presentation

`WeaponPresentation` is added to the existing local WeaponsHolder at runtime. It offsets the
holder above the authored weapon animations, without changing their bindings or remote weapons.
Inspector tunables cap sway at 1.5 degrees and movement offset at 1 cm. Actual horizontal travel
advances gait; standing against a wall does not produce walking bob. ADS reduces these effects
to 20%. Shots add a bounded 1.5 cm / 1.5 degree kick that returns 95% in 0.18 seconds. Swap,
teleport and disable reset offsets; the first shot after a swap still gets its impulse.

Zoom reaches 95% of its target in 0.18 seconds regardless of frame rate. Existing Idle/Aim/Sprint
transitions use 0.12-second blends. Reload/hide transitions retain their authored timing.
Crosshair expansion and shot impulses occur only when ammunition is actually consumed.

## Persistent low-health edges

The existing mesh overlay adds no render pass. Below 30% HP its baseline smoothly increases to
0.18 opacity at 10% HP, capped below that. It has no continuous pulse. The damage flash retains
its existing HP-dependent intensity and wins whenever stronger than the baseline.
Healing removes old flashes and eases the baseline down. Menus, death and player rebinding clear
all transient state; respawn restores the ordinary view. The center remains transparent.

## Verification

Editor-host input checks passed at 30/60/120 FPS: walking traveled 2.00/2.05/2.05 m during an
approximately 0.8-second input, and sprint returned to walking while W remained held. Measured
95% zoom times were 0.168/0.167/0.170 seconds. Holding pistol fire consumed one round, triple fire
three, and automatic fire eight in roughly 0.65 seconds. Pause blocked movement and shooting.
Camera yaw updated immediately while the physics body remained unchanged until its next tick;
recoil decay matched its continuous formula at all three frame rates.

Low-health baseline measurements: 100/30/25/20/10/5% HP → 0/0/0.0281/0.09/0.18/0.18 opacity.
Damage flash, healing, pause/resume and respawn checks passed. Gameplay capture confirms readable
center and visible edges (`Logs/player-low-health.png`). Runtime scripts/results in Logs are
local verification artifacts, not shipped components.

An Editor host plus a native Linux development client passed the two-peer check: the client
observed 2.05 m of host travel and the remote muzzle flash; server damage replicated 20 HP and
0.09 local overlay opacity. Remote models had no WeaponPresentation component. A separate
client-local shot consumed one round, immediately changed the holder and settled within 0.5 s;
view yaw matched in the same call. Building/action transitions cleared motion/recoil and restored
controls; both Boy/Girl prefabs retained valid controller/camera references. Pistol/rifle reloads
retained 2.0/2.2-second gameplay timing; hide/show completed with the swap lock released, and the
first shot after swapping retained its impulse (`Logs/player-feel-reload-swap.json`). Evidence:
`Logs/player-feel-qa.json`, `Logs/player-view-qa.json`, `Logs/player-health-qa.json`,
`Logs/player-feel-peer-client-result.json`, `Logs/player-feel-client-local.json` and
`Logs/player-feel-lifecycle.json`.

Windows/macOS/Linux compile and scene-ID checks passed (`Logs/player-feel-compile-final.log`),
with the existing TMP editor harness warning. The build baker still reports the pre-existing
unused PlacementController prefab's missing NetworkIdentity. Repeated test connection setup also
logged one duplicate AddPlayer message; it did not recur during the successful two-peer checks.

The regular Linux build succeeded with zero build errors in 43.6 seconds
(`Logs/player-feel-release-build.json`); the development QA build also succeeded with zero errors.
No runtime inspection define is retained in project settings or the regular build.

Feedback visibility: 3/5 → 4/5 for this scoped feel pass: shot impulses accompany actual shots,
and persistent edges communicate danger while keeping the center clear. Subjective feel with
the user's mouse and real-phone joystick/performance remain manual checks.
