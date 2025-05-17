using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using static UnityEssentials.Timing;

namespace UnityEssentials.Tests
{
    public class TimingTests
    {
        private GameObject _testObject;
        private Timing _timing;
        private int _testCounter;
        private bool _testFlag;

        [SetUp]
        public void Setup()
        {
            _testObject = new GameObject("TimingTest");
            _timing = _testObject.AddComponent<Timing>();
            _testCounter = 0;
            _testFlag = false;
            Timing.ReplacementFunction = null;
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_testObject);
            _timing = null;
        }

        [UnityTest]
        public IEnumerator BasicCoroutineExecution()
        {
            // Arrange
            var handle = Timing.RunCoroutine(TestCoroutine());

            // Act
            yield return null;
            _timing.ProcessSegment(Segment.Update);

            // Assert
            Assert.AreEqual(1, _testCounter);
            Assert.IsFalse(handle.IsValid, "Coroutine should complete and be auto-removed");

            IEnumerator<float> TestCoroutine()
            {
                _testCounter++;
                yield break;
            }
        }

        [Test]
        public void FrameWaiting()
        {
            // Arrange
            var handle = Timing.RunCoroutine(MultiFrameCoroutine());

            // Act & Assert
            _timing.ProcessSegment(Segment.Update);
            Assert.AreEqual(1, _testCounter);

            _timing.ProcessSegment(Segment.Update);
            Assert.AreEqual(2, _testCounter);

            _timing.ProcessSegment(Segment.Update);
            Assert.AreEqual(3, _testCounter);
            Assert.IsFalse(handle.IsValid);

            IEnumerator<float> MultiFrameCoroutine()
            {
                _testCounter++;
                yield return Timing.WaitForOneFrame;
                _testCounter++;
                yield return Timing.WaitForOneFrame;
                _testCounter++;
            }
        }

        [UnityTest]
        public IEnumerator TimeBasedWaiting()
        {
            // Arrange
            var handle = Timing.RunCoroutine(TimeDelayCoroutine());

            // Act - First process (should wait)
            _timing.ProcessSegment(Segment.Update);
            Assert.AreEqual(0, _testCounter);

            // Simulate time passing
            GetSegmentData(Segment.Update).LastTime = 0.5f;

            // Act - Second process
            _timing.ProcessSegment(Segment.Update);
            Assert.AreEqual(1, _testCounter);

            IEnumerator<float> TimeDelayCoroutine()
            {
                yield return 0.5f;
                _testCounter++;
            }

            yield return 0;
        }

        [Test]
        public void CoroutineReplacement()
        {
            // Arrange
            Timing.ReplacementFunction = ReplacementHandler;
            var handle = Timing.RunCoroutine(OriginalCoroutine());

            // Act
            _timing.ProcessSegment(Segment.Update);
            _timing.ProcessSegment(Segment.Update);

            // Assert
            Assert.AreEqual(2, _testCounter);

            IEnumerator<float> OriginalCoroutine()
            {
                yield return float.NaN;
            }

            IEnumerator<float> ReplacementHandler(IEnumerator<float> original, CoroutineHandle handle)
            {
                return ReplacementCoroutine();
            }

            IEnumerator<float> ReplacementCoroutine()
            {
                _testCounter++;
                yield return Timing.WaitForOneFrame;
                _testCounter++;
            }
        }

        [Test]
        public void CoroutineKilling()
        {
            // Arrange
            var handle = Timing.RunCoroutine(InfiniteCoroutine());

            // Act
            _timing.ProcessSegment(Segment.Update);
            Timing.KillCoroutines(handle);
            _timing.ProcessSegment(Segment.Update);

            // Assert
            Assert.AreEqual(1, _testCounter);

            IEnumerator<float> InfiniteCoroutine()
            {
                while (true)
                {
                    _testCounter++;
                    yield return Timing.WaitForOneFrame;
                }
            }
        }

        [Test]
        public void CurrentCoroutineTracking()
        {
            // Arrange
            CoroutineHandle currentHandle = default;
            var handle = Timing.RunCoroutine(HandleTrackingCoroutine());

            // Act
            _timing.ProcessSegment(Segment.Update);

            // Assert
            Assert.AreEqual(handle, currentHandle);

            IEnumerator<float> HandleTrackingCoroutine()
            {
                currentHandle = Timing.CurrentCoroutine;
                yield break;
            }
        }

        [Test]
        public void MultipleSegments()
        {
            // Arrange
            var updateHandle = Timing.RunCoroutine(SegmentTestCoroutine(), Segment.Update);
            var fixedHandle = Timing.RunCoroutine(SegmentTestCoroutine(), Segment.FixedUpdate);

            // Act
            _timing.ProcessSegment(Segment.Update);
            _timing.ProcessSegment(Segment.FixedUpdate);

            // Assert
            Assert.AreEqual(2, _testCounter);

            IEnumerator<float> SegmentTestCoroutine()
            {
                _testCounter++;
                yield break;
            }
        }

        [Test]
        public void ExceptionHandling()
        {
            // Arrange
            LogAssert.ignoreFailingMessages = true;
            var handle = Timing.RunCoroutine(FailingCoroutine());

            // Act & Assert (shouldn't throw)
            Assert.DoesNotThrow(() => _timing.ProcessSegment(Segment.Update));

            IEnumerator<float> FailingCoroutine()
            {
                throw new System.Exception("Test exception");
            }
        }

        private SegmentData GetSegmentData(Segment segment) =>
            Timing.Instance._segments[(int)segment];
    }
}