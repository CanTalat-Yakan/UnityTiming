using UnityEngine;
using System.Collections.Generic;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace UnityEssentials
{
    public partial class Timing : PersistentSingleton<Timing>
    {
        public float TimeBetweenSlowUpdateCalls = 1f / 7f;
        public DebugInfoType ProfilerDebugAmount;
        public int UpdateCoroutines, FixedUpdateCoroutines, LateUpdateCoroutines, SlowUpdateCoroutines;
        public float localTime, deltaTime;

        public static float LocalTime => Instance.localTime;
        public static float DeltaTime => Instance.deltaTime;
        public static CoroutineHandle CurrentCoroutine => GetCurrentCoroutine();
        public CoroutineHandle currentCoroutine { get; private set; }

        private const int ProcessArrayChunkSize = 64;
        private const int InitialBufferSizeLarge = 256, InitialBufferSizeMedium = 64, InitialBufferSizeSmall = 8;
        private const float ASmallNumber = 0.00048828125f;

        private SegmentData[] _segments = new SegmentData[4];
        private Dictionary<CoroutineHandle, ProcessIndex> _handleToIndex = new();
        private Dictionary<ProcessIndex, CoroutineHandle> _indexToHandle = new();
        private Dictionary<CoroutineHandle, HashSet<CoroutineHandle>> _waitingTriggers = new();
        private HashSet<CoroutineHandle> _allWaiting = new();
        private byte _instanceID;

        private struct SegmentData
        {
            public IEnumerator<float>[] Processes;
            public bool[] Paused, Held;
            public float[] WaitUntil; // Track when to resume each coroutine
            public int NextSlot, LastSlot;
            public float LastTime;
            public int CurrentFrame;
            public int InitialSize;

            public SegmentData(int size)
            {
                Processes = new IEnumerator<float>[size];
                Paused = new bool[size];
                Held = new bool[size];
                WaitUntil = new float[size];
                NextSlot = LastSlot = 0;
                LastTime = 0f;
                CurrentFrame = 0;
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
            _segments[(int)Segment.Update] = new(InitialBufferSizeLarge);
            _segments[(int)Segment.FixedUpdate] = new(InitialBufferSizeMedium);
            _segments[(int)Segment.LateUpdate] = new(InitialBufferSizeSmall);
            _segments[(int)Segment.SlowUpdate] = new(InitialBufferSizeMedium);
        }

        public void Update() => ProcessSegment(Segment.Update);
        public void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        public void LateUpdate() => ProcessSegment(Segment.LateUpdate);
        public void OnDestroy() { if (_instance == this) _instance = null; }

        private void ProcessSegment(Segment segment)
        {
            if (OnPreExecute != null)
                OnPreExecute();

            var index = (int)segment;
            ref var data = ref _segments[index];
            if (data.NextSlot == 0) return;

            UpdateTimeValues(segment);
            data.LastSlot = data.NextSlot;

            for (int i = 0; i < data.LastSlot; i++)
                try
                {
                    if (data.Processes[i] == null || data.Paused[i] || data.Held[i]) continue;
                    if (data.WaitUntil[i] > data.LastTime) continue; // Wait if not ready

                    currentCoroutine = _indexToHandle[new ProcessIndex { Segment = segment, index = i }];
                    if (!data.Processes[i].MoveNext())
                        KillCoroutines(currentCoroutine);
                    else
                    {
                        float current = data.Processes[i].Current;
                        if (float.IsNaN(current))
                            HandleReplacement(ref data.Processes[i]);
                        else if (current == WaitForOneFrame)
                            data.WaitUntil[i] = data.LastTime; // Next frame
                        else if (current >= 0f)
                            data.WaitUntil[i] = data.LastTime + current; // Wait for seconds
                        else
                            data.WaitUntil[i] = data.LastTime; // Invalid value, next frame
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    if (e is MissingReferenceException)
                        Debug.LogError("Consider using CancelWith(gameObject) when starting coroutine.");
                }

            currentCoroutine = default;
        }

        private void HandleReplacement(ref IEnumerator<float> coroutine)
        {
            if (ReplacementFunction == null) return;
            coroutine = ReplacementFunction(coroutine, currentCoroutine);
            ReplacementFunction = null;
        }

        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, Segment segment = Segment.Update)
        {
            return Instance.RunCoroutineInternal(coroutine, segment);
        }

        private CoroutineHandle RunCoroutineInternal(IEnumerator<float> coroutine, Segment segment)
        {
            if (coroutine == null) return default;

            ref var data = ref _segments[(int)segment];
            if (data.NextSlot >= data.Processes.Length) data.Expand();

            var handle = new CoroutineHandle(_instanceID);
            var index = new ProcessIndex { Segment = segment, index = data.NextSlot };

            data.Processes[index.index] = coroutine;
            data.WaitUntil[index.index] = 0f; // Start immediately
            _handleToIndex[handle] = index;
            _indexToHandle[index] = handle;

            data.NextSlot++;
            return handle;
        }

        public static void KillCoroutines(CoroutineHandle handle)
        {
            if (!Instance._handleToIndex.TryGetValue(handle, out var index)) return;

            Instance._segments[(int)index.Segment].Processes[index.index] = null;
            Instance._handleToIndex.Remove(handle);
            Instance._indexToHandle.Remove(index);
        }

        private static CoroutineHandle GetCurrentCoroutine()
        {
            foreach (var segment in Instance._segments)
                for (int i = 0; i < segment.LastSlot; i++)
                    if (segment.Processes[i] != null && Instance._indexToHandle.TryGetValue(
                        new ProcessIndex { Segment = Segment.Update, index = i }, out var handle))
                        return handle;
            return default;
        }

        private bool UpdateTimeValues(Segment segment)
        {
            ref var data = ref _segments[(int)segment];
            data.LastTime = segment == Segment.FixedUpdate ? Time.fixedTime : Time.time;
            deltaTime = segment == Segment.FixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;
            localTime = data.LastTime;
            return true;
        }

        public static event System.Action OnPreExecute;
        public static System.Func<IEnumerator<float>, CoroutineHandle, IEnumerator<float>> ReplacementFunction;
        public const float WaitForOneFrame = float.NegativeInfinity;
    }

    public enum Segment { Update, FixedUpdate, LateUpdate, SlowUpdate }
    public enum DebugInfoType { None, SeperateCoroutines, SeperateTags }

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

    public struct ProcessIndex
    {
        public Segment Segment;
        public int index;
    }
}