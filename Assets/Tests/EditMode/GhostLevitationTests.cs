using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nightmare.Tests
{
    /// <summary>Checks bounded, deterministic visual motion, scaling independence and death/reset lifecycle without driving game state.</summary>
    public class GhostLevitationTests
    {
        private GameObject _root;
        private GhostLevitation _motion;
        private Vector3 _restPosition;
        private Quaternion _restRotation;

        [SetUp]
        public void CreateVisualPivot()
        {
            _root = new GameObject("Levitation fixture");
            _root.transform.localScale = Vector3.one * 1.2f;
            _root.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            var pivot = new GameObject("Visual pivot");
            pivot.transform.SetParent(_root.transform, false);
            pivot.transform.localPosition = new Vector3(0f, .12f, -.35f);
            _motion = pivot.AddComponent<GhostLevitation>();
            GameTestDriver.Call(_motion, "Awake");
            _restPosition = pivot.transform.localPosition;
            _restRotation = pivot.transform.localRotation;
        }
        [TearDown]
        public void DestroyVisualPivot() { Object.DestroyImmediate(_root); }

        [Test]
        public void SamplesStayWithinWorldBoundsWithoutMovingTheParent()
        {
            var position = _root.transform.position;
            var rotation = _root.transform.rotation;
            var origin = _root.transform.TransformPoint(_restPosition);
            for (int i = 0; i <= 120; i++)
            {
                _motion.Present(i / 60.0, 27);
                var offset = _motion.transform.position - origin;
                Assert.That(offset.y, Is.InRange(-.00001f, .08001f));
                Assert.That(new Vector2(offset.x, offset.z).magnitude, Is.LessThanOrEqualTo(.01501f));
                Assert.That(Quaternion.Angle(_restRotation, _motion.transform.localRotation), Is.LessThan(2.3f));
            }
            Assert.That(_root.transform.position, Is.EqualTo(position));
            Assert.That(_root.transform.rotation, Is.EqualTo(rotation));
        }
        [Test]
        public void AbsoluteTimeIsPeriodicAndDoesNotAccumulateDrift()
        {
            _motion.Present(1234.25, 17);
            var position = _motion.transform.localPosition;
            var rotation = _motion.transform.localRotation;
            for (int i = 0; i < 500; i++) _motion.Present(i * .13, 17);
            _motion.Present(1234.25, 17);
            Assert.That(Vector3.Distance(position, _motion.transform.localPosition), Is.LessThan(.000001f));
            Assert.That(Quaternion.Angle(rotation, _motion.transform.localRotation), Is.LessThan(.001f));
            _motion.Present(1236.25, 17);
            Assert.That(Vector3.Distance(position, _motion.transform.localPosition), Is.LessThan(.000001f));
        }
        [Test]
        public void WorldTravelIsIndependentOfUniformParentScale()
        {
            var origin = _root.transform.TransformPoint(_restPosition);
            _motion.Present(.3, 5);
            var offset = _motion.transform.position - origin;
            _root.transform.localScale = Vector3.one * .6f;
            origin = _root.transform.TransformPoint(_restPosition);
            _motion.Present(.3, 5);
            Assert.That(Vector3.Distance(offset, _motion.transform.position - origin), Is.LessThan(.00001f));
        }
        [Test]
        public void FreezeHoldsThePoseAndDisableRestoresAuthoredTransform()
        {
            _motion.Present(.37, 8);
            var position = _motion.transform.localPosition;
            var rotation = _motion.transform.localRotation;
            _motion.Freeze();
            _motion.Present(10, 8);
            Assert.That(_motion.transform.localPosition, Is.EqualTo(position));
            Assert.That(_motion.transform.localRotation, Is.EqualTo(rotation));
            GameTestDriver.Call(_motion, "OnDisable");
            Assert.That(_motion.transform.localPosition, Is.EqualTo(_restPosition));
            Assert.That(_motion.transform.localRotation, Is.EqualTo(_restRotation));
            _motion.Present(.37, 8);
            Assert.That(Vector3.Distance(position, _motion.transform.localPosition), Is.LessThan(.00001f));
        }
        [Test]
        public void StableEntitySeedsVaryPhaseWithoutConsumingGameplayRandomness()
        {
            var state = Random.state;
            try
            {
                float expected = Random.value;
                Random.state = state;
                _motion.Present(.2, 1);
                var first = _motion.transform.localPosition;
                _motion.Present(.2, 2);
                Assert.That(Vector3.Distance(first, _motion.transform.localPosition), Is.GreaterThan(.001f));
                Assert.That(Random.value, Is.EqualTo(expected));
            }
            finally { Random.state = state; }
        }
    }
}
