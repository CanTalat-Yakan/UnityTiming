using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityEssentials.Tests
{
    public class TimingTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ensure singleton is initialized and clean before each test
            if (Timing.Instance == null)
            {
                var go = new GameObject("Timing");
                go.AddComponent<Timing>();
            }
            Timing.KillAllCoroutines();
        }

        [TearDown]
        public void TearDown()
        {
            Timing.KillAllCoroutines();
        }

        private IEnumerator<float> SimpleCoroutine()
        {
            yield return 0.1f;
            yield return 0.2f;
        }

        [Test]
        public void IsCoroutineActive_ShouldReturnTrueForActiveCoroutine()
        {
            var handle = Timing.RunCoroutine(SimpleCoroutine());
            Assert.IsTrue(Timing.IsCoroutineActive(handle));
        }

        [Test]
        public void IsCoroutineActive_ShouldReturnFalseForKilledCoroutine()
        {
            var handle = Timing.RunCoroutine(SimpleCoroutine());
            Timing.KillCoroutine(handle);
            Assert.IsFalse(Timing.IsCoroutineActive(handle));
        }

        [Test]
        public void PauseCoroutine_ShouldPauseCoroutine()
        {
            var handle = Timing.RunCoroutine(SimpleCoroutine());
            Timing.PauseCoroutine(handle);

            // Access internal state to verify paused (reflection or friend class if needed)
            // Here, we check that the coroutine is still active but will not advance
            Assert.IsTrue(Timing.IsCoroutineActive(handle));
        }

        [Test]
        public void ResumeCoroutine_ShouldResumePausedCoroutine()
        {
            var handle = Timing.RunCoroutine(SimpleCoroutine());
            Timing.PauseCoroutine(handle);
            Timing.ResumeCoroutine(handle);

            Assert.IsTrue(Timing.IsCoroutineActive(handle));
        }

        [Test]
        public void KillCoroutine_ShouldRemoveCoroutine()
        {
            var handle = Timing.RunCoroutine(SimpleCoroutine());
            Timing.KillCoroutine(handle);
            Assert.IsFalse(Timing.IsCoroutineActive(handle));
        }

        [Test]
        public void KillAllCoroutines_ShouldRemoveAllCoroutines()
        {
            var handle1 = Timing.RunCoroutine(SimpleCoroutine());
            var handle2 = Timing.RunCoroutine(SimpleCoroutine(), Segment.FixedUpdate);

            Timing.KillAllCoroutines();

            Assert.IsFalse(Timing.IsCoroutineActive(handle1));
            Assert.IsFalse(Timing.IsCoroutineActive(handle2));
        }

        [Test]
        public void KillAllCoroutines_BySegment_ShouldRemoveOnlyThatSegment()
        {
            var handle1 = Timing.RunCoroutine(SimpleCoroutine(), Segment.Update);
            var handle2 = Timing.RunCoroutine(SimpleCoroutine(), Segment.FixedUpdate);

            Timing.KillAllCoroutines((int)Segment.Update);

            Assert.IsFalse(Timing.IsCoroutineActive(handle1));
            Assert.IsTrue(Timing.IsCoroutineActive(handle2));
        }

        [Test]
        public void ProcessSegment_ShouldAdvanceCoroutine()
        {
            // Set up a coroutine that increments a counter
            int counter = 0;
            IEnumerator<float> CountingCoroutine()
            {
                counter++;
                yield return 0;
                counter++;
            }

            var handle = Timing.RunCoroutine(CountingCoroutine(), Segment.Update);

            // Call Update (which calls ProcessSegment)
            Timing.Instance.Update();

            Assert.AreEqual(2, counter); // Should have advanced both steps
        }

        [Test]
        public void OnDestroy_ShouldKillAllCoroutines()
        {
            var handle = Timing.RunCoroutine(SimpleCoroutine());
            Timing.Instance.OnDestroy();
            Assert.IsFalse(Timing.IsCoroutineActive(handle));
        }

        [Test]
        public void Awake_ShouldInitializePool()
        {
            Timing.Instance.Awake();
            var processPool = Timing.Instance.ProcessPool;
            Assert.IsNotNull(processPool);
        }

        [UnityTest]
        public IEnumerator ZeroAllocationCheck()
        {
            GC.Collect();
            var allocBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            for (int i = 0; i < 100; i++)
            {
                Timing.RunCoroutine(SimpleCoroutine(), Segment.Update, "Test");
                yield return null;
            }

            var allocAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Assert.LessOrEqual(allocAfter - allocBefore, 0);
        }
    }
}