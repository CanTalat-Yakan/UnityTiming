using UnityEngine;
using System.Collections.Generic;

namespace UnityEssentials
{
    public partial class Timing : PersistentSingleton<Timing>
    {
        private const int ProcessArrayChunkSize = 64;
        private const int InitialBufferSizeLarge = 256;
        private const int InitialBufferSizeMedium = 64;
        private const int InitialBufferSizeSmall = 8;

        public static float LocalTime => Instance._localTime;
        public static float DeltaTime => Instance._deltaTime;
        public static CoroutineHandle CurrentCoroutine => Instance._currentCoroutine;
        public static event System.Action OnPreExecute;

        private readonly SegmentData[] _segments = new SegmentData[4];
        private readonly Dictionary<CoroutineHandle, ProcessIndex> _handleToIndex = new();
        private readonly Dictionary<ProcessIndex, CoroutineHandle> _indexToHandle = new();
        public static System.Func<IEnumerator<float>, CoroutineHandle, IEnumerator<float>> ReplacementFunction;
        public const float WaitForOneFrame = float.NegativeInfinity;

        private CoroutineHandle _currentCoroutine;
        private float _localTime;
        private float _deltaTime;
        private byte _instanceID;

        public struct CoroutineHandle
        {
            private readonly int _id;
            public bool IsValid => _id != 0;
            public CoroutineHandle(byte id) => _id = id;

            public static bool operator ==(CoroutineHandle a, CoroutineHandle b) => a._id == b._id;
            public static bool operator !=(CoroutineHandle a, CoroutineHandle b) => a._id != b._id;
            public override bool Equals(object obj) => obj is CoroutineHandle h && _id == h._id;
            public override int GetHashCode() => _id;
        }

        private struct ProcessIndex
        {
            public Segment Segment;
            public int Index;
        }

        private struct SegmentData
        {
            public IEnumerator<float>[] Processes;
            public bool[] Paused;
            public bool[] Held;
            public float[] WaitUntil;
            public int NextSlot;
            public int LastSlot;
            public float LastTime;
            public int InitialSize;

            public SegmentData(int size)
            {
                Processes = new IEnumerator<float>[size];
                Paused = new bool[size];
                Held = new bool[size];
                WaitUntil = new float[size];
                NextSlot = 0;
                LastSlot = 0;
                LastTime = 0f;
                InitialSize = size;
            }

            public void Expand()
            {
                System.Array.Resize(ref Processes, Processes.Length + ProcessArrayChunkSize);
                System.Array.Resize(ref Paused, Paused.Length + ProcessArrayChunkSize);
                System.Array.Resize(ref Held, Held.Length + ProcessArrayChunkSize);
                System.Array.Resize(ref WaitUntil, WaitUntil.Length + ProcessArrayChunkSize);
            }
        }

        public override void Awake()
        {
            base.Awake();
            InitializeSegments();
        }

        public void Update() => ProcessSegment(Segment.Update);
        public void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        public void LateUpdate() => ProcessSegment(Segment.LateUpdate);

        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, Segment segment = Segment.Update) =>
            Instance.RunCoroutineInternal(coroutine, segment);

        private void InitializeSegments()
        {
            _segments[(int)Segment.Update] = new(InitialBufferSizeLarge);
            _segments[(int)Segment.FixedUpdate] = new(InitialBufferSizeMedium);
            _segments[(int)Segment.LateUpdate] = new(InitialBufferSizeSmall);
            _segments[(int)Segment.SlowUpdate] = new(InitialBufferSizeMedium);
        }

        private void ProcessSegment(Segment segment)
        {
            OnPreExecute?.Invoke();

            ref var segmentData = ref _segments[(int)segment];
            if (segmentData.NextSlot == 0) return;

            UpdateTimeValues(segment);
            segmentData.LastSlot = segmentData.NextSlot;

            for (int i = 0; i < segmentData.LastSlot; i++)
            {
                ProcessCoroutine(segment, ref segmentData, i);
            }

            _currentCoroutine = default;
        }

        private void ProcessCoroutine(Segment segment, ref SegmentData data, int index)
        {
            try
            {
                if (data.Processes[index] == null || data.Paused[index] || data.Held[index]) return;
                if (data.WaitUntil[index] > data.LastTime) return;

                _currentCoroutine = _indexToHandle[new ProcessIndex { Segment = segment, Index = index }];

                if (!data.Processes[index].MoveNext())
                    KillCoroutines(_currentCoroutine);
                else
                    UpdateWaitTime(data.Processes[index].Current, ref data, index);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                if (e is MissingReferenceException)
                {
                    Debug.LogError("Consider using CancelWith(gameObject) when starting coroutine.");
                }
            }
        }

        private void UpdateWaitTime(float waitValue, ref SegmentData data, int index)
        {
            if (float.IsNaN(waitValue))
                HandleReplacement(ref data.Processes[index]);
            else if (waitValue == WaitForOneFrame)
                data.WaitUntil[index] = data.LastTime;
            else if (waitValue >= 0f)
                data.WaitUntil[index] = data.LastTime + waitValue;
            else
                data.WaitUntil[index] = data.LastTime;
        }

        private void UpdateTimeValues(Segment segment)
        {
            ref var data = ref _segments[(int)segment];
            data.LastTime = segment == Segment.FixedUpdate ? Time.fixedTime : Time.time;
            _deltaTime = segment == Segment.FixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;
            _localTime = data.LastTime;
        }

        private CoroutineHandle RunCoroutineInternal(IEnumerator<float> coroutine, Segment segment)
        {
            if (coroutine == null) return default;

            ref var data = ref _segments[(int)segment];
            if (data.NextSlot >= data.Processes.Length) data.Expand();

            var handle = new CoroutineHandle(_instanceID);
            var processIndex = new ProcessIndex { Segment = segment, Index = data.NextSlot };

            data.Processes[processIndex.Index] = coroutine;
            data.WaitUntil[processIndex.Index] = 0f;

            _handleToIndex[handle] = processIndex;
            _indexToHandle[processIndex] = handle;

            data.NextSlot++;
            return handle;
        }

        private void HandleReplacement(ref IEnumerator<float> coroutine)
        {
            if (ReplacementFunction == null) return;

            coroutine = ReplacementFunction(coroutine, _currentCoroutine);
            ReplacementFunction = null;
        }

        public static void KillCoroutines(CoroutineHandle handle)
        {
            if (!Instance._handleToIndex.TryGetValue(handle, out var index)) return;

            Instance._segments[(int)index.Segment].Processes[index.Index] = null;
            Instance._handleToIndex.Remove(handle);
            Instance._indexToHandle.Remove(index);
        }
    }

    public enum Segment { Update, FixedUpdate, LateUpdate, SlowUpdate }
}