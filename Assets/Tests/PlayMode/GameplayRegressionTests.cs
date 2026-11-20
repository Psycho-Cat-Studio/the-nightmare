using System;
using Nightmare.Core.Match;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nightmare.Tests
{
    /// <summary>Exercises production match, combat, placement and movement flows with a real local host.</summary>
    public class GameplayRegressionTests
    {
        private Component _network;
        private Component _game;
        private Component _player;

        [UnitySetUp]
        public IEnumerator StartSession()
        {
            SceneManager.LoadScene("LobbyTests");
            yield return null;
            _network = Object.FindObjectsByType(GameTestDriver.Type("CustomNetworkManager")).Cast<Component>().First();
            GameTestDriver.Call(_network, "Host", "Regression", 7798);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline)
            {
                _game = Object.FindObjectsByType(GameTestDriver.Type("GameManager")).Cast<Component>().FirstOrDefault();
                _player = _game == null ? null : GameTestDriver.Get(GameTestDriver.Get(_game, "Players"), "LocalPlayer") as Component;
                if (_player != null && _game != null && _player.gameObject.scene == _game.gameObject.scene
                    && _game.gameObject.scene.name == "ExtremeHomeMakeover") break;
                yield return null;
            }
            Assert.That(_player, Is.Not.Null, "Host never spawned its local player");
            _game = Object.FindObjectsByType(GameTestDriver.Type("GameManager")).Cast<Component>().First();
            foreach (var screen in Object.FindObjectsByType(GameTestDriver.Type("ButtonsControll")).Cast<Component>())
            {
                GameTestDriver.Call(screen, "SkipWelcomeScreen");
                GameTestDriver.Call(screen, "SkipControls");
            }
            yield return new WaitForSeconds(0.2f);
        }

        [UnityTearDown]
        public IEnumerator StopSession()
        {
            if (_network != null) GameTestDriver.Call(_network, "StopHost");
            yield return null;
            SceneManager.LoadScene("MainMenu");
            yield return null;
        }

        private void EnterRoom() { GameTestDriver.Call(_game, "EnterCampaign"); }

        [UnityTest]
        public IEnumerator CompletedStageCannotDrainItsOldCandles()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Defend, 0));
            GameTestDriver.Call(GameTestDriver.Get(_game, "Spawner"), "Stop");
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var area = (Component)((IList)GameTestDriver.Get(room, "CaptureAreas"))[0];
            var prefab = FirstEnemyPrefab();
            var enemy = Object.Instantiate(prefab, area.transform.position, Quaternion.identity);
            SpawnNetworkEntity(enemy);
            yield return null;
            var brain = GameTestDriver.Component(enemy, "EnemyControllerServer");
            GameTestDriver.Call(GameTestDriver.Get(area, "_enemiesInside"), "Add", brain);
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Prepare, 1));
            float progress = (float)GameTestDriver.Get(area, "_progress");
            yield return null;
            yield return null;
            Assert.That(GameTestDriver.Get(area, "_progress"), Is.EqualTo(progress), "Completed room must be frozen");
        }

        [UnityTest]
        public IEnumerator CompletedStageImmediatelyRetiresItsSurvivingHorde()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Defend, 0));
            GameTestDriver.Call(GameTestDriver.Get(_game, "Spawner"), "Stop");
            var prefab = FirstEnemyPrefab();
            var enemy = Object.Instantiate(prefab, _player.transform.position + Vector3.forward * 2f, Quaternion.identity);
            SpawnNetworkEntity(enemy);
            yield return null;
            var brain = GameTestDriver.Component(enemy, "EnemyControllerServer");
            GameTestDriver.Set(GameTestDriver.Get(_game, "Clock"), "Remaining", 0f);
            yield return null;
            Assert.That(GameTestDriver.Get(brain, "_isDying"), Is.True, "Room completion must disable surviving enemies before dissolve");
            Assert.That(enemy.GetComponentsInChildren<Collider>().All(collider => !collider.enabled || !collider.gameObject.activeInHierarchy), Is.True);
        }

        [UnityTest]
        public IEnumerator FinalVictoryFreezesCandlesAndRetiresAGhost()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Prepare, 2));
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Defend, 2));
            GameTestDriver.Call(GameTestDriver.Get(_game, "Spawner"), "Stop");
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var area = (Component)((IList)GameTestDriver.Get(room, "CaptureAreas"))[0];
            var ghost = Object.Instantiate(EnemyPrefabs(true)[2], area.transform.position, Quaternion.identity);
            SpawnNetworkEntity(ghost);
            yield return null;
            GameTestDriver.Set(GameTestDriver.Get(_game, "Clock"), "Remaining", 0f);
            yield return null;
            Assert.That(((MatchProgress)GameTestDriver.Get(_game, "Progress")).Phase, Is.EqualTo(MatchPhase.Won));
            var brain = GameTestDriver.Component(ghost, "EnemyControllerServer");
            Assert.That(GameTestDriver.Get(brain, "_isDying"), Is.True);
            var motion = ghost.GetComponentInChildren<GhostLevitation>(true);
            var heldPose = motion.transform.localPosition;
            yield return new WaitForSeconds(.1f);
            Assert.That(Vector3.Distance(heldPose, motion.transform.localPosition), Is.LessThan(.0001f), "Retirement must freeze levitation");
            var hidden = (GameObject[])GameTestDriver.Get(brain, "_deathHiddenObjects");
            Assert.That(hidden.All(part => part == null || !part.activeSelf), Is.True, "Retirement must use the existing dissolve preparation");
            GameTestDriver.Set(area, "_progress", 10f);
            yield return null;
            yield return null;
            Assert.That(GameTestDriver.Get(area, "_progress"), Is.EqualTo(10f));
            var dissolve = ghost.GetComponentsInChildren<MonoBehaviour>(true).First(component => component.GetType().Name == "DissolveEnemyBody");
            Assert.That((float)GameTestDriver.Get(dissolve, "DissolveAmount"), Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator RepeatedCampaignEntryDoesNotRestartDefenseOrReplaceTheLoadout()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_game, "ServerSetProgress", new MatchProgress(MatchPhase.Defend));
            GameTestDriver.Call(GameTestDriver.Get(_game, "Spawner"), "Stop");
            var clock = GameTestDriver.Get(_game, "Clock");
            GameTestDriver.Set(clock, "Remaining", 77f);
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            var weapon = GameTestDriver.Get(equipment, "Weapon1");
            GameTestDriver.Set(weapon, "CurrentMagAmmo", 1);
            EnterRoom();
            Assert.That(((MatchProgress)GameTestDriver.Get(_game, "Progress")).Phase, Is.EqualTo(MatchPhase.Defend));
            Assert.That(GameTestDriver.Get(clock, "Remaining"), Is.EqualTo(77f));
            Assert.That(GameTestDriver.Get(equipment, "Weapon1"), Is.SameAs(weapon));
            Assert.That(GameTestDriver.Get(weapon, "CurrentMagAmmo"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GhostLevitationCombinesWithEveryAnimationAndFreezesForDeath()
        {
            EnterRoom(); yield return null;
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var spawn = ((IList)GameTestDriver.Get(room, "EnemySpawnPoint"))[0] as GameObject;
            foreach (var prefab in EnemyPrefabs(true))
            {
                var root = Object.Instantiate(prefab, spawn.transform.position, Quaternion.identity);
                SpawnNetworkEntity(root); yield return null;
                var brain = GameTestDriver.Component(root, "EnemyControllerServer");
                ((Behaviour)brain).enabled = false;
                var agent = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Assert.That(agent.isOnNavMesh, Is.True);
                agent.isStopped = true;
                var motion = root.GetComponentInChildren<GhostLevitation>(true);
                Assert.That(motion, Is.Not.Null);
                var animator = root.GetComponentInChildren<Animator>(true);
                var body = root.transform.Find("EnemyBody");
                var legs = root.transform.Find("EnemyLegs");
                var origin = root.transform.position;
                var bodyPosition = body.position;
                var legsPosition = legs.position;
                var head = root.GetComponentsInChildren<Collider>(true).Single(collider => collider.CompareTag(GameTags.EnemyHead));
                Assert.That(head.transform.IsChildOf(motion.transform), Is.True);
                foreach (string state in new[] { "Waiting", "Walking", "Running", "Screaming", "Punching" })
                {
                    animator.SetBool("waiting", state == "Waiting");
                    animator.SetBool("running", state == "Running");
                    animator.SetBool("screaming", state == "Screaming");
                    GameTestDriver.Set(brain, "CurrentState", Enum.Parse(GameTestDriver.Type("EnemyControllerServer+EnemyState"), state == "Punching" ? "Fighting" : "Running"));
                    animator.Play("Base Layer." + state, 0, .1f);
                    yield return null;
                    var before = motion.transform.position;
                    yield return new WaitForSeconds(.25f);
                    Assert.That(Vector3.Distance(before, motion.transform.position), Is.GreaterThan(.0001f), prefab.name + " / " + state + " has no levitation");
                    Assert.That(Vector3.Distance(origin, root.transform.position), Is.LessThan(.002f));
                    Assert.That(Vector3.Distance(bodyPosition, body.position), Is.LessThan(.002f));
                    Assert.That(Vector3.Distance(legsPosition, legs.position), Is.LessThan(.002f));
                }
                var feedback = GameTestDriver.Component(root, "EnemyFeedback");
                var impact = root.transform.InverseTransformPoint(root.GetComponentInChildren<SkinnedMeshRenderer>().bounds.center);
                Assert.That(GameTestDriver.Call(feedback, "IsValidImpact", impact, Vector3.forward), Is.True);
                GameTestDriver.Call(feedback, "BulletHit", impact, Vector3.forward);
                yield return new WaitForSeconds(.08f);
                Assert.That(animator.GetLayerWeight(animator.GetLayerIndex("Hit feedback")), Is.GreaterThan(0f));
                var held = motion.transform.localPosition;
                GameTestDriver.Call(brain, "TakeDamage", 10000f);
                yield return new WaitForSeconds(.25f);
                Assert.That(Vector3.Distance(held, motion.transform.localPosition), Is.LessThan(.0001f), "Death must keep the current hover pose");
                agent.enabled = false;
            }
        }

        [UnityTest]
        public IEnumerator BarrelChainPropagatesBeyondTheFirstBlastAndExplodesEachItemOnce()
        {
            var chain = SpawnBarrelChain();
            GameTestDriver.Call(chain[0], "Explode");
            GameTestDriver.Call(chain[0], "Explode");
            yield return AwaitBarrelChain(chain, 3);
        }

        [UnityTest]
        public IEnumerator GrenadeStartsABarrelChainBeyondItsOwnRadius()
        {
            var chain = SpawnBarrelChain(2.2f);
            var prefab = (GameObject)GameTestDriver.Get(GameTestDriver.Component(_player.gameObject, "PlayerShoot"), "GrenadePrefab");
            var grenade = Object.Instantiate(prefab, ChainOrigin, Quaternion.identity);
            var body = grenade.GetComponent<Rigidbody>();
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
            SpawnNetworkEntity(grenade);
            yield return AwaitBarrelChain(chain, 4);
        }

        private static Vector3 ChainOrigin => new Vector3(-100f, 20f, -100f);
        private Component[] SpawnBarrelChain(float firstOffset = 0f)
        {
            var world = GameTestDriver.Get(_game, "World");
            var cells = ((IDictionary)GameTestDriver.Get(world, "BuildingPoints")).Values.Cast<Component>().Take(4).ToArray();
            var content = GameTestDriver.Get(_game, "Content");
            var entry = ((IEnumerable)GameTestDriver.Get(content, "Placeables")).Cast<object>()
                .First(item => ((GameObject)GameTestDriver.Get(item, "Prefab")).name == "Barrel");
            var prefab = (GameObject)GameTestDriver.Get(entry, "Prefab");
            var offsets = new[] { firstOffset, firstOffset + 2.2f, firstOffset + 4.4f, firstOffset + 8f };
            return offsets.Select((offset, index) =>
            {
                var root = Object.Instantiate(prefab, ChainOrigin + Vector3.right * offset, Quaternion.identity);
                var barrel = GameTestDriver.Component(root, "Barrel");
                GameTestDriver.Call(barrel, "InitializeCell", GameTestDriver.Get(cells[index], "Id"));
                GameTestDriver.Set(cells[index], "Buildable", false);
                SpawnNetworkEntity(root);
                return barrel;
            }).ToArray();
        }

        private IEnumerator AwaitBarrelChain(Component[] chain, int expectedEffects)
        {
            var deadline = Time.realtimeSinceStartup + 6f;
            while (chain.Take(3).Any(barrel => !(bool)GameTestDriver.Get(barrel, "_exploded")) && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            Assert.That(chain.Take(3).All(barrel => (bool)GameTestDriver.Get(barrel, "_exploded")), Is.True, "Chain stopped before its third barrel");
            Assert.That(GameTestDriver.Get(chain[3], "_exploded"), Is.False, "Out-of-range barrel detonated");
            while (Object.FindObjectsByType(GameTestDriver.Type("ExplosionFeedback")).Length < expectedEffects && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(Object.FindObjectsByType(GameTestDriver.Type("ExplosionFeedback")).Length, Is.EqualTo(expectedEffects), "A barrel produced duplicate feedback");
            var world = GameTestDriver.Get(_game, "World");
            var cells = (IDictionary)GameTestDriver.Get(world, "BuildingPoints");
            foreach (var barrel in chain.Take(3))
            {
                string id = (string)GameTestDriver.Get(barrel, "InitialPosAndTag");
                Assert.That(GameTestDriver.Get(cells[id], "Buildable"), Is.True);
            }
            string remainingId = (string)GameTestDriver.Get(chain[3], "InitialPosAndTag");
            Assert.That(GameTestDriver.Get(cells[remainingId], "Buildable"), Is.False);
        }

        private GameObject[] EnemyPrefabs(bool ghosts)
        {
            var campaign = GameTestDriver.Get(_game, "_campaign");
            return ((IEnumerable)GameTestDriver.Get(campaign, "Stages")).Cast<object>().Select(stage =>
                (GameObject)GameTestDriver.Get(((IEnumerable)GameTestDriver.Get(stage, "Enemies")).Cast<object>()
                    .First(entry => ((GameObject)GameTestDriver.Get(entry, "Prefab")).name.StartsWith("Ghost") == ghosts), "Prefab")).ToArray();
        }
        private GameObject FirstEnemyPrefab() => EnemyPrefabs(false)[0];

        private static void SpawnNetworkEntity(GameObject enemy)
        {
            var spawn = GameTestDriver.Type("Mirror.NetworkServer").GetMethods().Single(method => method.Name == "Spawn"
                && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType.Name == "NetworkConnectionToClient");
            spawn.Invoke(null, new object[] { enemy, null });
        }

        [UnityTest]
        public IEnumerator LobbyIsImmortalAndCombatDamageReplicatesToHealth()
        {
            float health = (float)GameTestDriver.Get(_player, "_currentHealth");
            GameTestDriver.Call(_player, "TakeDamage", 45f);
            Assert.That(GameTestDriver.Get(_player, "_currentHealth"), Is.EqualTo(health));
            EnterRoom(); yield return null;
            GameTestDriver.Call(_player, "TakeDamage", 45f);
            Assert.That(GameTestDriver.Get(_player, "_currentHealth"), Is.EqualTo(health - 45f));
            Assert.That(GameTestDriver.Get(_player, "IsDead"), Is.False);
        }

        [UnityTest]
        public IEnumerator FirstRoomPreservesPreparationTimeAndRifleReload()
        {
            EnterRoom(); yield return null;
            Assert.That((float)GameTestDriver.Get(GameTestDriver.Get(_game, "Clock"), "Remaining"), Is.InRange(59f, 60f));
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            var prefabs = (GameObject[])GameTestDriver.Get(_game, "Weapons");
            var weaponObject = Object.Instantiate(prefabs[1]);
            var weapon = GameTestDriver.Component(weaponObject, "Weapon");
            yield return null;
            GameTestDriver.Set(weapon, "CurrentMagAmmo", 1);
            int total = (int)GameTestDriver.Get(weapon, "CurrentAmmo");
            GameTestDriver.Call(weapon, "reload");
            yield return new WaitForSeconds((float)GameTestDriver.Get(weapon, "ReloadTime") + 0.15f);
            Assert.That(GameTestDriver.Get(weapon, "CurrentMagAmmo"), Is.EqualTo(GameTestDriver.Get(weapon, "MaxMagAmmo")));
            Assert.That(GameTestDriver.Get(weapon, "CurrentAmmo"), Is.EqualTo(total));
            Object.Destroy(weaponObject);
        }

        [UnityTest]
        public IEnumerator LosingOneCaptureAreaEndsTheMatch()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Defend, 0));
            GameTestDriver.Call(GameTestDriver.Get(_game, "Spawner"), "Stop");
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var area = (Component)((IList)GameTestDriver.Get(room, "CaptureAreas"))[0];
            var prefab = FirstEnemyPrefab();
            var enemy = Object.Instantiate(prefab, area.transform.position, Quaternion.identity);
            var spawn = GameTestDriver.Type("Mirror.NetworkServer").GetMethods().Single(method => method.Name == "Spawn"
                && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType.Name == "NetworkConnectionToClient");
            spawn.Invoke(null, new object[] { enemy, null });
            yield return null;
            var brain = GameTestDriver.Component(enemy, "EnemyControllerServer");
            GameTestDriver.Set(brain, "CurrentState", Enum.Parse(GameTestDriver.Type("EnemyControllerServer+EnemyState"), "Running"));
            GameTestDriver.Set(brain, "IsTriggerLocked", false);
            GameTestDriver.Set(area, "_progress", 0.001f);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (((MatchProgress)GameTestDriver.Get(_game, "Progress")).Phase.ToString() != "Lost" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(((MatchProgress)GameTestDriver.Get(_game, "Progress")).Phase.ToString(), Is.EqualTo("Lost"));
            Assert.That(GameTestDriver.Get(_game, "CanDamagePlayers"), Is.False);
        }

        [UnityTest]
        public IEnumerator BuildingAndActionRestoreControlsWithoutPendingMovement()
        {
            EnterRoom(); yield return null;
            var motor = GameTestDriver.Component(_player.gameObject, "PlayerMotor");
            GameTestDriver.Call(motor, "Move", Vector3.forward * 7f);
            GameTestDriver.Call(motor, "IncreaseRecoil", 5f);
            GameTestDriver.Call(_player, "SetBuildingMode");
            Assert.That(((Behaviour)motor).enabled, Is.False);
            Assert.That(GameTestDriver.Get(motor, "DesiredVelocity"), Is.EqualTo(Vector3.zero));
            GameTestDriver.Call(_player, "SetActionMode"); yield return null;
            Assert.That(((Behaviour)motor).enabled, Is.True);
            Assert.That(GameTestDriver.Get(motor, "DesiredVelocity"), Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator ServerDeathCannotLeavePlayerAliveOrMoving()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_player, "TakeDamage", 10000f); yield return null;
            Assert.That(GameTestDriver.Get(_player, "IsDead"), Is.True);
            Assert.That(GameTestDriver.Get(_player, "_currentHealth"), Is.EqualTo(0f));
            Assert.That(_player.GetComponent<Rigidbody>().isKinematic, Is.True);
        }
        [UnityTest]
        public IEnumerator AuthoredSpawnPointsHaveCompleteRoutesToTheirCandleTargets()
        {
            foreach (GameObject roomObject in (IList)GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "Rooms"))
            {
                var room = GameTestDriver.Component(roomObject, "Room");
                var areas = (IList)GameTestDriver.Get(room, "CaptureAreas");
                foreach (GameObject spawn in (IList)GameTestDriver.Get(room, "EnemySpawnPoint"))
                {
                    var marker = GameTestDriver.Component(spawn, "EnemySpawnTarget");
                    var area = marker != null ? GameTestDriver.Get(marker, "InitialArea") as Component : null;
                    if (area == null) area = (Component)areas[0];
                    UnityEngine.AI.NavMeshHit start, end;
                    Assert.That(UnityEngine.AI.NavMesh.SamplePosition(spawn.transform.position, out start, 3f, -1), Is.True, spawn.name);
                    Assert.That(UnityEngine.AI.NavMesh.SamplePosition(area.transform.position, out end, 3f, -1), Is.True, area.name);
                    var path = new UnityEngine.AI.NavMeshPath();
                    Assert.That(UnityEngine.AI.NavMesh.CalculatePath(start.position, end.position, -1, path), Is.True, spawn.name);
                    Assert.That(path.status, Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete), spawn.name);
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator WalkingEnemyApproachesStopsAndDamagesPlayerInsteadOfPushing()
        {
            EnterRoom(); yield return new WaitForSeconds(.75f);
            _player.GetComponent<Rigidbody>().isKinematic = true;
            var prefab = FirstEnemyPrefab();
            UnityEngine.AI.NavMeshHit start;
            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(_player.transform.position + Vector3.right * 1.2f, out start, 3f, -1), Is.True);
            var enemy = Object.Instantiate(prefab, start.position, Quaternion.identity);
            var spawn = GameTestDriver.Type("Mirror.NetworkServer").GetMethods().Single(method => method.Name == "Spawn"
                && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType.Name == "NetworkConnectionToClient");
            spawn.Invoke(null, new object[] { enemy, null }); yield return null;
            var brain = GameTestDriver.Component(enemy, "EnemyControllerServer");
            GameTestDriver.Set(brain, "ShouldScream", false);
            GameTestDriver.Call(brain, "PlayerDetected", _player.transform);
            float health = (float)GameTestDriver.Get(_player, "_currentHealth");
            float deadline = Time.realtimeSinceStartup + 4f;
            while ((float)GameTestDriver.Get(_player, "_currentHealth") >= health && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That((float)GameTestDriver.Get(_player, "_currentHealth"), Is.LessThan(health));
            Assert.That(enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped, Is.True);
            Assert.That(GameTestDriver.Get(brain, "CurrentState").ToString(), Is.EqualTo("Fighting"));
        }
        [UnityTest]
        public IEnumerator ExplosionDamagesAMultiColliderPlayerOnlyOnce()
        {
            EnterRoom(); yield return new WaitForSeconds(.2f);
            var damage = Activator.CreateInstance(GameTestDriver.Type("ExplosionDamage"));
            var colliders = _player.GetComponentsInChildren<Collider>().Where(c => c.CompareTag("Player")).ToArray();
            Assert.That(colliders.Length, Is.GreaterThan(0));
            foreach (var collider in colliders) GameTestDriver.Call(damage, "Apply", collider, 100f);
            foreach (var collider in colliders) GameTestDriver.Call(damage, "Apply", collider, 100f);
            Assert.That(GameTestDriver.Get(_player, "_currentHealth"), Is.EqualTo(55f));
        }

        [UnityTest]
        public IEnumerator DeathCancelsReloadWithoutRefillingAnInactiveWeapon()
        {
            EnterRoom(); yield return null;
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            var weapon = GameTestDriver.Call(equipment, "getActiveWeapon");
            GameTestDriver.Call(weapon, "shoot");
            int rounds = (int)GameTestDriver.Get(weapon, "CurrentMagAmmo");
            var shoot = (MonoBehaviour)GameTestDriver.Component(_player.gameObject, "PlayerShoot");
            shoot.StartCoroutine((IEnumerator)GameTestDriver.Call(shoot, "Reload"));
            GameTestDriver.Call(_player, "TakeDamage", 10000f);
            yield return new WaitForSeconds(2.3f);
            Assert.That(GameTestDriver.Get(weapon, "CurrentMagAmmo"), Is.EqualTo(rounds));
            Assert.That(GameTestDriver.Get(weapon, "State").ToString(), Is.EqualTo("idle"));
        }

        [UnityTest]
        public IEnumerator ReadyRequestIsIdempotentAndStartsOnlyOneCampaign()
        {
            GameTestDriver.Call(_game, "ServerRegisterReady", _player.name);
            yield return null;
            GameTestDriver.Call(_game, "ServerRegisterReady", _player.name);
            Assert.That(GameTestDriver.Get(_game, "ReadyPlayersCnt"), Is.EqualTo(1));
            Assert.That(((MatchProgress)GameTestDriver.Get(_game, "Progress")).Phase.ToString(), Is.EqualTo("Prepare"));
        }

        [UnityTest]
        public IEnumerator FourthStageRunsThroughTheSameProductionFlowWithoutANewEnumValue()
        {
            var original = (ScriptableObject)GameTestDriver.Get(_game, "_campaign");
            var campaign = Object.Instantiate(original);
            var oldStages = (Array)GameTestDriver.Get(campaign, "Stages");
            var stages = Array.CreateInstance(oldStages.GetType().GetElementType(), 4);
            Array.Copy(oldStages, stages, oldStages.Length);
            var fourth = Object.Instantiate((ScriptableObject)oldStages.GetValue(2));
            GameTestDriver.Set(fourth, "PreparationSeconds", 33f);
            stages.SetValue(fourth, 3); GameTestDriver.Set(campaign, "Stages", stages);
            var originalBindings = (Array)GameTestDriver.Get(_game, "_stageBindings");
            var bindings = Array.CreateInstance(originalBindings.GetType().GetElementType(), 4);
            Array.Copy(originalBindings, bindings, originalBindings.Length); bindings.SetValue(originalBindings.GetValue(2), 3);
            GameTestDriver.Set(_game, "_campaign", campaign); GameTestDriver.Set(_game, "_stageBindings", bindings);
            try
            {
                var progress = Activator.CreateInstance(GameTestDriver.Type("Nightmare.Core.Match.MatchProgress"),
                    Enum.Parse(GameTestDriver.Type("Nightmare.Core.Match.MatchPhase"), "Prepare"), 3);
                GameTestDriver.Call(_game, "ServerSetProgress", progress); yield return null;
                Assert.That(GameTestDriver.Get(_game, "StageIndex"), Is.EqualTo(3));
                Assert.That((float)GameTestDriver.Get(GameTestDriver.Get(_game, "Clock"), "Remaining"), Is.InRange(32f, 33f));
                GameTestDriver.Set(GameTestDriver.Get(_game, "Clock"), "Remaining", 0f); yield return null; yield return null;
                Assert.That(GameTestDriver.Get(_game, "Phase").ToString(), Is.EqualTo("Defend"));
                GameTestDriver.Set(GameTestDriver.Get(_game, "Clock"), "Remaining", 0f); yield return null; yield return null;
                Assert.That(GameTestDriver.Get(_game, "Phase").ToString(), Is.EqualTo("Won"));
            }
            finally
            {
                GameTestDriver.Set(_game, "_campaign", original); GameTestDriver.Set(_game, "_stageBindings", originalBindings);
                Object.Destroy(campaign); Object.Destroy(fourth);
            }
        }
        [UnityTest]
        public IEnumerator LosingTheLastCandleAtTheTimerBoundaryCannotBecomeVictory()
        {
            EnterRoom(); yield return null;
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Prepare, 2));
            GameTestDriver.Call(_game, "ApplyReplicatedProgress", new MatchProgress(MatchPhase.Defend, 2));
            GameTestDriver.Call(GameTestDriver.Get(_game, "Spawner"), "Stop");
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var area = ((IList)GameTestDriver.Get(room, "CaptureAreas"))[0];
            GameTestDriver.Set(area, "_progress", 0f); GameTestDriver.Set(area, "IsCaptured", true);
            GameTestDriver.Set(GameTestDriver.Get(_game, "Clock"), "Remaining", 0f);
            yield return null; yield return null;
            Assert.That(GameTestDriver.Get(_game, "Phase").ToString(), Is.EqualTo("Lost"));
        }
        [UnityTest]
        public IEnumerator RifleRefillKeepsLoadedRoundsAndFullMagazineHudArithmetic()
        {
            EnterRoom(); yield return null;
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            Assert.That(GameTestDriver.Call(equipment, "TryCollectWeapon", 1), Is.True);
            yield return null;
            var weapon = GameTestDriver.Call(equipment, "getActiveWeapon");
            GameTestDriver.Set(weapon, "CurrentMagAmmo", 15);
            GameTestDriver.Set(weapon, "CurrentAmmo", 46);
            Assert.That(GameTestDriver.Call(equipment, "TryCollectWeapon", 1), Is.True);
            Assert.That(GameTestDriver.Get(weapon, "CurrentMagAmmo"), Is.EqualTo(15));
            Assert.That(GameTestDriver.Get(weapon, "ReserveAmmo"), Is.EqualTo(140));
            Assert.That(GameTestDriver.Get(weapon, "FullReserveMagazines"), Is.EqualTo(4));
        }
        [UnityTest]
        public IEnumerator MovementKeepsAuthoredSpeedAcrossFrameRates()
        {
            EnterRoom(); yield return new WaitForSeconds(.75f);
            var controller = GameTestDriver.Component(_player.gameObject, "PlayerController");
            var input = Activator.CreateInstance(GameTestDriver.Type("AnalogInputState"));
            GameTestDriver.Call(controller, "InitializeInput", input);
            var body = _player.GetComponent<Rigidbody>();
            int oldRate = Application.targetFrameRate;
            int oldVsync = QualitySettings.vSyncCount;
            try
            {
                QualitySettings.vSyncCount = 0;
                body.useGravity = false;
                foreach (int frameRate in new[] { 30, 120 })
                {
                    Application.targetFrameRate = frameRate;
                    body.position = new Vector3(-40f, 20f, -43f);
                    yield return new WaitForFixedUpdate();
                    Vector3 start = body.position;
                    GameTestDriver.Set(input, "Movement", Vector2.right);
                    yield return new WaitForSeconds(.6f);
                    GameTestDriver.Set(input, "Movement", Vector2.zero);
                    yield return new WaitForFixedUpdate();
                    Assert.That(Vector3.Distance(start, body.position), Is.InRange(1.2f, 1.9f),
                        "Walking speed changed at target FPS " + frameRate);
                }
            }
            finally
            {
                GameTestDriver.Set(input, "Movement", Vector2.zero);
                Application.targetFrameRate = oldRate;
                QualitySettings.vSyncCount = oldVsync;
            }
        }

        [UnityTest]
        public IEnumerator CatalogChestGrantsRespectLimitsAndCanBeCollectedAgain()
        {
            EnterRoom(); yield return null;
            var chest = Object.FindObjectsByType(GameTestDriver.Type("ChestAlwaysFull"), FindObjectsInactive.Include)
                .Cast<Component>().First(c => c.gameObject.scene == _game.gameObject.scene && (int)GameTestDriver.Get(c, "snares") > 0);
            GameTestDriver.Set(chest, "active", true);
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            var placement = GameTestDriver.Component(_player.gameObject, "PlacementController");
            var counts = (int[])GameTestDriver.Get(placement, "placeableCount");
            var limits = (int[])GameTestDriver.Get(placement, "maxPlaceable");
            GameTestDriver.Set(chest, "alreadyPicked", false);
            GameTestDriver.Call(equipment, "CollectChest", chest);
            Assert.That(counts[0], Is.EqualTo(Mathf.Min(limits[0], (int)GameTestDriver.Get(chest, "snares"))));
            int[] once = (int[])counts.Clone();
            GameTestDriver.Call(equipment, "CollectChest", chest);
            Assert.That(counts, Is.EqualTo(once), "A duplicate call in the same frame granted twice");
            GameTestDriver.Set(chest, "alreadyPicked", false);
            for (int attempt = 0; attempt < 10; attempt++)
            {
                GameTestDriver.Call(equipment, "CollectChest", chest);
                GameTestDriver.Set(chest, "alreadyPicked", false);
            }
            for (int i = 0; i < counts.Length; i++)
                Assert.That(counts[i], Is.InRange(0, limits[i]));
            Assert.That(counts[0], Is.EqualTo(limits[0]));
        }

        [UnityTest]
        public IEnumerator GhostCrossingBricksDoesNotFreezeOrConsumeCharges()
        {
            EnterRoom(); yield return null;
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var spawnPoints = (IList)GameTestDriver.Get(room, "EnemySpawnPoint");
            var position = ((GameObject)spawnPoints[0]).transform.position;
            var ghostPrefab = (EnemyPrefabs(true))[0];
            var ghost = Object.Instantiate(ghostPrefab, position, Quaternion.identity);
            var spawn = GameTestDriver.Type("Mirror.NetworkServer").GetMethods().Single(method => method.Name == "Spawn"
                && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType.Name == "NetworkConnectionToClient");
            spawn.Invoke(null, new object[] { ghost, null });
            yield return null;
            var enemy = GameTestDriver.Component(ghost, "EnemyControllerServer");
            Assert.That(GameTestDriver.Call(enemy, "TryFreeze", 2f), Is.False);
            var placement = GameTestDriver.Component(_player.gameObject, "PlacementController");
            var prefabs = (System.Collections.Generic.IReadOnlyList<GameObject>)GameTestDriver.Get(placement, "PlaceablePrefabs");
            var bricks = Object.Instantiate(prefabs[0], position, Quaternion.identity);
            spawn.Invoke(null, new object[] { bricks, null });
            var trap = GameTestDriver.Component(bricks, "Snares");
            var handler = ghost.GetComponentInChildren(GameTestDriver.Type("TrapsHandler"), true);
            handler.SendMessage("OnTriggerEnter", bricks.GetComponent<Collider>(), SendMessageOptions.RequireReceiver);
            Assert.That(GameTestDriver.Get(trap, "EnemiesCounter"), Is.EqualTo(0));
            Assert.That(GameTestDriver.Get(enemy, "CurrentState").ToString(), Is.Not.EqualTo("Blocked"));
        }

        [UnityTest]
        public IEnumerator ConsumedPlaceablesReleaseTheAuthoritativeCellBeforeDespawn()
        {
            EnterRoom(); yield return null;
            var room = GameTestDriver.Get(GameTestDriver.Get(_game, "World"), "CurrentRoom");
            var cells = (System.Collections.IEnumerable)GameTestDriver.Get(room, "GridPoints");
            var cell = cells.Cast<Component>().First();
            string id = (string)GameTestDriver.Get(cell, "Id");
            var placement = GameTestDriver.Component(_player.gameObject, "PlacementController");
            var prefabs = (System.Collections.Generic.IReadOnlyList<GameObject>)GameTestDriver.Get(placement, "PlaceablePrefabs");
            var spawn = GameTestDriver.Type("Mirror.NetworkServer").GetMethods().Single(method => method.Name == "Spawn"
                && method.GetParameters().Length == 2 && method.GetParameters()[1].ParameterType.Name == "NetworkConnectionToClient");
            var body = _player.GetComponent<Rigidbody>();
            body.isKinematic = true; body.position = new Vector3(-40f, 20f, -40f);
            foreach (int index in new[] { 0, 1, 2 })
            {
                var item = Object.Instantiate(prefabs[index], cell.transform.position, Quaternion.identity);
                var behavior = GameTestDriver.Component(item, new[] { "Snares", "TeddyBearServer", "Barrel" }[index]);
                GameTestDriver.Call(behavior, "InitializeCell", id);
                spawn.Invoke(null, new object[] { item, null });
                yield return null;
                GameTestDriver.Set(cell, "Buildable", false);
                if (index == 0)
                    for (int hit = 0; hit < (int)GameTestDriver.Get(behavior, "EnemiesToDestroy"); hit++)
                        GameTestDriver.Call(behavior, "RegisterCaughtEnemy");
                else GameTestDriver.Call(behavior, index == 1 ? "Despawn" : "Explode");
                Assert.That(GameTestDriver.Get(cell, "Buildable"), Is.True,
                    "Authoritative cell remains occupied for item " + index);
                yield return new WaitForSeconds(.25f);
            }
        }

        [UnityTest]
        public IEnumerator WeaponSwapUsesDefinitionPoseAndKeepsCollectionBlockedUntilComplete()
        {
            EnterRoom(); yield return null;
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            var shoot = (MonoBehaviour)GameTestDriver.Component(_player.gameObject, "PlayerShoot");
            Assert.That(GameTestDriver.Call(equipment, "TryCollectWeapon", 1), Is.True);
            var pistol = (Component)GameTestDriver.Get(equipment, "Weapon1");
            var rifle = (Component)GameTestDriver.Get(equipment, "Weapon2");
            var original = (Object)GameTestDriver.Get(pistol, "Definition");
            var definition = Object.Instantiate(original);
            var pose = new Vector3(.021f, .031f, -.221f);
            GameTestDriver.Set(definition, "FirstPersonPosition", pose);
            GameTestDriver.Set(pistol, "_definition", definition);
            var presentation = (Behaviour)GameTestDriver.Get(shoot, "_presentation");
            bool presentationEnabled = presentation.enabled;
            var pistolAnimator = pistol.GetComponent<Animator>();
            bool animatorEnabled = pistolAnimator.enabled;
            pistolAnimator.enabled = false;
            presentation.enabled = false;
            try
            {
                shoot.StartCoroutine((IEnumerator)GameTestDriver.Call(shoot, "HideWeapon", rifle.gameObject, pistol.gameObject));
                Assert.That(GameTestDriver.Get(shoot, "IsSwitchingWeapon"), Is.True);
                Assert.That(GameTestDriver.Call(equipment, "TryCollectWeapon", 1), Is.False);
                yield return new WaitForSeconds(1.1f);
                Assert.That(GameTestDriver.Get(shoot, "IsSwitchingWeapon"), Is.False);
                Assert.That(GameTestDriver.Call(equipment, "getActiveWeapon"), Is.SameAs(pistol));
                Assert.That(Vector3.Distance(pistol.transform.localPosition, pose), Is.LessThan(.00001f));
            }
            finally
            {
                pistolAnimator.enabled = animatorEnabled;
                presentation.enabled = presentationEnabled;
                GameTestDriver.Set(pistol, "_definition", original);
                Object.Destroy(definition);
            }
        }

        [UnityTest]
        public IEnumerator DisablingShootCancelsSwapAndRestoresThePreviouslyHeldWeapon()
        {
            EnterRoom(); yield return null;
            var equipment = GameTestDriver.Component(_player.gameObject, "PlayerEquipment");
            var shoot = (MonoBehaviour)GameTestDriver.Component(_player.gameObject, "PlayerShoot");
            Assert.That(GameTestDriver.Call(equipment, "TryCollectWeapon", 1), Is.True);
            var pistol = (Component)GameTestDriver.Get(equipment, "Weapon1");
            var rifle = (Component)GameTestDriver.Get(equipment, "Weapon2");
            shoot.StartCoroutine((IEnumerator)GameTestDriver.Call(shoot, "HideWeapon", rifle.gameObject, pistol.gameObject));
            shoot.enabled = false;
            yield return new WaitForSeconds(1.1f);
            Assert.That(GameTestDriver.Get(shoot, "IsSwitchingWeapon"), Is.False);
            Assert.That(rifle.gameObject.activeSelf, Is.True);
            Assert.That(pistol.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator InventoryHudCanRenderAMappedFourthCatalogSlot()
        {
            EnterRoom(); yield return null;
            var placement = GameTestDriver.Component(_player.gameObject, "PlacementController");
            var originalCounts = (int[])GameTestDriver.Get(placement, "placeableCount");
            var originalViews = (Array)GameTestDriver.Get(placement, "_placeableTM");
            var views = Array.CreateInstance(originalViews.GetType().GetElementType(), 4);
            for (int i = 0; i < 3; i++) views.SetValue(originalViews.GetValue(i), i);
            views.SetValue(originalViews.GetValue(0), 3);
            try
            {
                GameTestDriver.Set(placement, "placeableCount", new[] { 1, 2, 3, 4 });
                GameTestDriver.Set(placement, "_placeableTM", views);
                GameTestDriver.Call(placement, "UpdateInventoryHud");
                Assert.That(GameTestDriver.Get(views.GetValue(3), "text"), Is.EqualTo("4"));
            }
            finally
            {
                GameTestDriver.Set(placement, "placeableCount", originalCounts);
                GameTestDriver.Set(placement, "_placeableTM", originalViews);
            }
        }

    }
}
