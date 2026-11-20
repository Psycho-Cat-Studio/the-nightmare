# Ghost variants and combat presentation

Unity 6000.6.7f1, URP and Mirror. This change keeps the existing enemy state machine,
server authority, spawn cadence and weapon/explosion damage rules.

## Ghost progression

Each server spawn independently chooses Ghost with probability **20%**, otherwise the existing
skeleton. This is a probability, not a quota per wave. The room chooses the corresponding prefab.

| Room | Ghost HP | Contact damage/s | Walk speed | Charge speed |
| --- | ---: | ---: | ---: | ---: |
| 1 | 60 | 2 | 2.8 | 4.9 |
| 2 | 120 | 6 | 2.8 | 4.9 |
| 3 | 180 | 10 | 2.8 | 4.9 |

Skeleton HP remains 100/200/300, contact damage 2/6/10, and speed 2/3.5. Ghost trades health
for 40% faster movement, not more damage. It follows the NavMesh, flies over brick traps, and uses the
same initial spawn-area preference and later nearest-candle retargeting as the skeleton.
The existing occasional 2.2-second charge preparation is retained, using Ghost idle motion and
its own voice. No damage is added during that preparation.

The existing Ghost controller was empty and its old prefab was incomplete. The new prefab uses
the existing enemy components with Ghost's rig and its own idle/run/attack clips. Walking
and charging play the run clip at 0.8/1.12 speed; its full attack plays over 1.2 seconds. Death now holds the current pose, flashes green and dissolves over 1.2 seconds.
A masked override hit layer recoils the spine/back/head for 0.3 seconds, matching the skeleton's
feedback mechanism while using Ghost's own rig. Parent torso bones and head are enabled in the
mask; arms/tail are excluded and unanimated channels do not reset to bind pose. The spine/back/head
recoil peaks at roughly 8/7/14 degrees, with existing hit cooldowns and no navigation/attack stun.
Attack timing/state/variant count are serialized in
`EnemyFeedback`, with the original skeleton defaults retained. Tier materials tint the same
Ghost texture yellow, orange and red. They reuse the existing URP dissolve shader, with a small
128×128 noise mask rather than the skeleton's unrelated UV atlas.

The rigged FBX has missing body UVs. `GhostSkinnedMesh.asset` transfers UVs from the repository's
original OBJ, matching rest-position vertices after converting OBJ centimeters to meters.
All 111,164 rigged vertices found exact position matches. The repaired geometry is now split
into a 47,972-vertex body and 63,192-vertex eye mesh. Geometry, normals, bone weights and
bind poses remain unchanged; the FBX and OBJ are preserved. The zero-UV eye geometry is expected
and does not indicate missing body UVs. A separate skinned eye renderer reuses the existing
SkeletonEyesYellow/Orange/Red emissive materials, follows the same bones and hides when dissolve
begins. This adds one renderer/draw per Ghost, not particles or dynamic lights. The inherited model is relatively dense (63,042 triangles total);
mobile performance still needs device profiling rather than an assumed frame-rate guarantee.

Health initializes in `OnStartServer` before initial spawn serialization. `Start` no longer
writes HP after replication. Death hides explicitly referenced objects instead of assuming the
skeleton's eye hierarchy. All Ghost variants are registered in the shared NetworkManager prefab and the lobby scene's
spawn-list override. Obsolete `m_SpawnPrefabs` UNET alias overrides were removed so they cannot
truncate or clear the current `spawnPrefabs` array after a scene reload.
Host and client must use builds with the same updated network behaviours/prefabs.

## Ground contact, traps and death

Ghost's EnemyLegs capsule now reaches the floor (center Y 0.22, height 0.48, radius 0.22 before
root scale). Its **kinematic Rigidbody is on the same child as the capsule and TrapsHandler**,
with gravity off. This supplies trigger interaction with the shallow candle area and keeps Unity's
trigger callbacks on the existing trap handler. A Rigidbody on the root instead routes those
callbacks away from that child. The visual hovering pose stays independent of the floor contact.

