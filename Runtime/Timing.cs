using UnityEngine;
using System.Collections.Generic;

namespace UnityEssentials
{
    public enum Segment { Update, FixedUpdate, LateUpdate, SlowUpdate }

    public partial class Timing : PersistentSingleton<Timing>
    {
        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, Segment segment = Segment.Update) =>
            Instance.RunCoroutineInternal(coroutine, (int)segment);

        public static void PauseCoroutine(CoroutineHandle handle)
        {
            var segments = Instance._segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref SegmentData segment = ref segments[s];
                for (int i = 0; i < segment.Count; i++)
                    if (segment.Processes[i].Handle.Equals(handle) &&
                        segment.Processes[i].Coroutine != null)
                    {
                        segment.Processes[i].Paused = true;
                        return;
                    }
            }
        }

        public static void ResumeCoroutine(CoroutineHandle handle)
        {
            var segments = Instance._segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref SegmentData segment = ref segments[s];
                for (int i = 0; i < segment.Count; i++)
                    if (segment.Processes[i].Handle.Equals(handle) &&
                        segment.Processes[i].Coroutine != null)
                    {
                        segment.Processes[i].Paused = false;

                        // Adjust wait time if paused during waiting period
                        if (segment.Processes[i].WaitUntil < LocalTime)
                            segment.Processes[i].WaitUntil = LocalTime;
                        return;
                    }
            }
        }

        public static void KillCoroutine(CoroutineHandle handle)
        {
            var segments = Instance._segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref SegmentData segment = ref segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    if (segment.Processes[i].Handle.Equals(handle))
                    {
                        segment.Processes[i].Coroutine = null;
                        segment.Processes[i].WaitUntil = segment.FreeHead;
                        segment.FreeHead = i;
                        Instance.ReleaseHandle(handle);
                        return;
                    }
                }
            }
        }

        public static void KillAllCoroutines()
        {
            var segments = Instance._segments;
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                SegmentData segment = segments[i];
                for (int j = 0; j < segment.Processes.Length; j++)
                    segment.Processes[j].Coroutine = null;

                segments[i].Count = 0;
                segments[i].FreeHead = -1;
            }
        }

        public static bool IsCoroutineActive(CoroutineHandle handle)
        {
            var segments = Instance._segments;
            foreach (var segment in segments)
                foreach (var process in segment.Processes)
                    if (process.Handle.Equals(handle))
                        return process.Coroutine != null;
            return false;
        }
    }

    public partial class Timing : PersistentSingleton<Timing>
    {
        private const int ProcessArrayChunkSize = 64;
        private const int InitialBufferSize = 256;
        public const float WaitForOneFrame = float.NegativeInfinity;

        public static float LocalTime { get; private set; }
        public static float DeltaTime { get; private set; }

        public struct CoroutineHandle
        {
            public int Id;
            public ushort Version;
            public bool IsValid => Version > 0;
        }

        private struct ProcessData
        {
            public IEnumerator<float> Coroutine;
            public float WaitUntil;
            public bool Paused;
            public bool Held;
            public CoroutineHandle Handle;
        }

        private struct SegmentData
        {
            public ProcessData[] Processes;
            public int Count;
            public int Capacity;
            public int FreeHead;
        }


        private SegmentData[] _segments = new SegmentData[4];
        private CoroutineHandle[] _handlePool = new CoroutineHandle[InitialBufferSize];
        private int _freeHandleIndex = 0;
        private ushort _currentVersion = 1;

        protected override void OnDestroy()
        {
            KillAllCoroutines();
            base.OnDestroy();
        }

        public override void Awake()
        {
            base.Awake();

            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].Processes = new ProcessData[InitialBufferSize];
                _segments[i].Capacity = InitialBufferSize;
                _segments[i].FreeHead = -1;
            }

            // Initialize handle pool
            for (int i = 0; i < _handlePool.Length; i++)
                _handlePool[i] = new CoroutineHandle { Id = i, Version = 0 };
        }

        public void Update() => ProcessSegment(Segment.Update);
        public void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        public void LateUpdate() => ProcessSegment(Segment.LateUpdate);

        private void ProcessSegment(Segment segment)
        {
            int segmentIndex = (int)segment;
            ref SegmentData segmentData = ref _segments[segmentIndex];
            UpdateTimeValues(segment);

            for (int i = 0; i < segmentData.Count; i++)
            {
                if (segmentData.Processes[i].Coroutine == null) continue;
                if (segmentData.Processes[i].Paused || segmentData.Processes[i].Held) continue;
                if (LocalTime < segmentData.Processes[i].WaitUntil) continue;

                try
                {
                    if (!segmentData.Processes[i].Coroutine.MoveNext())
                        KillCoroutine(segmentData.Processes[i].Handle);
                    else
                    {
                        float current = segmentData.Processes[i].Coroutine.Current;
                        segmentData.Processes[i].WaitUntil = float.IsNaN(current) ? 0f : LocalTime + current;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    KillCoroutine(segmentData.Processes[i].Handle);
                }
            }
        }

        private void UpdateTimeValues(Segment segment)
        {
            DeltaTime = segment == Segment.FixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;
            LocalTime = segment == Segment.FixedUpdate ? Time.fixedTime : Time.time;
        }

        private CoroutineHandle RunCoroutineInternal(IEnumerator<float> coroutine, int segmentIndex)
        {
            ref SegmentData segment = ref _segments[segmentIndex];
            int processIndex;

            if (segment.FreeHead >= 0)
            {
                processIndex = segment.FreeHead;
                segment.FreeHead = (int)segment.Processes[processIndex].WaitUntil; // Reuse WaitUntil for free list
            }
            else
            {
                if (segment.Count >= segment.Capacity)
                {
                    segment.Capacity += ProcessArrayChunkSize;
                    System.Array.Resize(ref segment.Processes, segment.Capacity);
                }
                processIndex = segment.Count++;
            }

            CoroutineHandle handle = GetHandle();
            segment.Processes[processIndex] = new ProcessData
            {
                Coroutine = coroutine,
                WaitUntil = 0f,
                Handle = handle,
                Paused = false,
                Held = false
            };

            return handle;
        }

        private CoroutineHandle GetHandle()
        {
            if (_freeHandleIndex >= _handlePool.Length)
            {
                System.Array.Resize(ref _handlePool, _handlePool.Length * 2);
                for (int i = _freeHandleIndex; i < _handlePool.Length; i++)
                    _handlePool[i] = new CoroutineHandle { Id = i, Version = 0 };
            }

            CoroutineHandle handle = _handlePool[_freeHandleIndex];
            handle.Version = _currentVersion++;
            _handlePool[_freeHandleIndex++] = handle;
            return handle;
        }

        private void ReleaseHandle(CoroutineHandle handle)
        {
            if (handle.Id >= 0 && handle.Id < _handlePool.Length)
            {
                handle.Version++;
                _handlePool[handle.Id] = handle;
                _freeHandleIndex--;
            }
        }
    }
}