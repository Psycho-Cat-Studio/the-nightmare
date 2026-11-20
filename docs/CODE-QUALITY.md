# Code quality and XML documentation

## Purpose and boundaries

This follow-up improves readability inside the existing plain Unity/C# modules. It preserves game
rules, numeric defaults, coroutine/input ordering, random draws, serialized names and Mirror wire
signatures. The previous modular refactor and its fixes are recorded in [REFACTORING.md](REFACTORING.md).
No runtime framework, formatter package or documentation generator was added.

## Review and changes

The review scanned 135 owned scripts and inspected the largest/most branched gameplay methods.
Method lengths below count physical lines, including blank lines and comments; they are review
indicators, not a cyclomatic-complexity measurement.

| Method | Before | After | Responsibility after extraction |
|---|---:|---:|---|
| PlayerShoot.Update | 113 | 21 | Ordered handling of controls and presentation |
| PlayerShoot.PerformWeaponFire | 56 | 12 | Consume, sample aim, present, trace and auto-reload |
| PlacementController.HandleKeys | 86 | 16 | Dispatch pending/FPS/building controls |
| PlacementController.MoveToMouse | 50 | 30 | Find a candidate and choose preview visibility |
| PlayerEquipment.Update | 58 | 6 | Local interaction ray and dispatch |
| GameManager.Awake | 46 | 9 | Session initialization and composition |
| GraphicsVerification.Run | 202 | 17 | Ordered diagnostic steps |
| ContentValidation.ValidateBuildContent | 54 | 17 | Open/validate/close the authored scene |

Shoot handlers deliberately fetch the current weapon at their operation boundary. A swap coroutine
changes active objects before its first yield, so one cached weapon for the entire Update would
change behavior. Aim is captured before this shot adds recoil. Ammunition is spent before feedback;
auto-reload remains after hit processing. Burst count/interval, both crosshair threshold comparisons
and the grenade's last-frame charge overshoot remain unchanged.

Placement has one cyclic inventory-selection rule for both directions. HUD iteration follows the
mapped inventory/view lengths rather than a literal three. Existing three-item behavior remains;
a regression checks the fourth mapped slot. Candidate selection, occupied-cell rejection,
acknowledgement and the two-tile horizontal range retain their roles.

Match bootstrap and legacy enum mapping are isolated. Spawn minimum/maximum bounds are authored
in MatchSettings: minimum 1 s; upper bounds 8/6/4 s for one/two/three active players, and 3 s for
other counts, including zero. Unused older 1/5-second fields were removed after reference checks; the separate 20-second
WaitForSpawn setting remains unchanged. Bear target cleanup removes invalid entries without a
per-frame ToList allocation and preserves surviving target order and duplicate semantics.

Graphics diagnostics use synchronous foreach flattening of IEnumerable steps. Exceptions still reach
the original outer catch/Restore path, and existing waits remain at the same points. The single-shot
probe temporarily enables its controller synchronously because it otherwise calls firing while
CanControl is false; it restores that fixture state afterward. This changes the diagnostic fixture,
not gameplay control gating.

## Constants and configuration

- CombatRules owns head/body/leg multipliers 3/2/1 and the shared maximum-damage multiplier.
- GrenadeThrowRules owns charge conversion and server bounds; forward/upward speeds remain 15/5.
- WeaponHandlingSettings groups swap, ADS, hitmarker and crosshair tuning on the player component;
  defaults match the old literals. Boy/Girl prefabs explicitly serialize these values.
- First-person swap poses use the existing WeaponDefinition values. Legacy pose fallbacks remain
  for unmigrated content. Custom definitions can now supply their authored swap pose.
- GameTags/GameInput and GraphicsPreferenceKeys name repeated identifiers. Preference strings,
  input mappings and tag values are unchanged; names are not a persistence migration.
- RoomLighting names its interpolation, ambient, exposure and fog bounds without changing equations.

The camera edge-scroll field was described as a percentage but used as a pixel threshold. Its value,
serialized name and comparisons are preserved; the comment now states the actual legacy units.
Converting it to a screen fraction is a separate behavior change.

## Selective XML documentation

Use English `///` XML comments on meaningful public contracts, operations and non-obvious properties.
Describe meaning, ownership, units, lifecycle, side effects and failure/return conditions. Important
examples include loaded versus total ammo, cancelled reload events, phase transitions, readiness,
authority, target-local hit coordinates and placement acknowledgement.