Ghost variants serialize `AffectedByBricks = false`: touching a brick mat neither freezes Ghost
nor changes its animation, and never spends a usage charge. The server checks immunity before
starting any trap response. The former `_consumesTrapCharges` serialized flag migrates to
`_affectedByBricks`, preserving existing prefab values. Skeletons retain the default true value,
normal freeze and existing three-use durability. Trap handling explicitly checks server authority and
ignores dying enemies; it caches its enemy in Awake before physics callbacks.

CaptureArea checks both trigger entry and stay, registers each enemy once, and waits for Running
before capturing. A scream or trap freeze overlapping the area therefore cannot permanently lose
its entry event; capture starts after recovery. Chasing a player still respects IsTriggerLocked.
Leaving an area while dying no longer restarts the NavMeshAgent.

Client Animator, audio and NavMesh references initialize in Awake so an early spawn RPC cannot
lose the brick-freeze animation before Start.

## Spectral death

Ghost holds its current pose instead of falling. The server stops movement and disables damage
colliders immediately. Each peer restarts Ghost's existing GreenCore spawn particle hierarchy,
clearing any previous spawn particles first. The same green core, circle, flash and wisps surround
the dissolving body. Delayed subeffects start immediately and particle simulation runs at 1.5×,
so the visible particle tail fits the roughly two-second presentation. Normal spawn settings are
unchanged; only the dying instance is retimed. Existing particle systems/materials are reused,
without new particle GameObjects or additional lights. The earlier seven-particle blink is removed.

Dissolve begins after 0.06 s and takes **1.2 s**, over five times longer than the preceding 0.22 s
version but still faster than the skeleton's 3 s dissolve after its 3.2 s fall. Eyes hide as dissolve
begins. The invisible actor remains another 0.8 s for particle/audio tails before server despawn.
Dissolve initializes in Awake so an early RPC cannot be reset by Start. Skeleton settings stay intact.

The generated Ghost fall clip and floor-alignment raycast are retired; original FBX source animation
remains. `_groundDeathVisual` migrates to `_spectralDeath` for existing prefabs. Existing hit/death
RPCs and damage validation are unchanged; updated peers need the current local presentation.

## Ghost voices

Separate from the skeleton's recordings: [Ghost Monster Voice Moaning & Growling by qubodup](https://opengameart.org/content/ghost-monster-voice-moaning-growling), CC0.
Sources and license also accompany the audio in `Assets/Audio/Enemies/Ghost/LICENSE.txt`.
Mono PCM at 22,050 Hz, 180–6500 Hz filtering, slight rate variation, quiet 43 ms echo, fades and
normalization; no new runtime DSP. Three clips per hit/attack/death, avoiding immediate repeats,
and one charge clip. Existing spatial rolloff, voice priority and hit/death precedence apply.

| Output | Source GhostMoan | Start (s) | Source length (s) | Rate |
| --- | ---: | ---: | ---: | ---: |
| Hurt 01/02/03 | 01/03/04 | 0.35/0.8/0.6 | 0.62/0.68/0.72 | 1.07/1.02/1.10 |
| Attack 01/02/03 | 02/04/05 | 0.45/1.7/0.15 | 0.75/0.8/0.65 | 1.10/1.05/1.12 |
| Death 01/02/03 | 01/03/04 | 1.1/2.2/3.2 | 0.9/0.95/0.85 | 0.98/1.04/1.00 |
| Charge 01 | 02 | 2.0 | 1.8 | 1.04 |

## Explosion proportions

Both effects retain their original particle animation, sound and three-second lifetime.
A shared relative scale is `1.5 × worldRadius / 3.52`, applied to their authored scale of 0.3.
Grenade scale is **0.45**; trash-can scale is **0.191761**. Both are 20% larger than the preceding
revision (grenade is now 50% larger than the original authored effect). This gives the smaller trash-can explosion the same visual-size/radius ratio, rather than making
both animations equally large. Damage radii remain 3.52/1.5 meters. The independent floor ring
compensates for visual scale and still has exactly the damage radius. The eight-object pool is
unchanged; no extra lights, particles or audio voices are added.

