# Friendly fire, lobby safety and explosion feedback

Unity 6000.6.7f1 / URP / Mirror. Player damage is enabled during preparation and fighting in all
three rooms. Lobby, uninitialized matches and win/loss phases are safe.

## Authoritative player health

`GameManager.CanDamagePlayers` derives the policy from the existing match phase; it is not a
client-controlled option. `PlayerManager.TakeDamage` enforces it on the server for every damage
source, including weapons, grenades, trash cans, enemies and teddy-bear damage. Lobby shots still
consume ammo and show muzzle effects, but do not trigger damage hitmarkers or the red HP pulse.

Initial health is assigned in OnStartServer, before spawn serialization. Setup no longer overwrites
health on clients. This addresses the previously observed initial host/client health mismatch.
No QA health reset is allowed when verifying fresh joins.

The active tagged player collider uses LocalPlayer on its own peer and RemotePlayer on other
peers. The existing weapon mask includes RemotePlayer and excludes LocalPlayer; previously the
remote collider remained on the excluded local layer. Visual-model, weapon and revive-collider
layers are preserved. Existing Commands, damage plausibility checks and health SyncVars remain.

Explosions still deal 45 player damage, including self damage; enemy damage remains 150. Each
explosion's existing HashSet deduplicates multiple colliders belonging to one target. Separate
explosions in a chain can each deal damage. Blast radius, lack of damage falloff and existing
through-obstacle blast behavior are unchanged.

## Server explosion data

Grenade fuse/trigger activation runs only on the server (2 s fuse, existing 0.2 s damage window).
The grenade Rigidbody is frozen at detonation so the 0.2 s damage sphere cannot travel beyond
the displayed radius after a fast throw. Trash cans retain their one-time server detonation and chain-reaction guard. Component references
are cached in Awake so a detonation RPC cannot race their Start initialization.

Each existing explosion RPC now carries the actual sphere center and world radius:

`center = sphere.transform.TransformPoint(sphere.center)`

`radius = sphere.radius * max(abs(sphere.transform.lossyScale))`

Current grenade radius is 3.52 m; trash-can radius is 1.5 m. No duplicate radius setting is stored
in the effect. Clients hide the source model/collider before playing local feedback, avoiding a
ground ray hitting the exploding object itself. Damage remains independent of rendering/pooling.

## Visuals and budget

The original grenade and trash-can animations and audio settings have been restored after
visual review. All eight authored Particle Systems, subemitters, materials, root scale (0.3)
and AudioSource settings are preserved. Grenades use `boom`; trash cans use `explosion`, both
at their original volume of 1. Native activation starts the original autoplay hierarchy; the
controller no longer overrides emissions, gradients, particle counts or dust animation.
Effects retain the original three-second lifetime and source-specific rotation. Visual scale now
follows the shared radius ratio described in [Ghost and combat polish](GHOST-AND-COMBAT-POLISH.md):
0.45 for grenades and 0.191761 for trash cans. The floor ring remains independent.

The previous replacement's 46-particle budget no longer applies. The restored effect needs
fresh profiling on target phones; the pool bounds instance count, not GPU overdraw.

A shared 64-segment annulus marks the full radius immediately, then fades over 0.45 s. It follows
a downward raycast to Default-layer colliders tagged Floor / Floor 0 / Floor 1, ignores triggers,
and uses depth testing. Its diameter is exactly twice the supplied radius. It represents maximum
horizontal reach; actual damage still uses the 3D sphere and target collider overlap. High airbursts
without floor within radius + 0.6 m omit the ring instead of marking a distant, unaffected floor.

ExplosionFeedbackPool is scene-owned. Each of the two effect prefabs prewarms four instances on
first use: eight total. Prewarming under an inactive parent prevents unwanted autoplay and
sounds before a real detonation. Further events reuse inactive entries or replace the oldest active entry.
There are no further per-explosion GameObjects/materials after warmup. A reused entry clears its
particles, audio and ring state. Audio is played once and may finish up to the old three-second
limit, then the effect deactivates. Scene unloading releases the pool.

## Restoration verification

- Compared both restored prefabs against the pre-feedback backups: original particle, audio,
  transform and material-reference blocks were unchanged at restoration; only the ring controller was added.
  Subsequent radius-based runtime scaling is documented above.
