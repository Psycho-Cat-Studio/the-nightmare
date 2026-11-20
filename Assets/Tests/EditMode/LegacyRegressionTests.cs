using System;
using Nightmare.Core.Match;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nightmare.Tests
{
    /// <summary>Characterizes authored assets and compatibility behavior across the modular migration.</summary>
    public class LegacyRegressionTests
    {
        private GameObject _instance;
        private Component CreateWeapon(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Weapons/{name}/{name}.prefab");
            _instance = Object.Instantiate(prefab);
            var weapon = GameTestDriver.Component(_instance, "Weapon");
            GameTestDriver.Call(weapon, "Start");
            return weapon;
        }

        [TearDown] public void Cleanup() { if (_instance != null) Object.DestroyImmediate(_instance); }

        [TestCase("Pistol", 7, 2000000000)]
        [TestCase("Rifle", 31, 155)]
        public void WeaponDefaultsPreserveAuthoredBalance(string name, int capacity, int total)
        {
            var weapon = CreateWeapon(name);
            Assert.That(GameTestDriver.Get(weapon, "MaxMagAmmo"), Is.EqualTo(capacity));
            Assert.That(GameTestDriver.Get(weapon, "MaxAmmo"), Is.EqualTo(total));
            GameTestDriver.Call(weapon, "shoot");
            Assert.That(GameTestDriver.Get(weapon, "CurrentMagAmmo"), Is.EqualTo(capacity - 1));
            Assert.That(GameTestDriver.Get(weapon, "CurrentAmmo"), Is.EqualTo(total - 1));
        }

        [TestCase(0, 0)] [TestCase(1, 1)] [TestCase(15, 15)] [TestCase(31, 31)] [TestCase(62, 31)] [TestCase(155, 31)]
        public void ReloadTransfersExistingRoundsWithoutCreatingAmmo(int total, int expectedMagazine)
        {
            var weapon = CreateWeapon("Rifle");
            GameTestDriver.Set(weapon, "CurrentMagAmmo", 0);
            GameTestDriver.Set(weapon, "CurrentAmmo", total);
            GameTestDriver.Call(weapon, "reload");
            GameTestDriver.Call(weapon, "Tick", 3f);
            Assert.That(GameTestDriver.Get(weapon, "CurrentMagAmmo"), Is.EqualTo(expectedMagazine));
            Assert.That(GameTestDriver.Get(weapon, "CurrentAmmo"), Is.EqualTo(total));
        }

        [Test]
        public void PistolStartsSingleAndPreservesItsAuthoredModeCycle()
        {
            var weapon = CreateWeapon("Pistol");
            Assert.That(GameTestDriver.Get(weapon, "Mode").ToString(), Is.EqualTo("single"));
            foreach (var expected in new[] { "triple", "continous", "single" })
            {
                GameTestDriver.Call(weapon, "changeFireMode");
                Assert.That(GameTestDriver.Get(weapon, "Mode").ToString(), Is.EqualTo(expected));
            }
        }

        [TestCase(MatchPhase.Prepare, 0, MatchPhase.Defend, 0)]
        [TestCase(MatchPhase.Defend, 0, MatchPhase.Prepare, 1)]
        [TestCase(MatchPhase.Prepare, 1, MatchPhase.Defend, 1)]
        [TestCase(MatchPhase.Defend, 1, MatchPhase.Prepare, 2)]
        [TestCase(MatchPhase.Prepare, 2, MatchPhase.Defend, 2)]
        [TestCase(MatchPhase.Defend, 2, MatchPhase.Won, 2)]
        public void ExistingCampaignTransitionsStayInOrder(MatchPhase phase, int stage, MatchPhase next, int nextStage)
        {
            Assert.That(new MatchProgress(phase, stage).Next(3), Is.EqualTo(new MatchProgress(next, nextStage)));
        }

        [TestCase("127.0.0.1", "127.0.0.1", 7777)]
        [TestCase("192.168.1.4:7798", "192.168.1.4", 7798)]
        public void DirectConnectionAddressParsingRemainsCompatible(string input, string expectedHost, int expectedPort)
        {
            object[] args = { input, null, 0 };
            Assert.That(GameTestDriver.Call(GameTestDriver.Type("CustomNetworkManager"), "TryParseAddress", args), Is.True);
            Assert.That(args[1], Is.EqualTo(expectedHost));
            Assert.That(args[2], Is.EqualTo(expectedPort));
        }

        [TestCase("Ghost", 60f, 2.8f, false)]
        [TestCase("GhostOrange", 120f, 2.8f, false)]
        [TestCase("GhostRed", 180f, 2.8f, false)]
        [TestCase("Skeleton", 100f, 2f, true)]
        public void EnemyVariantsKeepHealthSpeedAndBrickRules(string name, float health, float speed, bool bricks)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{name}.prefab");
            var enemy = GameTestDriver.Component(prefab, "EnemyControllerServer");
            Assert.That(GameTestDriver.Get(enemy, "_maxHealth"), Is.EqualTo(health));
            Assert.That(GameTestDriver.Get(enemy, "_walkSpeed"), Is.EqualTo(speed));
            Assert.That(GameTestDriver.Get(enemy, "AffectedByBricks"), Is.EqualTo(bricks));
        }

        [Test]
        public void CampaignContentAndSceneBindingsAreComplete()
        {
            GameTestDriver.Call(GameTestDriver.Type("ContentValidation"), "ValidateBuildContent");
        }

        [Test]
        public void OwnedPrefabsHaveNoMissingScripts()
        {
            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, kind) =>
            {
                if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message);
            };
            bool previous = LogAssert.ignoreFailingMessages;
            Application.logMessageReceived += capture;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject), Is.Zero, path);
                }
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previous;
                Application.logMessageReceived -= capture;
            }
            Assert.That(errors.Where(error => !error.StartsWith("PlacementController on PlacementController requires a NetworkIdentity.")), Is.Empty);

        }
    }
}