## Rejected placement

Visible local previews flash red twice over 0.35 seconds alongside the existing rejection sound.
A shared URP Unlit red material temporarily replaces preview material slots; original material
arrays are restored afterwards, on cancellation and when changing items. Placed/network objects
are never recolored and no per-flash materials are instantiated.

A submitted preview remains at its requested pose while awaiting the server; movement, selection
and duplicate submission are blocked. T can cancel the preview while the outstanding result still
settles. Acceptance removes the preview and decrements inventory; rejection restores the grid
and flashes the preview without spending inventory. Out-of-range previews remain hidden.

## Verification

### Initial implementation checks (before later tuning)

- Editor: 10,000 selections per room yielded 2,008 / 1,983 / 2,032 Ghosts, with no cross-tier choices.
- All three Ghosts spawned at 60/120/180 HP and walk speed 2.8. Sampled tail/spine poses advanced;
  attack, hit-layer visibility, voice playback, death audio and network despawn after dissolve passed.
- Five explosion replays per type retained eight pooled objects, original clips and the same
  scale/radius coefficient (0.106534); floor-ring world radii stayed exact.
- Preview pulse samples were red/original/red/original/original; all original materials returned.
- Fresh native Linux client + Editor host: all three Ghosts and the existing skeleton spawned
  with matching identities and 60/120/180/100 HP. The client used the repaired mesh and attack states.
  Its weapon Command dealt 20 damage to the 120-HP Ghost and played a Ghost hurt voice.
- A charging Ghost moved 1.88 m in the sampled interval at configured speed 4.9, was caught once
  by an actual networked brick trap and resumed running after release. Client movement/animation
  followed the server (`Logs/ghost-host-interactions.json`, `Logs/ghost-peer-client-moved.json`).
- An actual server grenade killed the wounded Ghost; the client observed its death animation,
  Ghost death voice and final despawn. Actual grenade/trash-can RPCs produced radii 3.52/1.5,
  scales 0.375/0.159801, matching ring radii and original `boom`/`explosion` clips on the client
  (`Logs/ghost-client-explosion-watch.json`).
- After host placement, a client request for the occupied cell was rejected; inventory stayed
  at three, the rejection timer activated and original preview materials returned
  (`Logs/placement-peer-client.json`). No spawn/exception errors occurred in the verified client log.
- Windows/macOS/Linux compile checks and saved Mirror scene-ID checks passed. The existing TMP
  editor harness warning remains. Runtime QA scripts, reports and captures are in ignored `Logs`.

- Final regular Linux build succeeded with zero build errors in 37.0 seconds
  (`Logs/ghost-release-build.json`). Runtime Pipeline defines were restored after the development
  QA build; the regular build excludes the runtime inspection server. Editor returned to the
  unchanged MainMenu scene outside Play Mode. The existing standalone PlacementController
  prefab validation message remains outside this change.

### Earlier grounding checks (superseded by spectral death)

- All three Ghost variants physically entered a candle area, played the attack state and reduced
  its progress. The existing skeleton passed the same check (`Logs/ghost-candle-qa.json`).
- Four actual walk-through/freeze/recovery cycles per Ghost variant used zero brick charges;
  a skeleton used one in the same trap (`Logs/ghost-traps-progress.json`).
- Actual Animator death: lowest body point 2.9237 m against floor 2.9125 m (about 1.1 cm above).
  Early death did not jump upwards; eyes were present during falling and hidden for dissolve
  (`Logs/ghost-trap-death-qa.json`). A gameplay capture confirms the three glowing eye colors.

- Updated native Linux client + Editor host: the warmed gameplay scenario showed trap freeze,
  subsequent candle capture (100 → 97.09), zero consumed brick charges, matching emissive eyes,
  death/dissolve and despawn on both peers (`Logs/ghost-ground-client-watch-verified.json`).
  A second run checked the held final death pose on the client: lowest body point differed from
  the actual floor by -0.0025 m (about 2.5 mm), with trap freeze, capture, eyes and despawn passing
  again (`Logs/ghost-ground-final-pose-watch.json`).
  The updated Windows/macOS/Linux compile checks passed (`Logs/ghost-floor-compile-final.log`),
  with the existing TMP harness warning.

