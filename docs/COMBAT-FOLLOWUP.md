# Combat follow-up: scream contact, spawn targets and audio

Unity 6000.6.7f1 / URP / Mirror. This extends the existing enemy feedback and weapon systems.

## Contact after screaming

Previously only OnTriggerEnter in Running entered Fighting. A player entering during the
2.2-second scream never produced a second enter when the scream finished. A separate one-second
initial destination coroutine could also overwrite a detected player or unlock an engaged enemy.

The server now remembers player collider contacts, refreshes them on OnTriggerStay and checks
physical penetration against the root melee box. The larger child chase detector alone cannot
deal damage. Screaming only records contacts; on completion the enemy immediately enters Fighting
if a live player remains in the melee box. Otherwise it resumes running. Fighting rechecks all
contacts each frame, so one collider leaving cannot cancel another valid contact. Leaving melee
range resumes the existing chase of a live target; a dead/inactive target is dropped in favor of
the nearest eligible candles. No new chase-distance cutoff is introduced. Death and traps
suppress attacks. Damage remains the existing server DPS multiplied by delta time.

Initial capture targeting is synchronous at Start after agent/animator initialization. It cannot
later overwrite a chase or fight. Scream distance checks also wait for an actual calculated path.

## First destination in room 2

`EnemySpawnTarget` is optional authoring data on a spawn, containing a CaptureArea reference.
GameManager passes it to the spawned enemy before NetworkServer.Spawn. Only the first destination
selection consumes it; subsequent selection always uses the nearest eligible area. No new target
RPC is needed; the existing destination RPC remains in use.

In scene hierarchy Room6 (the second gameplay room), Spawn Point 2 Room2 at the entrance initially
attacks Area 0. Spawn Point 0/1 still initially attack Area 1. After losing a player, enemies from
all spawns use nearest-area selection, with the existing seven-enemy retarget threshold. Initial
selection retains the old behavior of ignoring that threshold. Inactive/captured areas are excluded;
a missing/invalid override falls back to nearest. Other rooms have no overrides.

## Death voice

Every first lethal TakeDamage calls EnemyFeedback.ServerDied, so bullets, explosives and teddy-bear
damage share the event. The server chooses one of three clips without an immediate repeat across
deaths and broadcasts its index/pitch. Each peer stops the long charge scream and any pain/attack
voice, clears the hurt layer and plays the chosen death voice exactly once. Later animation/death
updates must not stop that voice. Existing death animation, dissolve and despawn timing remain.

The clips derive from Vinrax's CC0 Horror scream1 recording already used for pain. Source excerpts:
1.10/3.70/5.10 seconds, rates 0.90/1.00/0.94, final lengths 0.70/0.80/0.85 seconds. Filtering is
160–6500 Hz, onset 15 ms, release 140 ms. Updated RMS is approximately -12.0/-12.2/-13.6 dBFS;
source volume 0.9, priority 64, pitch 0.95–1.05 and linear attenuation 5–24 units. Mono 22050 Hz, preloaded ADPCM/decompressed on load.
The existing feedback AudioSource is reused; no additional concurrent voice or runtime DSP.

## Dry automatic gunfire

The old common shot is approximately 2.3 seconds long; rifle cadence is 0.08 seconds. Repeated
PlayOneShot calls layered many reverberant tails. The initial 75 ms replacement was too thin.
The current 145 ms crop restores the body, adds a +5 dB low shelf near 170 Hz and fades the last
70 ms. Two alternating, bounded Stop/Play sources bypass Reverb Zones and retain short body
overlap without stacking long tails. Both use 90% of the original source volume; routing and
spatial settings are inherited. The mono 44100 Hz PCM sample is shared across sources/players.

Both local fire and remote shot RPCs use the same sound selection. Shared reload, pickup and
hitmarker audio is not stopped. The pistol retains its existing sample. Weapon cadence, modes,
ammo consumption, damage and RPC frequency are unchanged. Both Boy/Girl prefabs have the dry
clip reference. See [FEEDBACK-TUNING.md](FEEDBACK-TUNING.md) for the latest measurements.

## Prefab reference integrity

Both existing player prefabs had an added-component override pointing to missing local ID
3953590870183607671 on BuildingCamera (already dangling in the baseline). Unity logged broken
PPtr errors when restoring cached Editor state. Saving the prefabs through the Editor removed
these invalid overrides; Girl also dropped its now-unused stripped GameObject entry. Mirror
asset-ID defaults and saved scene IDs were preserved. No live camera component was removed.

## Verification

These baseline results precede the latest audio tuning; see [FEEDBACK-TUNING.md](FEEDBACK-TUNING.md).

- Actual Editor physics contact: no damage while screaming, immediate Fighting/damage after
  completion, retained contact after one of two player colliders exited, recovery via Stay,
  no damage while Blocked, and no damage to a player who left before completion passed
  (`Logs/combat-contact-qa.json`). Walking still follows the original pre-charge phase: contact
  is remembered, but only Running may enter Fighting.
- Actual GameManager spawn iterator: all 12 gameplay spawn points passed initial/retarget checks.
  The room-2 entrance chose Area 0 first, then Area 1 when nearest on retarget; other rooms
  retained their ordinary choices (`Logs/combat-targets-qa.json`).
- Editor weapon fire: 12 calls consumed 12 rifle rounds, dry clip length 0.075 s, no playing
  tail after the series, shared reload audio preserved and pistol sample unchanged. Yellow,
  Orange and Red each played one short death variant, cut off their old scream and did not
  restart after further damage (`Logs/combat-audio-qa.json`).
- A separate native Linux client contacted a screaming enemy: HP stayed 100 during the scream
  then decreased after completion, while its server AI component remained disabled on the client.
  The Editor host confirmed Fighting against that remote player
  (`Logs/combat-native-contact-qa.json`). QA initialized server player health through the
  existing SetDefaults path rather than asserting fresh-join health correctness.
- Host and native client observed the same SkeletonDeath02 at volume 0.6, with the old scream
  stopped, hurt layer cleared and the new voice allowed to finish
  (`Logs/combat-native-death-qa.json`).
- The native player's actual 12-shot rifle series consumed 12 rounds. The host observed the
  same dry source playing across 24 sampled frames, then silence; both used the 0.075 s clip
  (`Logs/combat-native-rifle-qa.json`, `Logs/combat-host-rifle-qa.json`).
- Windows/macOS/Linux compile and asset checks passed; existing TMP editor harness warnings
  remain (`Logs/combat-followup-compile-final.log`). Mirror scene-ID checks also passed.
- The final regular Linux build succeeded with zero build errors in 14.8 seconds
  (`Logs/combat-followup-release-build.json`). The regular native player also passed
  `-verifyGraphics` after the prefab reference cleanup
  (`Logs/combat-followup-release-graphics.log`). The build baker still logs the existing
  standalone PlacementController/NetworkIdentity validation message.
- Runtime Pipeline was enabled only temporarily for the development QA player. Project defines
  were restored; the regular build must exclude the runtime inspection server.

Temporary QA scripts/reports are in ignored Logs. Listening acceptance and mobile performance
still require target-device checks. This change does not address the separately documented
fresh-join player-health mismatch.

The initial-health mismatch was subsequently fixed and verified on fresh peers in
[FRIENDLY-FIRE-EXPLOSIONS.md](FRIENDLY-FIRE-EXPLOSIONS.md); earlier reset-based tests above are historical.