- Editor comparison against the original autoplay hierarchy matched particle counts at the
  sampled frame, original scale/audio and ring world radii (3.52 m / 1.5 m)
  (`Logs/explosion-original-comparison.json`).
- Five consecutive plays per type produced particles and active original audio each time,
  retaining eight pooled objects (`Logs/explosion-restored-replay-qa.json`). Captures show
  the restored effects in the gameplay room. Listening acceptance remains with the player.
- Windows/macOS/Linux compile and asset/scene-ID checks passed
  (`Logs/explosion-restore-compile-final.log`); the existing TMP harness warning remains.

- Regular Linux build succeeded with zero build errors in 30.7 seconds
  (`Logs/explosion-restore-release-build.json`).

## Earlier verification before the presentation rollback

The particle counts and benchmark below describe the superseded replacement effect, not the
restored animation. Gameplay/network checks remain relevant: damage logic was not reverted.


- Editor fresh host: lobby remained at 100 HP after direct lethal damage, a trash-can blast
  and a grenade. Preparation-phase blasts each dealt exactly 45 HP, including with an extra
  player collider. Fully outside/just overlapping both sphere boundaries gave no damage/45 HP.
  Death and revive passed (`Logs/friendly-fire-editor-qa.json`).
- Camera captures show the ground ring/fire with bloom enabled and disabled. An airburst with
  no floor in range omitted its ring. Six actual trash cans chained successfully. No dynamic
  lights were present (`Logs/explosion-presentation-qa.json`, `Logs/explosion-no-bloom.png`).
- Pool stress: 12 repeated bursts retained eight objects and at most 46 live particles per
  effect; all effects returned inactive afterwards (`Logs/explosion-visual-qa.json`).
- Fresh native Linux client + Editor host: both peers reported 100 HP for both players before
  damage, without SetDefaults/revive/reset. Each peer saw its own tagged collider on layer 9
  and the other player's on layer 10 (`Logs/friendly-native-lobby-stage.cs` reproduction).
- Actual pistol rays in both directions hit RemotePlayer but caused no lobby HP loss or
  hitmarker. Lobby grenade/trash-can explosions also left both players at 100 HP. The client
  received radii 1.5 and 3.52, active ground rings and an eight-object pool
  (`Logs/friendly-native-blast-qa.json`).
- In preparation, host and client shots each dealt 20 HP and showed hitmarkers. A trash can
  dealt 45 to the client in range and none to the host outside. Further host shots downed the
  client; the existing CmdCallRevive path restored the client to 100 HP. Its actual grenade
  Command then dealt 45 to both players, including itself: host 80→35, client 100→55.
- During invasion, both directions again dealt 20 HP. In Win, a 1000-damage attempt was blocked
  for both living players (`Logs/friendly-peer-final.json`, host 15 HP / client 35 HP).
- Native warmed-pool benchmark: 120 bursts in batches of ten retained eight instances and
  368 total live particles. CPU enqueue/reset/emission time averaged 1.25 ms per ten bursts,
  maximum 1.87 ms, on this Steam Deck (`Logs/friendly-native-pool-benchmark.json`). Audio was
  muted for the benchmark. This is not whole-frame GPU time or a mobile frame-rate guarantee.
- Windows/macOS/Linux compile and asset/scene-ID checks passed; the existing TMP editor harness
  warning remains (`Logs/friendly-fire-compile-final.log`).
- The regular Linux build succeeded with zero build errors in 16.1 seconds
  (`Logs/friendly-fire-release-build.json`). The final regular player also passed
  `-verifyGraphics` (`Logs/friendly-fire-release-graphics.log`). The build baker retains its pre-existing
  standalone PlacementController/NetworkIdentity validation message.
- Fast moving grenade test (8 m/s): its Rigidbody became kinematic at detonation, the damage
  center drifted 0 m afterwards and the visual center matched exactly
  (`Logs/grenade-fixed-center-qa.json`).
- Runtime Pipeline was enabled temporarily only for the development QA player, then removed
  from project defines. The final regular player excludes the runtime inspection server.

Temporary scripts/screenshots remain in ignored Logs. A real-phone test and listening acceptance
remain necessary. The previous fixed bug reports about initial HP should be read alongside this
fresh-join verification; no manual health reset was used in the two-peer lobby check.
