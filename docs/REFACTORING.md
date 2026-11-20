# Modular refactor: decisions and verification

## Contract

Use plain Unity/C#, explicit constructor/component initialization and no DI container. Keep
Mirror/KCP and the host-client topology. Prepare seams for another backend, relay/matchmaking and
solo, without implementing those features. Preserve movement, combat rules, room order/timings,
weapon modes, **31/155 rifle ammunition**, full-reserve-magazine HUD icons and **20% Ghost weight**.
Balance/feel changes require a separate decision. Third-party code and pre-existing local
asset/editor changes are excluded.

## Baseline before production edits

Regression fixtures were added before extracting gameplay. The initial EditMode characterization
covered authored weapons/enemies, ammunition, reload, fire modes, room transitions, address parsing
and missing scripts. **22 baseline EditMode cases passed.** The first PlayMode run exposed a real
camera-reference failure during host teardown; later fixtures exercise repeated host lifetimes.

The pistol originally supported B-cycle single → triple → continuous and started single; an early
test expectation forbidding its alternate modes was corrected to match the authored behavior.
Tests characterize existing rules, rather than treating mistaken assumptions as bug fixes.

## Structural decisions

| Before | After | Reason |
|---|---|---|
| Weapon counters and independent reload/animation deadlines | AmmoState and one WeaponOperation completion owner | Explain ammunition invariants and bind reload completion to the correct weapon. |
| Room1/2/3 state cases | MatchPhase + StageIndex, CampaignDefinition and StageBinding | Add stages through data instead of more enum branches. |
| Input/presentation mixed with Commands | MonoBehaviour interactions plus player request ports/Mirror adapters | Keep local gameplay independent of transport APIs. |
| GameManager was a NetworkBehaviour | MonoBehaviour composition root plus MirrorMatch | Separate match orchestration from wire messages. |
| Enemy decisions inside navigation/animation callbacks | Engine-free EnemyBrain with IEnemyWorld | Test transitions independently of NavMesh/physics. |
| Hierarchy-dependent rigs and HUD lookups | Authored PlayerRigBindings/MatchSceneBindings | Support the different Boy/Girl hierarchies and detect missing references. |
| Stringified positions as network keys | Persistent scene IDs; separate typed location lookup | Avoid locale/float formatting and wrong grid/candle lookup. |
| Menu components address concrete network manager | ISessionService / ISessionDirectory | Prepare session-service substitution. |
| Repeated per-item placement/grant branches | ContentCatalog, IPlacedItem and PlaceableGrant | Extend content without editing a central spawn switch. |

Core, Contracts, Configuration and Runtime have separate assemblies. Core has no engine references;
Runtime does not depend on Mirror or Assembly-CSharp. Remaining world/UI/composition and adapters
are still in Assembly-CSharp; this is not a claim that every component is completely isolated.
See [ARCHITECTURE.md](ARCHITECTURE.md) for diagrams, authority boundaries and extension instructions.

## Confirmed fixes, distinguished from refactoring

- **Enemy pushing without attacking:** melee entry excluded Walking. Contact now enters fighting
  from Walking or Running, stops the agent and deals damage; Screaming/Blocked retain grace.
  Regression verifies actual approach/contact/stop/damage. No new attack cadence or balance values.
- **Ammunition/reload ordering:** separate completion paths could act on the current weapon after
  the initiating weapon changed or controls were disabled. One captured operation owns completion;
  death/disable cancels it. Total includes loaded rounds; reserve and full-magazine HUD counts derive
  from that invariant. Same-weapon pickup preserves the loaded magazine while replenishing total.
- **Ready requests:** duplicate player readiness could count twice. ReadyRoster deduplicates IDs,
  removes departures and starts one campaign when connected players are ready.
- **Win/loss ordering:** simultaneous timer expiry and candle depletion could select advancement
  first. Server LateUpdate resolves loss before advancement, including the final stage.
- **Initialization/lifetime:** initialize weapon/enemy state before first use/replication; release
  camera-facing references on shutdown; reset session compatibility registries between matches.