Prefer a short summary. Add param/returns/remarks/exception tags only when they clarify a real
contract; use cref/inheritdoc for relevant references. Obvious getters, standard Unity callbacks,
trivial forwarding helpers and Equals/GetHashCode do not need boilerplate. Document the reason for
non-obvious ordering in selected private code. Comments must reflect the implementation rather
than promise stronger validation, authority or timing guarantees.

`python3 Tools/check_xml_docs.py` parses owned-script documentation blocks as XML. It rejects
malformed markup, but does not resolve cref targets or require documentation on every public member.
The license-free compile workflow runs it alongside module and asset checks. No documentation
coverage quota is imposed. Equal numeric literals are not automatically one setting: independent
input, recoil and crosshair reference rates remain scoped to their respective mechanics.

## Remaining exceptions

A method around 40–50 lines or more than three nesting levels warrants review, not automatic
fragmentation. The candle-lighting diagnostic retains a cohesive larger try/finally scope for
restoring scene state. UI layout factories, explicit state switches and old experimental-scene
fallbacks are not replaced with new abstraction layers merely to reduce line counts.

Legacy public names and several compatibility fields remain because UnityEvents, tools or old
assets can refer to them. Main composition/preview components still coordinate Unity-facing work;
this pass does not claim every script has been rewritten or every numeric literal removed.
The older revive pose-by-ID path and weapon audio selection by legacy ID remain follow-up work;
this pass migrates the swap pose without expanding player-life or audio behavior changes.

## Previous readability-pass verification

Baseline before these edits: 45 EditMode and 17 PlayMode cases passed. Added characterization covers
charge endpoints/overshoot and server bounds, cyclic inventory selection, spawn branches, unchanged
handling defaults, configured swap pose, interrupted swaps and the fourth HUD slot. Pose testing
isolates animation/presentation so an animated transform cannot mask the assignment under test.

Verified locally on 2026-11-20:

| Check | Result | Ignored local evidence |
|---|---|---|
| EditMode | 70 passed, 0 failed | `Logs/quality-edit-verified.json` |
| PlayMode | 20 passed, 0 failed | `Logs/quality-play-verified-status.json` |
| Script/asset compile checks | Windows, macOS, Linux pass; known TMP editor harness limitation reported separately | `Logs/quality-compile-verified.log` |
| XML | 135 blocks parse; default-assembly documentation compilation has no XML/reference/parameter diagnostics | `Logs/XmlCheck/compiler.log` |
| Development Linux build | Succeeded, 0 errors | `Logs/quality-dev-build.json` |
| Editor host + native client | Passed | `Logs/network-regression-20261120-120348/result.json` |

The network run confirms one round consumed, GhostOrange 120 → 40 HP on a body hit, player 20 HP
with overlay alpha 0.09, completed dissolve/despawn, placement acknowledgement, duplicate rejection
without spending inventory and cell release on both peers. Movement remains about 1.50 m per 0.6 s.
The first pose test observed an animation-driven transform rather than the assignment; its fixture
now disables/restores animation and presentation while testing the configured swap pose.

Mobile/device and adverse-network acceptance remain outside this desktop readability pass.
Normal Linux release build succeeded with zero errors (`Logs/quality-release-build.json`).
The native release graphics scenario exited 0 and passed all 15 checks
(`Logs/quality-graphics-result.json`): camera stack/single shot, effects, AA/AO, quality switching,
GPU exposure/bloom, previews, 640x360 confirmation/reapply/rollback and candle lighting across all
three rooms, including victory reset.

A first Flatpak launch used the home directory as its working directory and exposed a diagnostic
output-directory assumption. Diagnostics now create Logs before taking screenshots. The complete
run was performed directly on the host from the project directory; normal gameplay was not changed.
An attempted rebuild during pending script compilation returned Unknown and was not treated as a
successful build; the final build waited for a stable import/compilation state.

In that earlier pass, final XML additions were documentation-only. Its sole code change after the gameplay suites was
creation of the opt-in diagnostic output directory. No commits or pushes were made. Existing local
cloud-material, Rider-plugin and recovery changes remain outside this follow-up.

Final release recheck after documentation edits: Succeeded, 0 errors, 14.6 seconds.


## Follow-up: generator, class contracts and dead code

BuildingPointsGenerator is a legacy edit-mode authoring tool referenced by fpsTest4, outside the
three build scenes. Its Awake now reads as an ordered rebuild, with separate room, grid, capture
and spawn operations. BuildingGridLayout holds the engine-free geometry. Its two small nested
loops deliberately express the two grid axes and preserve integer half-extents, repeated floating
increments and x-major traversal. Replacing them with LINQ would hide the geometry.