- Follow-up regular Linux build succeeded with zero build errors in 35.6 seconds
  (`Logs/ghost-floor-release-build.json`). Runtime inspection defines were restored and generated
  test settings removed. Existing material normalization from the build was reverted; the shared
  eye materials retain their original authored values. MainMenu is clean and Play Mode is stopped.

### Current brick immunity and explosion-size correction

- All three Ghost variants each crossed the same mat four times without entering Blocked,
  playing the blocked animation or spending a charge. The skeleton still froze and spent one
  charge in the same test (`Logs/ghost-fly-qa.json`).
- Five replays per explosion type retained eight pooled instances, original clips/particle
  animation and exact 3.52/1.5 m ring radii. Visual scales were 0.45/0.191761: +20% over the
  preceding revision and the same scale/radius coefficient of 0.127841.
- Earlier freeze-without-consumption reports above describe superseded Ghost behavior.
- Current Windows/macOS/Linux compile and scene-ID checks passed (`Logs/ghost-fly-compile.log`);
  the regular Linux build succeeded with zero errors in 35.4 seconds
  (`Logs/ghost-fly-release-build.json`).

### Earlier 0.28-second death and recoil checks

- All three Ghost variants showed 6.5–6.9 degree back recoil and 12.9–13.8 degree head recoil
  in the sampled hit frame. Arm local rotation stayed unchanged; the hit layer returned to zero.
- Death emitted seven particles, froze the current pose, advanced dissolve immediately and hid
  the body within 0.4 s. The death voice still played afterwards and the invisible actor despawned
  within about 1.2 s (`Logs/ghost-spectral-qa.json`).
- The skeleton retained its animated fall and original dissolve delay. Gameplay captures show
  the viewer-facing flash and fast dissolve (`Logs/ghost-death-flash.png`, `Logs/ghost-fast-dissolve.png`).

- Native Linux client + Editor host: the client's weapon Command dealt 20 HP (120 → 100),
  visibly changed torso/head bone rotations and preserved arm rotation. The client observed
  the death flash, completed fast dissolve, frozen pose, uninterrupted death voice and server
  despawn (`Logs/ghost-spectral-client-qa.json`). No spawn/exception errors appeared in its log.
- Windows/macOS/Linux compile and scene-ID checks passed (`Logs/ghost-spectral-compile-final.log`);
  the existing TMP editor harness warning remains.

- The preceding regular Linux build succeeded with zero build errors in 34.6 seconds
  (`Logs/ghost-spectral-release-build.json`). Runtime inspection defines/settings were removed;
  MainMenu is clean and Play Mode stopped. The pre-existing standalone PlacementController
  NetworkIdentity validation message remains outside this change.

### Spawn-style death timing revision

- Editor-host checks passed for Ghost, GhostOrange and GhostRed: the same five spawn particle
  systems were reused, with 23 particles sampled shortly after death, zero start delays and 1.5×
  simulation speed. The Animator stayed disabled and the back bone did not move.
- Dissolve advanced through 24–34% and 59–67% in the two intermediate samples, completed before
  1.4 seconds and despawned after its tail. No extra particle systems were created
  (`Logs/ghost-spawn-death-qa.json`). Gameplay captures show the spawn-style flash and partial
  dissolve (`Logs/ghost-spawn-death-flash.png`, `Logs/ghost-spawn-death-mid.png`).
- Windows/macOS/Linux compile and scene-ID checks passed; the existing TMP editor harness
  warning remains (`Logs/ghost-spawn-death-compile.log`). This presentation retune was checked
  on the Editor host; the two-peer checks above refer to the earlier timing/effect revision.