- **Authoritative cell release:** two-process regression reproduced consumed bricks freeing the
  client view while leaving the server cell reserved. A destruction-time RPC is not a reliable
  owner of server state. Bricks, bears and barrels now release the server cell directly before
  broadcasting presentation/despawning; a dedicated regression checks all three.
- **HUD ownership:** recreating ClockManager no longer resets the session clock. UI formats state;
  the match owns simulation time.
- **Remote rig:** use the authored hand reference instead of a skeleton-specific child chain.

These are logical ordering/lifecycle defects on Unity's main thread; they are not evidence of
parallel memory races. Host-only tests do not prove delivery ordering on separate peers.

## Code-quality audit

Reviewed own scripts under Assets/Scripts; excluded vendored libraries. The audit combines searches,
manual call-site/reference inspection, assembly checks, compiler diagnostics and runtime fixtures.
It does not use a numeric-literal regex as an automatic rewrite rule.

| Topic | Action / policy |
|---|---|
| Magic numbers | Authorable weapon/enemy/stage/item values live in definitions. Shared damage cap and scream timings have named Core constants. Interaction reach and effect lifetime have explicit meaning. Preserve numeric values. |
| Unity strings | Shared tags/layers have named identifiers. Direct animation parameters use cached hashes; the dissolve property uses PropertyToID. Existing preference/protocol keys retain their values. |
| Ownership | Core rule state has one owner. Match clock is session-owned; HUD cannot advance/reset it. Network requests stay in adapters. |
| Coupling | Player rigs, room roots and HUD have authored references. Several old experimental/effect fallbacks still contain child indices; do not silently reinterpret those asset layouts. |
| Duplication | Chest grants use one capped allocation loop; stage flow is generic; ammo arithmetic is shared; client/server damage limits share a constant. |
| Dead code | Remove confirmed-unused private fields and obsolete commented blocks in touched components. Keep serialized/public compatibility fields until all scenes/tools can be migrated safely. |
| Naming/API | New types use PascalCase, private fields use _camelCase. Existing serialized names, UnityEvents and compatibility wrappers retain their spelling/GUIDs. No bulk formatter. |
| Lifecycle | Inspect event unsubscription, coroutines, static registries, component initialization and destroyed Unity references. Preserve local/remote ownership guards. |
| Hot paths | Keep scene discovery in composition/startup; do not add per-frame global searches. No speculative pooling or broad physics changes without profiler evidence. |

Small mathematical literals, vector identities, loop indices and local one-use text remain inline
where naming would obscure meaning. Runtime message text is not a reason to create an unrelated
localization system. Network animation string payloads remain compatible; local Animator calls use
hashes. Configuration assets retain all authored defaults.

### Remaining debt and deliberate limits

GameManager remains a sizeable Unity composition root; PlacementController still coordinates
several local preview modes. Compatibility statics, legacy UnityEvent wrappers, old experimental
scene fallbacks and some hierarchy-based world effects remain. ContentValidation and regression
fixtures protect the shipped scenes; they do not certify every abandoned experiment.

Movement/ammunition/inventory are still owner-driven. Server validation does not reconstruct shot
rays or maintain an inventory ledger. Replacing Mirror requires adapters/composition work; adding
solo or online services is future implementation. Mobile/device performance, packet loss, high
latency and long multiplayer soak tests need separate runs. Existing furniture emits negative-scale
BoxCollider warnings (for example corner-sofa seats); Unity forces positive collider sizes. These
authored warnings are not newly introduced errors, but the affected furniture needs a separate
geometry review before claiming every physical contact is verified. The NavMesh route fixture
checks complete paths, not all possible crowd/obstacle interactions.

Potential gameplay improvements deliberately deferred: change attack telegraph/grace length, melee
range, ammunition economy, spawn weights, movement speeds or weapon slots. None is required to
explain or fix the confirmed lifecycle/contact defects.

## Verification

The suite includes typed Core tests and real-host PlayMode fixtures for lobby immunity, damage,
reload/death/pickup invariants, candle loss, phase boundaries, ready deduplication, an added fourth
stage, authored NavMesh routes, melee, explosions, movement, chest grants and Ghost brick immunity.
A small reflection bridge reaches legacy Assembly-CSharp; test assemblies are excluded from normal
players.