RoomHierarchy shares scene-root identifiers with match bootstrap. Resource paths belong to the
generator when only it uses them; matching text does not make a resource key and a hierarchy name
one contract. Shared GameScenes, GameTags, GameLayers, IndicatorSprites and AnimationParameters
also replace repeated scene, collision, layer, sprite and player-animation identifiers. Display labels
remain inline where they are neither lookup keys nor configuration.

Two authoring defects were corrected: clearing a canvas previously skipped every second child
because DestroyImmediate shifted indices, and optional chest children invalidated room root indices.
Generated roots are now bound explicitly. Invalid grid spacing fails rather than hanging the Editor.
These are authoring fixes; shipped campaign spawning and placement rules are unchanged.

Every owned class, including editor utilities and regression fixtures, now has a short XML summary
covering its role. Important public-member documentation remains selective. The hygiene check requires
class summaries, rejects obvious commented-out statements and empty Unity frame callbacks. This is a
source heuristic, not a full dead-code analyzer or a guarantee of simple control flow.

EnemyDamage was empty; CapturedRooms only cached and computed unused data. Their four serialized
references were removed through Unity before deleting both scripts and their meta files. Unreferenced
no-op APIs, unused fields/imports and commented-out implementations were also removed after source,
UnityEvent and serialized-reference checks. Existing migration compatibility entry points remain when
referenced. In particular, the discarded GetStartPosition result in revival still calls Mirror:
that call advances spawn bookkeeping/RNG even though revival does not teleport there.

New regression cases cover grid extents/order, invalid spacing, complete child cleanup and generated
room setup with and without a chest. Test objects use an isolated preview scene, so an unsaved scene
created by Unity Test Runner does not block test setup or become part of the fixture.


Follow-up validation on 2026-11-20: 81 EditMode and 20 PlayMode cases pass, including the 11 new
generator cases. The source checks find XML summaries on all 150 owned classes and parse 274 XML
blocks. XML coverage does not measure comment usefulness or architectural completeness; this review
still leaves Unity authoring helpers and compatibility code in place where they have real references.

The follow-up Windows/macOS/Linux compile and asset checks pass (`Logs/cleanup-compile-final.log`);
the existing TextMeshPro editor harness warning remains separate from owned-code compilation.
A fresh Linux QA build succeeded with zero errors (`Logs/cleanup-dev-build.json`). The two-process
run (`Logs/network-regression-20261120-125803/result.json`) passed on both peers: one round spent,
body-hit health 40, player health 20 with overlay alpha 0.09, dissolve/despawn, placement, duplicate
rejection and cell release. The fixture's native player remains a disposable development build;
its Flatpak working-directory player-preference warning is unrelated to this source cleanup.

The final normal Linux release also succeeded with zero errors (`Logs/cleanup-release-build.json`,
53.9 seconds). Editor Play Mode is stopped and runtime QA defines are restored. No commit or push
was performed as part of this follow-up.


## Follow-up: explicit match ownership

GameManager's responsibilities moved to four cohesive owners rather than partial-class fragments.
MatchSession owns progression/readiness/time; MatchWorldRegistry owns authored indices; spawning
owns its coroutine; the player coordinator owns session membership and campaign entry. Read-only
registry views prevent callers from editing dictionaries directly. GameManager remains responsible
for the visible ordering of multi-module operations. See ARCHITECTURE.md for dependency wiring.

Named predicates now express active-stage capture, successful stage completion, spawn cutoff,
readiness eligibility, enemy-contact eligibility, placement validation and attack-cycle eligibility.
Short null/lifecycle guards remain inline when extracting them would obscure the operation. Existing
short-circuit ordering, random selection, cadence, health/ammo values and movement are preserved.
The two recursive layer-setting implementations use one TransformLayers utility.

The old numbered-room map and implicit three-room configuration were removed, including their test
and diagnostic callers. The architecture checker rejects reintroduction of the removed singleton,
static registry API and numbered-state compatibility API. This prevents a disguised replacement
singleton while keeping the current backend/menu service boundary explicit.


The match root now has 175 physical lines (previously 616). This is a review aid, not an enforced
class-size quota. All 157 owned classes have summaries and all 296 XML blocks parse. The final
EditMode/PlayMode counts are 92/24, including the baseline failures and regression cases described
in REFACTORING.md. Existing game balance remains; successful-stage horde retirement and inactive-area
protection are the explicitly requested gameplay corrections.
