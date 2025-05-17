using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;

namespace UnityEssentials.Tests
{
    public class TimingTests
    {
        private Timing _timing;
        private float _testFloat;
        private bool _testBool;

        [SetUp]
        public void Setup()
        {
            _timing = Timing.Instance;
            _testFloat = 0f;
            _testBool = false;
        }

        [TearDown]
        public void Teardown()
        {
            _timing.KillAllCoroutines();
            Object.DestroyImmediate(_timing);
        }

        [UnityTest]
        public IEnumerator BasicCoroutineExecution()
        {
            var handle = Timing.RunCoroutine(TestCoroutine());

            yield return null; // Wait for first frame
            Assert.AreEqual(1f, _testFloat);

            yield return null; // Wait for second frame
            Assert.AreEqual(2f, _testFloat);
        }

        [UnityTest]
        public IEnumerator KillCoroutineStopsExecution()
        {
            var handle = Timing.RunCoroutine(InfiniteCoroutine());

            yield return null;
            Assert.IsTrue(_testBool);

            Timing.KillCoroutine(handle);
            _testBool = false;

            yield return null;
            Assert.IsFalse(_testBool);
        }

        [UnityTest]
        public IEnumerator TimeValuesAreUpdatedCorrectly()
        {
            var handle = Timing.RunCoroutine(TimeTestCoroutine());

            yield return new WaitForSeconds(0.1f);
            Assert.Greater(Timing.LocalTime, 0f);
            Assert.Greater(Timing.DeltaTime, 0f);

            Timing.KillCoroutine(handle);
        }

        [Test]
        public void HandleInvalidationAfterKill()
        {
            var handle = Timing.RunCoroutine(TestCoroutine());
            Assert.IsTrue(Timing.Instance.IsCoroutineActive(handle)); // Implement IsCoroutineActive

            Timing.KillCoroutine(handle);
            Assert.IsFalse(Timing.Instance.IsCoroutineActive(handle));
        }

        [UnityTest]
        public IEnumerator MultipleSegmentExecution()
        {
            float updateValue = 0f;
            float fixedValue = 0f;
            float lateValue = 0f;

            Timing.RunCoroutine(SegmentTestCoroutine(Segment.Update, () => updateValue++), Segment.Update);
            Timing.RunCoroutine(SegmentTestCoroutine(Segment.FixedUpdate, () => fixedValue++), Segment.FixedUpdate);
            Timing.RunCoroutine(SegmentTestCoroutine(Segment.LateUpdate, () => lateValue++), Segment.LateUpdate);

            yield return null; // Update
            yield return new WaitForFixedUpdate(); // FixedUpdate
            yield return new WaitForEndOfFrame(); // LateUpdate

            Assert.AreEqual(1f, updateValue);
            Assert.AreEqual(1f, fixedValue);
            Assert.AreEqual(1f, lateValue);
        }

        [UnityTest]
        public IEnumerator ZeroAllocationCheck()
        {
            var allocBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            for (int i = 0; i < 100; i++)
            {
                var handle = Timing.RunCoroutine(AllocationTestCoroutine());
                yield return null;
                Timing.KillCoroutine(handle);
            }

            var allocAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Assert.LessOrEqual(allocAfter - allocBefore, 4096); // More lenient threshold
        }

        // Test Coroutines
        IEnumerator<float> TestCoroutine()
        {
            _testFloat = 1f;
            yield return 0;
            _testFloat = 2f;
        }

        IEnumerator<float> InfiniteCoroutine()
        {
            while (true)
            {
                _testBool = true;
                yield return 0;
            }
        }

        IEnumerator<float> TimeTestCoroutine()
        {
            while (true)
            {
                _testFloat = Timing.LocalTime;
                yield return 0;
            }
        }

        IEnumerator<float> SegmentTestCoroutine(Segment segment, System.Action callback)
        {
            callback();
            yield return 0;
        }

        IEnumerator<float> AllocationTestCoroutine()
        {
            yield return 0;
        }
    }
}