Two-peer automation lives in `Tools/run_network_regression.py` and `Tools/Regression/`. It uses
an Editor host plus a development Linux player with runtime Pipeline enabled only for QA. It checks
local movement, a real ray/Command/ammo decrement, replicated health/dissolve/despawn, low-health
feedback, placement acknowledgement/conflict and stable-cell release. This is loopback network
coverage, not a test of Wi-Fi discovery or Internet quality.

License-free compile checks cover Windows/macOS/Linux and asset integrity; the known TMP editor
harness limitation is reported separately. Full Unity tests/builds need an activated license.
The regression GitHub workflow uses the existing license concurrency group and has not been run
remotely during this local work. Reproduction commands are in [BUILDING.md](BUILDING.md).

### Local execution, 2026-11-20

| Check | Result | Local evidence (ignored Logs directory) |
|---|---|---|
| Final EditMode | 45 passed, 0 failed | `refactor-edit-verified.json` |
| Final PlayMode | 17 passed, 0 failed | `refactor-play-verified-status.json` |
| License-free script/asset checks | Windows, macOS and Linux pass; known TMP editor harness limitation remains separate | `refactor-compile-verified.log` |
| QA Linux build | Succeeded, 0 errors | `refactor-dev-build.json` |
| Editor host + native Linux client | Passed, 2 peers | `network-regression-20261120-111627/result.json` |
| Module boundaries and saved Mirror IDs | Passed | `Tools/check_module_boundaries.py`, `Tools/assign_scene_ids.py --check` |

Two-peer observations: movement 1.50 m over the 0.6 s walking sample; one rifle round consumed;
GhostOrange 120 → 40 HP on the authored body hit; player 100 → 20 HP and persistent overlay alpha
0.09; completed dissolve/despawn; placement accepted, duplicate rejected without spending inventory,
and cell released on both server and client. These numbers validate existing balance, not new tuning.

The harness initially assumed too little rifle damage; its expectation was corrected to the authored
40 × 2 body multiplier. It then exposed the real server-cell release defect described above. It also
handles the current CLI's Editor-eval error reporting on stderr even when exit status is zero, so
an empty response cannot silently count as a successful scenario. Test sequencing arms the host
observer before the native shot, avoiding compiler latency as a timing assumption.

Normal Linux release: **Succeeded, 0 errors**, 37.1 seconds (`refactor-release-build.json`).
A ten-second native host launch stayed running without detected reference/spawn/compile exceptions
and created no runtime Pipeline endpoint (`refactor-release-smoke.json`); this is a startup smoke
test, not a complete real-time campaign playthrough. QA defines were restored, Play Mode stopped,
auto-tick disabled, and MainMenu left clean. Unrelated pre-existing cloud material, Rider plugin
and recovery assets were preserved. No commit or push was made.

The local console check from the saved cursor and final source/asset-integrity checks are retained
in Logs. The sampler returned no new errors; the Editor ground-truth error count remained one,
as at the initial snapshot. The ring buffer reports dropped older entries, so the console export
is not a complete historical transcript; test/build results and native logs provide the evidence. Existing negative-scale/old NavMesh/LightingData warnings remain documented limitations;
no cross-platform player builds, real Wi-Fi match or phone-device acceptance is claimed.

## Code-quality follow-up

Method extraction, shared constants/configuration, selective XML API documentation and their
characterization tests are recorded in [CODE-QUALITY.md](CODE-QUALITY.md).


## Match ownership follow-up (2026-11-20)

GameManager keeps its name and MonoScript GUID but now composes MatchSession, MatchWorldRegistry,
EnemyWaveSpawner and MatchPlayerCoordinator. It orders transitions instead of maintaining parallel
rules, static collections or a spawn coroutine. Registry views are read-only; mutations use owner
methods. The remaining player coordinator is a Unity integration component and still references the
current PlayerManager adapter; this is not a claim that changing networking is configuration-only.

