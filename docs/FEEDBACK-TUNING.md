# Audio, damage and candle-alarm tuning

Follow-up to COMBAT-FOLLOWUP.md and PLAYER-DAMAGE-FEEDBACK.md.

## Death audibility

The death clips were assigned on all three enemy variants, but the original mixes were quiet
(-17 dBFS RMS, source volume 0.6 / priority 128), so gunfire and a busy 32-real-voice mix could mask
or virtualize them. Merely observing isPlaying did not establish audibility. A 40-source
pressure fixture also exposed transient virtualization when death priority was assigned after
Play. Priority, level and attenuation are now configured before starting the clip.

Clips retain their 0.70/0.80/0.85 s lengths and CC0 source. They now measure approximately
-12.0/-12.2/-13.6 dBFS RMS, with peaks below -1 dBFS. Death voice volume is 0.9, priority 64,
linear attenuation 5–24 units. The higher priority takes precedence over ordinary weapon and
enemy voices without raising the global voice limit. Existing scream/pain/attack voices still
stop on death, while the death voice is allowed to finish. No extra death source or DSP is added.

## Rifle body without long echo

The 75 ms crop removed too much of the shot's body. The same original recording now supplies
145 ms with a +5 dB low shelf near 170 Hz and a 70 ms release, normalized to -2 dBFS peak.
Bass below 220 Hz relative to treble above 800 Hz rises by about 3.5 dB versus the 75 ms version.
This retains a low-frequency body rather than restoring the original 2.3-second reverberant tail.

Two reusable AudioSources alternate, using Stop/Play on reuse. At the existing 80 ms cadence,
short bodies may overlap naturally, but the budget is strictly two voices per shooter even if
RPCs arrive bunched. Both bypass Reverb Zones and use 90% of the old source volume. Shared reload,
pickup and hitmarker sounds remain independent. One shared mono PCM clip occupies ~12.5 KiB;
no runtime equalizer or reverb is introduced.

## HP-scaled red pulse

Intensity depends on remaining health after the hit, not the size of that hit:

`opacity = 0.04 + 0.66 * (1 - clamp01(currentHealth / maxHealth))²`

| Remaining HP out of 100 | Peak opacity |
|---|---|
| 99 | 0.040 |
| 75 | 0.081 |
| 50 | 0.205 |
| 25 | 0.411 |
| 10 | 0.575 |
| 1 | 0.687 |

Each pulse holds for 0.12 s then fades proportionally over 0.55 s. Continued damage refreshes the
pulse at the updated health level. Low HP alone does not create a permanent red overlay. Healing,
death, reassignment and menus retain the previous clearing rules; the center remains transparent.

## Alarm ownership and reset

CapturePointsUI previously latched hasPlayed forever and never stopped its looping AudioSource
on setRoom. It also retained red backgrounds from the previous room. This was the direct cause
of the old room's siren continuing into the next room.

The HUD caches its original sprites in Awake. Every setRoom stops audio immediately and restores
all backgrounds before binding the new area's list. Update checks only the current room and only
Room1Fight/Room2Fight/Room3Fight. Any area below 20% activates the existing single looping source;
recovery to 20% or above, preparation, win, loss and disabling the HUD stop it. Critical icons are
computed separately per area. Each room can independently trigger its own warning.

## Verification

- Editor HP test: measured opacity 0.040/0.081/0.205/0.411/0.575/0.687 at
  99/75/50/25/10/1 HP, followed by a full fade. Alarm tests covered all three rooms,
  immediate room-transition stop, independent activation in room 2, the strict <20 threshold,
  recovery, win and loss, and per-area warning sprites (`Logs/feedback-hp-alarm-qa.json`).
- Editor real lethal rifle shot under pressure from 40 additional priority-128 AudioSources:
  the death voice stayed nonvirtual and produced output RMS up to 0.169 across 18 samples.
  The initial pressure fixture did expose transient virtualization before configuration was
  moved ahead of Play; the corrected fixture passed (`Logs/feedback-audio-output-qa.json`).
- Native Linux client + Editor host: replicated low room-1 candle progress started the alarm,
  Room2Prepare stopped it, and low room-2 progress independently restarted it. This used the
  real server phase RPC and dirty SyncVars (`Logs/feedback-native-alarm-qa.json`).
- Replicated player damage on the native client produced opacity 0.040 at 99 HP and 0.575 at
  10 HP (`Logs/feedback-native-hp-qa.json`). QA initialized player health via the existing
  SetDefaults path and does not establish fresh-join health correctness.
- Native actual weapon test: a 12-round series used the two 145 ms voices and left no playing
  tail after stopping. Its next shot killed an enemy through the normal Command path.
  The death voice produced RMS up to 0.163, stayed nonvirtual under 40 additional sources,
  and retained priority 64 / volume 0.9 (`Logs/feedback-native-audio-qa.json`).
- Windows/macOS/Linux compile and asset/scene-ID checks passed; the existing TMP editor
  harness warning remains (`Logs/feedback-tuning-compile-final.log`).
- The regular Linux build succeeded with zero build errors in 31.2 seconds
  (`Logs/feedback-tuning-release-build.json`). The regular native player also passed
  `-verifyGraphics` (`Logs/feedback-tuning-release-graphics.log`). The build baker still logs the pre-existing
  standalone PlacementController/NetworkIdentity validation message.
- Runtime Pipeline was temporarily enabled only for the separate development QA player and
  then removed from project defines. The regular release build excludes the inspection server.

Logs and temporary QA scripts remain in ignored Logs. Listening and mobile-device acceptance
remain necessary: source meters do not prove perceived sound quality on every speaker or headphone.

The initial-health mismatch was subsequently fixed and verified on fresh peers in
[FRIENDLY-FIRE-EXPLOSIONS.md](FRIENDLY-FIRE-EXPLOSIONS.md); earlier reset-based tests above are historical.
