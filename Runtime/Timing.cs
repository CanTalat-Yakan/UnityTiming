using UnityEngine;
using System.Collections.Generic;

namespace UnityEssentials
{
    public enum Segment { Update, FixedUpdate, LateUpdate, SlowUpdate }

    public partial class Timing : PersistentSingleton<Timing>
    {
        public const float WaitForOneFrame = float.NegativeInfinity;

        public static float LocalTime { get; private set; }
        public static float DeltaTime { get; private set; }

        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, Segment segment = Segment.Update) =>
            Instance.RunCoroutineInternal(coroutine, (int)segment);

        public static void PauseCoroutine(CoroutineHandle handle)
        {
            ref var segments = ref Instance._segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref var segment = ref segments[s];
                for (int i = 0; i < segment.Count; i++)
                    if (segment.Items[i].Handle.Equals(handle) &&
                        segment.Items[i].Coroutine != null)
                    {
                        segment.Items[i].Paused = true;
                        return;
                    }
            }
        }

        public static void ResumeCoroutine(CoroutineHandle handle)
        {
            ref var segments = ref Instance._segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref var segment = ref segments[s];
                for (int i = 0; i < segment.Count; i++)
                    if (segment.Items[i].Handle.Equals(handle) &&
                        segment.Items[i].Coroutine != null)
                    {
                        segment.Items[i].Paused = false;

                        // Adjust wait time if paused during waiting period
                        if (segment.Items[i].WaitUntil < LocalTime)
                            segment.Items[i].WaitUntil = LocalTime;
                        return;
                    }
            }
        }

        public static void KillCoroutine(CoroutineHandle handle)
        {
            ref var segments = ref Instance._segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref var segment = ref segments[s];
                for (int i = 0; i < segment.Count; i++)
                {
                    if (segment.Items[i].Handle.Equals(handle))
                    {
                        segment.Items[i].Coroutine = null;
                        segment.Items[i].WaitUntil = segment.FreeHead;
                        segment.FreeHead = i;
                        Instance.ReleaseHandle(handle);
                        return;
                    }
                }
            }
        }

        public static void KillAllCoroutines()
        {
            ref var segments = ref Instance._segments;
            for (int i = segments.Length - 1; i >= 0; i--)
            {
                var segment = segments[i];
                for (int j = 0; j < segment.Items.Length; j++)
                    segment.Items[j].Coroutine = null;

                segments[i].Count = 0;
                segments[i].FreeHead = -1;
            }
        }

        public static bool IsCoroutineActive(CoroutineHandle handle)
        {
            ref var segments = ref Instance._segments;
            foreach (var segment in segments)
                foreach (var process in segment.Items)
                    if (process.Handle.Equals(handle))
                        return process.Coroutine != null;
            return false;
        }
    }

    public partial class Timing : PersistentSingleton<Timing>
    {
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

        private ManagedArray<ProcessData>[] _segments;
        private ObjectPool<CoroutineHandle> _handlePool;
        private ushort _currentVersion = 1;

        protected override void OnDestroy()
        {
            KillAllCoroutines();
            base.OnDestroy();
        }

        protected override void Awake()
        {
            base.Awake();

            const int initialBufferSize = 256;

            _handlePool = new ObjectPool<CoroutineHandle>(
                initialBufferSize,
                i => new CoroutineHandle { Id = i, Version = 0 });

            _segments = new ManagedArray<ProcessData>[4];
            for (int i = 0; i < _segments.Length; i++)
                _segments[i] = new ManagedArray<ProcessData>(initialBufferSize);
        }

        public void Update() => ProcessSegment(Segment.Update);
        public void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        public void LateUpdate() => ProcessSegment(Segment.LateUpdate);

        private void ProcessSegment(Segment segment)
        {
            int segmentIndex = (int)segment;
            ref var segmentData = ref _segments[segmentIndex];
            UpdateTimeValues(segment);
            for (int i = 0; i < segmentData.Count; i++)
            {
                if (segmentData.Items[i].Coroutine == null) continue;
                if (segmentData.Items[i].Paused || segmentData.Items[i].Held) continue;
                if (LocalTime < segmentData.Items[i].WaitUntil) continue;

                try
                {
                    if (segmentData.Items[i].Coroutine.MoveNext())
                    {
                        float current = segmentData.Items[i].Coroutine.Current;
                        segmentData.Items[i].WaitUntil = float.IsNaN(current) ? 0f : LocalTime + current;
                    }
                    else KillCoroutine(segmentData.Items[i].Handle);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    KillCoroutine(segmentData.Items[i].Handle);
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
            ref var segment = ref _segments[segmentIndex];
            int processIndex = segment.Add(new ProcessData
            {
                Coroutine = coroutine,
                Handle = GetHandle()
            });

            return segment.Items[processIndex].Handle;
        }

        private CoroutineHandle GetHandle()
        {
            CoroutineHandle handle = _handlePool.Get();
            handle.Version = _currentVersion++;
            return handle;
        }

        private void ReleaseHandle(CoroutineHandle handle) =>
            _handlePool.Release(ref handle, handle.Id);
    }
}