The room-specific MatchState/CurrentMachState/LegacyMatchStateMap and ClockManager statics were
removed, together with three-stage fallbacks for order, durations and enemies. Production callers,
editor diagnostics, integration tests and native network probes now use MatchProgress plus an
instance context. Old experimental scenes remain archived and do not silently construct a campaign.

IMatchParticipant receives the root at scene composition or Mirror spawn boundaries. The backend
registers its loaded scene context through IMatchSceneRegistration. MirrorMatchBinding is a
stateless backend bridge; there is no replacement global game registry or per-frame scene search.
Persistent visual/menu participants are bound at startup and detached during teardown. Lobby is
initialized by the session, not by local-player Start, so player presentation cannot reset a later
phase. Duplicate campaign-entry notifications preserve both progress and the existing loadout.

Two new failing baseline tests demonstrated old-candle drain after a transition and a surviving
active horde. CaptureArea now drains only current-stage defense areas and never resolves results.
MatchSession resolves loss before timeout advancement. At successful defense, spawning stops and
IStageEnemy retirement disables combat/colliders synchronously, replicates the existing dissolve
and leaves final despawn to authority. This is the agreed gameplay change: no survivors pursue the
next room. Combat death animations/audio remain separate; already dying enemies finish normally.

The native host/client harness additionally observes Skeleton and Ghost retirement on both peers,
then waits for both server-owned despawns. Tests also cover final victory, duplicate entry, readiness,
deadline loss priority and duplicate progress preserving clock time. Final build and peer-check results are recorded below.


The final dissolve review shares preparation between normal death and stage retirement: both hide
the authored auxiliary objects before dissolving the body. Retirement stops its own audio sources;
normal death retains its existing voice/delay. This avoids a second, incomplete visual death path.
The expanded peer fixture uses the actual campaign prefabs (including SkeletonYellow), not the
unregistered Skeleton base used only as a prefab parent. Two fixture failures during development
were not treated as gameplay failures or successful acceptance: misplaced using declarations and
an unregistered test prefab. Both were corrected before the final run.


Final source validation: 92 EditMode cases passed (`Logs/split-edit-verified.json`), and all 24
PlayMode cases passed after the shared dissolve preparation change (`Logs/split-play-last-status.json`).
The license-free Windows/macOS/Linux checks passed (`Logs/split-compile-last.log`), with the existing
separate TMP editor-harness limitation. The final development Linux build succeeded with zero errors
in 43.7 seconds (`Logs/split-final-dev-build.json`). The saved scene contains no obsolete fallback
arrays; content validation, stable Mirror scene IDs and reimported chair/table remaps were checked.
The extended URP audit inspected 258 materials with zero errors.

Desktop checks do not replace a complete co-op playthrough on phones, dense-horde profiling,
cross-platform rendering acceptance or adverse-network/reconnect tests. The remaining scene-level
player integration still uses current adapters; future single-player/transport work must supply its
own composition and corresponding adapters.


The final two-process run passed (`Logs/network-regression-20261120-135218/result.json`): movement
about 1.5 m/0.6 s, one round spent, body-hit HP 40, player HP 20/overlay alpha 0.09, normal death,
placement/duplicate rejection/cell release and stage retirement of two configured enemy variants.
The native replica verified disabled colliders and auxiliary-object hiding before observing both
server-owned despawns. This run used the final rebuilt QA player and matching Editor scripts.


The normal Linux release succeeded with zero errors in 56.7 seconds
(`Logs/split-release-build.json`), with runtime Pipeline QA defines absent from the build.


The final normal native Linux player exited 0 and passed all 15 graphics checks
(`Logs/split-native-graphics-result.json`): camera stack/single shot, Volume toggles, AA/AO, quality
switching, GPU exposure/bloom, placed/preview models, native resolution confirmation/reapply/rollback,
small-screen HUD and candle lighting/fog across every shipped room through victory. The Editor was
left stopped on a clean MainMenu with runtime QA defines absent and automatic ticking disabled.
No new Editor errors appeared after the final test baseline (`Logs/split-console-final.json`).
No commit or push was made. Existing cloud-material, Rider-plugin and recovery changes were preserved.