- Regular Linux build succeeded with zero build errors in 25.8 seconds
  (`Logs/ghost-spawn-death-release-build.json`). No new console errors appeared apart from the
  previously documented unused PlacementController prefab validation message during the build.

Listening acceptance and real-phone profiling remain manual checks.


### Later default-radius tuning (2026-11-20)

The earlier 3.52/1.5 m radii and 0.45/0.191761 scales recorded above are historical. Both item
prefabs now default to 2.5 m and scale approximately 0.319602, with independent per-prefab tuning.
The authored reference-size formula, animation and audio remain unchanged. See
[matching default blast sizes](FRIENDLY-FIRE-EXPLOSIONS.md#matching-default-blast-sizes-2026-11-20).


## Additive levitation (2026-11-20)

Ghost's existing idle, run, attack and hit clips have valid rig bindings. The agreed change keeps
those clips and arm poses, adding one Levitation pivot between Graphics and ghost_rigged_model.
Authored collider positions and Animator references were checked before/after reparenting; all clip
bindings remain relative to the same Animator root. GhostOrange and GhostRed inherit the pivot and
EnemyFeedback reference from Ghost; skeleton prefabs have no levitation component.

GhostLevitation lives in Nightmare.Runtime and only presents an absolute sample. EnemyFeedback
supplies NetworkTime.time and the entity netId during LateUpdate, before its attack-audio early
returns. A deterministic fractional seed spaces phases without consuming UnityEngine.Random, so
spawn selection and audio randomness are unaffected. There are no new RPCs or NetworkBehaviours.

| Serialized parameter | Default | Meaning |
|---|---:|---|
| Vertical travel | 0.08 m | Upward-only peak-to-peak motion from the authored pose |
| Lateral amplitude | 0.015 m | Maximum displacement to either side |
| Roll | 2 degrees | Maximum sideways tilt |
| Pitch | 1 degree | Maximum forward/back tilt |
| Frequency | 0.5 Hz | One two-second cycle across all living animation states |

World-space offsets are converted through the parent transform, accounting for Ghost's 1.2 scale.
Each sample starts from cached local position/rotation, preventing accumulation. Root navigation,
body/leg colliders and minimap marker stay fixed; the existing bone-attached head collider and eye
skin follow the visual rig. The hurt layer continues independently. No root motion, rig-bone edits,
new renderer, material, light, particle system or per-frame allocation is introduced.

Normal death freezes the current pivot before the existing spectral flash/dissolve. Stage retirement
also freezes through the existing dying flag on every peer. Neither path restores the base pose
while visible; disabling the component restores the authored pivot for teardown/reuse.

Five EditMode cases cover world bounds, parent-scale independence, absolute/periodic sampling,
freeze/reset and gameplay RNG preservation. The PlayMode fixture samples all three variants in
Waiting, Walking, Running, Screaming and Punching, validates the existing hit layer and impact
bounds, and checks root/body/leg stability and frozen death pose. The final-victory case also checks
retirement freeze. The native peer harness samples live bobbing with a stationary navigation root,
checks a physical head ray and impact bounds, then exercises the existing body-hit/death flow.

Verification: 97 EditMode and 27 PlayMode tests passed, including the two physical chain cases.
The native client measured 0.0167 m of visual movement with zero navigation-root movement and
a targetable head; existing body-hit, hurt overlay, placement and retirement checks also passed.
Both chain scenarios replicated seven unique effects, each once, and all spawned items despawned
on both peers (`Logs/network-regression-20261120-154842/result.json`). Windows/macOS/Linux
compile checks passed (`Logs/ghost-motion-compile.log`); the pre-existing TMP editor-harness
limitation remains. Subjective motion intensity and phone performance remain manual acceptance.

The regular Linux build succeeded with zero errors (63.4 seconds), with runtime QA defines absent.
Its native graphics verification passed all 15 checks (`Logs/ghost-motion-release-build.json`,
`Logs/ghost-motion-native-graphics-result.json`). The Editor was left stopped in a clean MainMenu
scene; temporary test actors and auto-tick were removed/disabled.
