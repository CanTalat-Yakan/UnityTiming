using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Assertions;
#if UNITY_5_5_OR_NEWER
using UnityEngine.Profiling;
#endif

namespace UnityEssentials
{
    public class Timing : PersistentSingleton<Timing>
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

        private SegmentData[] segments = new SegmentData[4];
        private Dictionary<CoroutineHandle, ProcessIndex> handleToIndex = new Dictionary<CoroutineHandle, ProcessIndex>();
        private Dictionary<ProcessIndex, CoroutineHandle> indexToHandle = new Dictionary<ProcessIndex, CoroutineHandle>();
        private Dictionary<CoroutineHandle, HashSet<CoroutineHandle>> waitingTriggers = new Dictionary<CoroutineHandle, HashSet<CoroutineHandle>>();
        private HashSet<CoroutineHandle> allWaiting = new HashSet<CoroutineHandle>();
        private byte instanceID;

        private struct SegmentData
        {
            public IEnumerator<float>[] processes;
            public bool[] paused, held;
            public int nextSlot, lastSlot;
            public float lastTime;
            public int currentFrame;
            public int initialSize;

            public SegmentData(int size)
            {
                processes = new IEnumerator<float>[size];
                paused = new bool[size];
                held = new bool[size];
                nextSlot = lastSlot = 0;
                lastTime = 0f;
                currentFrame = 0;
                initialSize = size;
            }

            public void Expand()
            {
                System.Array.Resize(ref processes, processes.Length + ProcessArrayChunkSize);
                System.Array.Resize(ref paused, paused.Length + ProcessArrayChunkSize);
                System.Array.Resize(ref held, held.Length + ProcessArrayChunkSize);
            }
        }

        private void Awake()
        {
            segments[(int)Segment.Update] = new SegmentData(InitialBufferSizeLarge);
            segments[(int)Segment.FixedUpdate] = new SegmentData(InitialBufferSizeMedium);
            segments[(int)Segment.LateUpdate] = new SegmentData(InitialBufferSizeSmall);
            segments[(int)Segment.SlowUpdate] = new SegmentData(InitialBufferSizeMedium);
        }

        private void Update() => ProcessSegment(Segment.Update);
        private void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        private void LateUpdate() => ProcessSegment(Segment.LateUpdate);
        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void ProcessSegment(Segment segment)
        {
            if (OnPreExecute != null) OnPreExecute();

            var index = (int)segment;
            ref var data = ref segments[index];
            if (data.nextSlot == 0) return;

            UpdateTimeValues(segment);
            data.lastSlot = data.nextSlot;

            for (int i = 0; i < data.lastSlot; i++)
            {
                try
                {
                    if (data.processes[i] == null || data.paused[i] || data.held[i]) continue;

                    currentCoroutine = indexToHandle[new ProcessIndex { seg = segment, i = i }];
                    if (!data.processes[i].MoveNext()) KillCoroutines(currentCoroutine);
                    else if (float.IsNaN(data.processes[i].Current)) HandleReplacement(ref data.processes[i]);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    if (ex is MissingReferenceException)
                        Debug.LogError("Consider using CancelWith(gameObject) when starting coroutine.");
                }
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

            ref var data = ref segments[(int)segment];
            if (data.nextSlot >= data.processes.Length) data.Expand();

            var handle = new CoroutineHandle(instanceID);
            var index = new ProcessIndex { seg = segment, i = data.nextSlot++ };

            data.processes[index.i] = coroutine;
            handleToIndex[handle] = index;
            indexToHandle[index] = handle;

            return handle;
        }

        public static void KillCoroutines(CoroutineHandle handle)
        {
            if (!Instance.handleToIndex.TryGetValue(handle, out var index)) return;

            Instance.segments[(int)index.seg].processes[index.i] = null;
            Instance.handleToIndex.Remove(handle);
            Instance.indexToHandle.Remove(index);
        }

        private static CoroutineHandle GetCurrentCoroutine()
        {
            foreach (var segment in Instance.segments)
                for (int i = 0; i < segment.lastSlot; i++)
                    if (segment.processes[i] != null && Instance.indexToHandle.TryGetValue(
                        new ProcessIndex { seg = Segment.Update, i = i }, out var handle))
                        return handle;
            return default;
        }

        private bool UpdateTimeValues(Segment segment)
        {
            ref var data = ref segments[(int)segment];
            data.lastTime = segment == Segment.FixedUpdate ? Time.fixedTime : Time.time;
            deltaTime = segment == Segment.FixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;
            localTime = data.lastTime;
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
        public Segment seg;
        public int i;
    }
}