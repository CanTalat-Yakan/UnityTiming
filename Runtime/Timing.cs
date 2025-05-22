using UnityEngine;
using System.Collections.Generic;

namespace UnityEssentials
{
    public enum Segment { Update, FixedUpdate, LateUpdate }

    public partial class Timing : PersistentSingleton<Timing>
    {
        public const float WaitForOneFrame = float.NegativeInfinity;

        public static float LocalTime { get; private set; }
        public static float DeltaTime { get; private set; }

        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, Segment segment = Segment.Update)
        {
            ref var processArray = ref Instance._processPool[(int)segment];

            ref var handle = ref Instance._handlePool.Get(out var handleIndex);
            handle.Version = Instance._currentVersion++;

            ref var processData = ref processArray.Get(out var processIndex);
            processData.Coroutine = coroutine;
            processData.Paused = false;
            processData.ArrayIndex = processIndex;
            processData.WaitUntil = LocalTime;
            processData.HandleIndex = handleIndex;
            processData.HandleVersion = handle.Version;

            return handle;
        }

        public static void PauseCoroutine(ushort handleVersion)
        {
            ref var processPool = ref Instance._processPool;
            for (int s = 0; s < processPool.Length; s++)
            {
                ref var processArray = ref processPool[s];
                for (int i = 0; i < processArray.Count; i++)
                    if (processArray.Elements[i].HandleVersion.Equals(handleVersion) &&
                        processArray.Elements[i].Coroutine != null)
                    {
                        processArray.Elements[i].Paused = true;
                        return;
                    }
            }
        }

        public static void ResumeCoroutine(ushort handleVersion)
        {
            ref var processPool = ref Instance._processPool;
            for (int s = 0; s < processPool.Length; s++)
            {
                ref var processArray = ref processPool[s];
                for (int i = 0; i < processArray.Count; i++)
                    if (processArray.Elements[i].HandleVersion.Equals(handleVersion) &&
                        processArray.Elements[i].Coroutine != null)
                    {
                        processArray.Elements[i].Paused = false;

                        // Adjust wait time if paused during waiting period
                        if (processArray.Elements[i].WaitUntil < LocalTime)
                            processArray.Elements[i].WaitUntil = LocalTime;
                        return;
                    }
            }
        }

        public static void KillCoroutine(ushort handleVersion)
        {
            ref var processPool = ref Instance._processPool;
            for (int s = 0; s < processPool.Length; s++)
            {
                ref var processArray = ref processPool[s];
                for (int i = 0; i < processArray.Count; i++)
                {
                    if (processArray.Elements[i].HandleVersion.Equals(handleVersion))
                    {
                        processArray.Elements[i].Coroutine = null;
                        Instance._handlePool.Return(processArray.Elements[i].HandleIndex);
                        return;
                    }
                }
            }
        }

        public static void KillCoroutine(ProcessData processData)
        {
            processData.Coroutine = null;
            Instance._handlePool.Return(processData.HandleIndex);
        }

        public static void KillAllCoroutines()
        {
            ref var processPool = ref Instance._processPool;
            for (int i = 0; i < processPool.Length; i++)
            {
                ref var processArray = ref processPool[i];
                for (int j = 0; j < processArray.Count; j++)
                {
                    ref var processData = ref processArray.Elements[j];
                    processData.Coroutine = null;
                    processArray.Return(processData.ArrayIndex);
                }
            }
        }

        public static bool IsCoroutineActive(CoroutineHandle handle)
        {
            ref var processPool = ref Instance._processPool;
            foreach (var processArray in processPool)
                foreach (var processData in processArray.Elements)
                    if (processData.HandleVersion.Equals(handle.Version))
                        return processData.Coroutine != null;
            return false;
        }
    }

    public partial class Timing : PersistentSingleton<Timing>
    {
        public struct CoroutineHandle
        {
            public ushort Version;
            public bool IsValid => Version > 0;
        }

        public struct ProcessData
        {
            public int ArrayIndex;
            public int HandleIndex;
            public ushort HandleVersion;

            public IEnumerator<float> Coroutine;
            public float WaitUntil;
            public bool Paused;
        }

        private ManagedArray<ProcessData>[] _processPool;
        private ManagedArray<CoroutineHandle> _handlePool;

        private ushort _currentVersion = 1;

        public override void OnDestroy()
        {
            KillAllCoroutines();
            base.OnDestroy();
        }

        public override void Awake()
        {
            base.Awake();

            _handlePool = new ManagedArray<CoroutineHandle>(256);

            _processPool = new ManagedArray<ProcessData>[3];
            for (int i = 0; i < _processPool.Length; i++)
                _processPool[i] = new ManagedArray<ProcessData>(256);
        }

        public void Update() => ProcessSegment(Segment.Update);
        public void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        public void LateUpdate() => ProcessSegment(Segment.LateUpdate);

        private void ProcessSegment(Segment segment)
        {
            DeltaTime = segment == Segment.FixedUpdate ? Time.fixedDeltaTime : Time.deltaTime;
            LocalTime = segment == Segment.FixedUpdate ? Time.fixedTime : Time.time;

            ref var processPool = ref _processPool[(int)segment];
            for (int i = 0; i < processPool.Count; i++)
            {
                ref var processData = ref processPool.Elements[i];

                if (processData.Coroutine == null || processData.Paused)
                    continue;

                if (LocalTime < processData.WaitUntil)
                    continue;

                try
                {
                    if (processData.Coroutine.MoveNext())
                    {
                        float current = processData.Coroutine.Current;
                        processData.WaitUntil = float.IsNaN(current) ? 0f : LocalTime + current;
                    }
                    else KillCoroutine(processData);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                    KillCoroutine(processData);
                }
            }
        }
    }
}