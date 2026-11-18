# Enemy hit and attack feedback

Unity 6000.6.7f1 / URP / Mirror. Applies to SkeletonYellow, SkeletonOrange and SkeletonRed through
their shared Skeleton prefab. Ghost is not spawned by the current three-room game and is unchanged.

## Behavior and ownership

The feature is presentation only: health, DPS, navigation, hit multipliers, capture rules and trap
behavior are unchanged. Accepted bullet hits request feedback through `EnemyFeedback`; explosion
and teddy-bear continuous damage retain their existing behavior without repeatedly triggering pain.

Two-peer spawn QA also exposed an existing client-side capture-trigger race: a newly received
enemy could overlap candles before its server controller's Start disabled client simulation,
calling TurnOnWalking with an uninitialized Agent. CaptureArea enter/exit handlers now return
immediately on clients; only the server may change AI/capture state. This does not alter server
capture rules.

`PlayerShoot.CmdEnemyShoot` carries enemy-local impact position and normal alongside the existing
ID/damage. The server keeps existing damage validation, rejects non-finite/out-of-bounds cosmetic
positions, applies damage and broadcasts one feedback event. Cosmetic position rejection does not
cancel otherwise accepted damage. This is not server-side ray validation or lag compensation.

Every peer, including the shooter and host, plays the confirmed green impact once.
Pain animation starts immediately on receipt using the local unscaled clock. Mirror's snapshot
interpolation clock is intentionally not used for transient reactions: its buffer delayed client
flinches during two-peer QA. Attack events use a server unscaled timestamp and a bounded
predicted-clock catch-up, then schedule audio on the local unscaled clock. The old generic
hit-particle path remains for other surfaces, but is skipped for enemy head/body/leg hits. The local
HUD hitmarker is unchanged. Local impact coordinates are reconstructed using each peer's enemy
transform, avoiding a stale world point when the enemy moves during delivery.

## Animation

Existing Generic Mixamo bindings are reused without retargeting or modifying source FBXs:

| Asset | Source segment | Playback |
|---|---|---|
| AttackLeft | Punching 0.25–1.10 s | 1.5 s clip, existing state speed 1.7 |
| AttackRight | Punching 1.10–2.05 s | Same |
| AttackDouble | Punching 0.20–2.05 s | Same |
| Hit | Hurting 0.65–1.15 s | 0.3 s, non-looping |

Attack endpoints blend to the original guard pose to avoid loop jumps. Hips translation is held at
its original value. A BlendTree replaces only the existing Punching state's motion; existing
walk/run/scream/trap/death transitions stay. All attack variants share a duration (~0.882 s real
time), and the server chooses a different variant for the next cycle. The existing attack state is
used both against players and while attacking candles. Sounds occur at 20% of a cycle and never
apply damage.

The independent Hit feedback layer affects only Spine, Spine1, Spine2, Neck and Head rotations.
Shoulders, arms and hands retain the underlying locomotion/attack pose and follow torso motion.
The original full upper-body Hurting pose folded both hands into the chest; it was not a renderer
or dissolve failure. Removing arm curves/mask entries prevents this occlusion. No position/scale
curves are applied by the reaction, and Hips, legs and root remain excluded.
Weight fades in over 0.04 s and out over the final 0.08 s. Reactions are limited to once per 0.25 s;
movement and attacks continue underneath. Death immediately zeros the layer and stops new attack
sounds. A lethal bullet still emits its green impact but does not start a pain animation.

## Green impact budget

`EnemyHitParticles` lazily creates one scene-owned world-space Particle System. Each hit emits a
0.12-unit flare for 0.12 s and three 0.035-unit motes for 0.25 s, 0.01 units above the impact surface.
It shares the built-in Default-Particle texture used by GreenCore's KYflash, with a new URP Unlit
additive material. The green follows the existing death dissolve palette; bloom is optional.

There are no per-hit GameObjects, materials, dynamic lights, physics, trails, soft-particle depth
requests or modifications to the death shader. Maximum live particle count is 128. Scene unloading
destroys the emitter. This deliberately avoids cloning GreenCore's larger particle/light hierarchy
and avoids a local dissolve mask that would also need depth/shadow and skin-motion handling.

## Sound palette and licenses

