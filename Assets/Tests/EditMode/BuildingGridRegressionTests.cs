using System;
using System.Collections;
using System.Linq;
using Nightmare.Core.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nightmare.Tests
{
    /// <summary>Checks legacy grid geometry and isolated authoring hierarchy generation without opening experimental scenes.</summary>
    public class BuildingGridRegressionTests
    {
        private Scene _testScene;
        private float _tileSize;

        [SetUp]
        public void OpenTemporaryScene()
        {
            _tileSize = (float)GameTestDriver.Get(GameTestDriver.Type("PlacementController"), "GridTileSize");
            GameTestDriver.Set(GameTestDriver.Type("PlacementController"), "GridTileSize", 1f);
            _testScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void CloseTemporaryScene()
        {
            GameTestDriver.Set(GameTestDriver.Type("PlacementController"), "GridTileSize", _tileSize);
            if (_testScene.IsValid()) EditorSceneManager.ClosePreviewScene(_testScene);
        }

        private GameObject CreateObject(string name, params Type[] components)
        {
            var created = new GameObject(name, components);
            SceneManager.MoveGameObjectToScene(created, _testScene);
            return created;
        }

        [TestCase(4, 2, 1f, 15)]
        [TestCase(3, 3, 1f, 9)]
        [TestCase(2, 2, .5f, 36)]
        [TestCase(0, 0, 1f, 1)]
        public void LayoutKeepsOriginalExtentsAndXMajorTraversal(int width, int height, float tile, int expectedCount)
        {
            var cells = BuildingGridLayout.Positions(3.25f, -1.75f, width, height, tile).ToArray();
            Assert.That(cells.Length, Is.EqualTo(expectedCount));
            Assert.That(cells[0].X, Is.EqualTo(3.25f - width / 2));
            Assert.That(cells[0].Z, Is.EqualTo(-1.75f - height / 2));
            if (cells.Length > 1)
            {
                Assert.That(cells[1].X, Is.EqualTo(cells[0].X));
                Assert.That(cells[1].Z, Is.EqualTo(cells[0].Z + tile));
            }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidTileSizesFailInsteadOfHangingTheEditor(float tile)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BuildingGridLayout.Positions(0f, 0f, 2, 2, tile).ToArray());
        }

        [Test]
        public void RebuildingCanvasRemovesEveryOldChild()
        {
            var canvas = CreateObject("Grid cleanup fixture");
            var children = Enumerable.Range(0, 5).Select(index => CreateObject("Old room " + index)).ToArray();
            foreach (var child in children) child.transform.SetParent(canvas.transform);
            GameTestDriver.Call(GameTestDriver.Type("BuildingPointsGenerator"), "ClearCanvas", canvas.transform);
            Assert.That(canvas.transform.childCount, Is.Zero);
            Assert.That(children.All(child => child == null), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GeneratedRoomBindsGridAndSpawnsRegardlessOfOptionalChest(bool chest)
        {
            var host = CreateObject("Inactive generator fixture");
            host.SetActive(false);
            var generator = host.AddComponent(GameTestDriver.Type("BuildingPointsGenerator"));
            var visual = CreateObject("Grid visual fixture", typeof(SpriteRenderer));
            visual.SetActive(false);
            GameTestDriver.Set(generator, "_gridPointPrefab", visual);
            GameTestDriver.Set(generator, "_areaPrefab", CreateObject("Unused capture template"));
            GameTestDriver.Set(generator, "_chestPrefab", CreateObject("Chest fixture"));
            var roomObject = CreateObject("Generated room fixture");
            roomObject.transform.SetPositionAndRotation(new Vector3(3.25f, 2.5f, -1.75f), Quaternion.Euler(0f, 37f, 0f));
            var room = roomObject.AddComponent(GameTestDriver.Type("Room"));
            GameTestDriver.Set(room, "_width", 4);
            GameTestDriver.Set(room, "_height", 2);
            GameTestDriver.Set(room, "_hasChest", chest);
            GameTestDriver.Set(room, "_enemySpawnPointsCounter", 2);
            GameTestDriver.Call(generator, "GenerateRoom", room, 7);
            var points = ((IEnumerable)GameTestDriver.Get(room, "GridPoints")).Cast<Component>().ToArray();
            Assert.That(points.Length, Is.EqualTo(15));
            Assert.That(((IEnumerable)GameTestDriver.Get(room, "EnemySpawnPoint")).Cast<object>().Count(), Is.EqualTo(2));
            Assert.That(((IEnumerable)GameTestDriver.Get(room, "CaptureAreas")).Cast<object>().Count(), Is.Zero);
            Assert.That(Vector3.Distance(points[0].transform.position, new Vector3(1.25f, 2.5f, -2.75f)), Is.LessThan(.00001f));
            Assert.That(points.All(point => GameTestDriver.Call(point, "getSpriteRenderer") != null), Is.True);
            GameTestDriver.Call(room, "Setup");
            Assert.That(((IEnumerable)GameTestDriver.Get(room, "GridPoints")).Cast<object>().Count(), Is.EqualTo(15));
        }
    }
}