Pain uses three short wraith shrieks. Their source is
**Vinrax: [Horror scream1](https://opengameart.org/content/horror-scream1)**, released under CC0 1.0.
No film audio is used. Each pain variant uses a different excerpt and playback rate, high-pass 260 Hz,
low-pass 8500 Hz, a baked 43/79 ms echo (0.22/0.12 decay), 12 ms fade-in and 160 ms fade-out.
Peak normalization is approximately -0.91 dBFS; RMS is -14.3 to -12.9 dBFS, 2.1–8.6 dB above the
previous files. Source selection and the broader frequency range make the voice sharper and airier.

| Mix | Source start | Rate | Final length |
|---|---|---|---|
| SkeletonHurt01 | 1.35 s | 1.09 | 0.78 s |
| SkeletonHurt02 | 2.60 s | 1.19 | 0.82 s |
| SkeletonHurt03 | 4.15 s | 1.02 | 0.76 s |

Attacks now use **iletzy: [Ghostmonster sounds](https://opengameart.org/content/ghostmonster-sounds)**
(CC0), replacing the rejected high-pitched scream-derived attack clips. These are lower, breathy
threat/growl excerpts, separate from pain, with 100–4800 Hz filtering, a quiet baked 29 ms echo,
10 ms onset and 130 ms release. RMS is approximately -14.05 dBFS; peaks stay below -0.9 dBFS.

| Attack | Ghostmonster source start | Rate | Final length |
|---|---|---|---|
| SkeletonAttack01 | 3.60 s | 0.82 | 0.68 s |
| SkeletonAttack02 | 9.90 s | 0.74 | 0.72 s |
| SkeletonAttack03 | 16.00 s | 0.88 | 0.66 s |

Files remain mono 22050 Hz, preloaded ADPCM/decompressed on load. One extra 3D AudioSource per
enemy uses linear rolloff 3–22 units and no doppler. Pain volume is 1.0 / priority 128; attack volume
0.85 / priority 150 (lower priority than pain). This replaces volume 0.65, priority 160 and 1.5–15
unit attenuation. No runtime echo/filter effects or extra simultaneous feedback voices are added.
Master Volume still applies through AudioListener; existing scream audio remains unchanged.

The server independently rate-limits pain audio to 0.9 s while retaining 0.25 s animation reactions
and an impact on every accepted bullet. Pain interrupts attack audio; attack audio cannot interrupt
a still-playing pain clip. This avoids rapid shots repeatedly cutting off the new longer shrieks.
The server chooses nonrepeating audible pain variants and pitch 0.95–1.05 for all existing peers.
Derived files are CC0 too; LICENSE.txt records the source. Raw downloads/mix diagnostics stay in
ignored Logs. Six decompressed clips occupy approximately 190 KiB of PCM sample data in total.

## Block trap lifetime

This feedback change does not alter traps. The placed Snares prefab has three catches and a
five-second freeze. Server-side `RegisterCaughtEnemy` increments its counter and despawns the
trap only at three accepted catches, releasing its building grid cell on clients. An enemy already
in Blocked state does not consume another catch. The counter tracks catches, not unique enemy IDs:
an enemy that is released and later re-enters can consume another use. This explains why some
entries leave the trap visible and the third accepted entry removes it.

## Verification and remaining acceptance

Latest attack-tone adjustment:

- The native Linux client played all three new low attack clips while receiving varied attack
  events from the Editor host (`Logs/enemy-feedback-peer-attacks.json`). This verifies playback,
  not subjective acceptance of the sound. Attack source/crop measurements are in
  `Logs/wraith-attack-low-mix.json`; pain files and timing were preserved.

Earlier wraith/arm follow-up:

- Editor tests retained arm local rotations/scales exactly in all three attack variants while the
  hurt layer had full weight; all skinned renderers stayed enabled. Repeated hits animated without
  restarting the pain voice, and an attack could not replace active pain
  (`Logs/wraith-feedback-qa.json`). An actual weapon shot still applied 40 damage and emitted four
  particles with the reaction/audio active.
- A freshly built native Linux client connected to an Editor host. Client and host weapon shots
  each produced remote feedback with the updated RPC. The client also verified all three arm
  variants, uninterrupted pain across three server hits, nonvirtualized playback and nonzero audio
  output (RMS up to 0.186, 23 sampled playing frames, ~0.748 s of a 0.781 s pain clip observed)
  (`Logs/wraith-native-qa.json`).
- Windows/macOS/Linux compile and scene-ID checks passed again; only the existing TMP editor
  harness warning remains (`Logs/wraith-feedback-compile.log`).
- The final regular Linux build succeeded with zero errors in 37.1 seconds and excludes runtime
  Pipeline (`Logs/enemy-feedback-release-build-final.json`). The regular native player also passed
  `-verifyGraphics` again (`Logs/wraith-feedback-release-graphics.log`). The build baker still logs
  its pre-existing standalone PlacementController/NetworkIdentity validation message; no new
  gameplay exceptions were observed.
- Six sample files originally totaled about 185 KiB decoded mono PCM. Their peak/RMS measurements and source
  segment mapping are recorded in `Logs/wraith-audio-remix.json`.

Earlier baseline verification:

- Editor actual shot: 1000 test HP to 960; four particles, hurt-layer weight 1 and one pain clip;
  layer/particles expired afterwards (`Logs/enemy-feedback-hit.json`). Test HP was local QA only.
- Cooldown, no consecutive pain repeats, one emitter capped at 128 particles, attack variation and
  death priority passed (`Logs/enemy-feedback-runtime.json`).
- Controlled server Update sampling while receiving feedback matched existing damage exactly:
  0.962036 observed versus 0.962034 expected across 25 sampled frames (`Logs/enemy-feedback-dps.json`).
- The green flare remained visible without bloom and showed no black rectangle
  (`Logs/enemy-feedback-no-bloom.png`). Attack/hit captures are also in Logs.
- A Red skeleton capture at one of twelve lit candles, fog density 0.1197 and exposure offset
  -0.4467 EV also retained the green impact with bloom disabled (`Logs/enemy-feedback-dark.png`).
- Windows/macOS/Linux compile and asset checks passed, including Mirror scene IDs. The existing
  TMP editor harness warning remains (`Logs/enemy-feedback-compile-final.log`).
- Two-peer QA used an Editor host and a separate native Linux client. Both real weapon paths
  produced damage, green particles, a pain clip and a positive reaction-layer weight on the
  receiving peer after the local-clock correction. The native client observed all three attack
  motions/sounds; its variant sequence matched the host's complete sampled prefix, without
  consecutive repeats (`Logs/enemy-feedback-two-peer-final.json`).
- A lethal client shot emitted four particles, left the hurt layer at zero, and despawned the
  enemy on the client after the existing death/dissolve sequence. Motion-weight inspection
  separately confirmed AttackLeft, AttackRight and AttackDouble each had full weight at their
  respective integer BlendTree values (`Logs/enemy-feedback-motion-selection.json`). All three
  colored prefabs inherited the configured component and clips.
- The pre-Start client capture-trigger regression passed with an intentionally uninitialized
  enemy Agent (`Logs/enemy-client-trigger-regression.txt`).
- Final regular Linux build: succeeded, zero errors, 13.1 seconds
  (`Logs/enemy-feedback-release-build-final.json`). Native `-verifyGraphics` passed, including
  the existing camera/weapon/effects/display and three-room candle checks
  (`Logs/enemy-feedback-release-graphics-final.log`).
- The final regular player connected to an Editor host; Yellow/Orange/Red enemies spawned
  directly at a capture area. All three registered and initialized server Agents/feedback;
  the client's previous TurnOnWalking NullReferenceException did not recur
  (`Logs/enemy-feedback-contact-peer.log`).

Runtime Pipeline was enabled only temporarily for the separate development QA player used to
inspect both peers. Project scripting defines were restored; the final regular build excludes
the runtime Pipeline server. No package or permanent debugging-setting changes are required.

A real-phone performance check and listening acceptance of the mix in a full horde remain required;
desktop runtime/source budgets do not certify mobile frame rate or perceived loudness. Network
acceptance covers existing peers; late join/reconnect and host migration are separate features.

Death voices and follow-up combat fixes are described in [COMBAT-FOLLOWUP.md](COMBAT-FOLLOWUP.md).
