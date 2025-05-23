using UnityEngine;
using System.Collections.Generic;
using System;

namespace UnityEssentials
{
    /// <summary>
    /// Represents the different segments of the Unity game loop where an operation can be executed.
    /// </summary>
    /// <remarks>This enumeration is typically used to specify the timing of operations within the Unity game
    /// loop. The available segments correspond to the main update phases: <see cref="Update"/>, <see
    /// cref="FixedUpdate"/>, and <see cref="LateUpdate"/>.</remarks>
    public enum Segment { Update, FixedUpdate, LateUpdate, LazyUpdate }

    /// <summary>
    /// Represents a handle to a coroutine, used to track and manage its execution state.
    /// </summary>
    /// <remarks>A <see cref="CoroutineHandle"/> is considered valid if its <see cref="Version"/> is greater
    /// than 0.  This handle can be used to identify and interact with a specific coroutine instance.</remarks>
    public struct CoroutineHandle
    {
        public ushort Version;
        public bool IsValid => Version > 0;
    }

    /// <summary>
    /// Represents the state and metadata associated with a process, including its indices, version, and execution
    /// state.
    /// </summary>
    /// <remarks>This structure is used to encapsulate information about a process, such as its array and
    /// handle indices,  versioning for handle validation, and execution state including coroutine management and pause
    /// status.</remarks>
    public struct ProcessData
    {
        public int ArrayIndex;
        public int HandleIndex;
        public ushort HandleVersion;

        public IEnumerator<float> Coroutine;
        public float WaitUntil;
        public bool Paused;
    }

    /// <summary>
    /// Provides functionality for managing and controlling coroutines, including starting, pausing, resuming,  and
    /// terminating coroutines. This class is a singleton and ensures centralized management of coroutine processes.
    /// </summary>
    /// <remarks>The <see cref="Timing"/> class allows for fine-grained control over coroutine execution,
    /// including the ability  to pause, resume, and kill individual coroutines or all coroutines at once. Coroutines
    /// are organized into  segments, and their execution is tied to the local time managed by this class.  Use <see
    /// cref="RunCoroutine"/> to start a coroutine, and manage its lifecycle using methods such as  <see
    /// cref="PauseCoroutine"/>, <see cref="ResumeCoroutine"/>, and <see cref="KillCoroutine"/>.  The <see
    /// cref="IsCoroutineActive"/> method can be used to check the status of a coroutine.</remarks>
    public partial class Timing : PersistentSingleton<Timing>
    {
        public const float WaitForOneFrame = float.NegativeInfinity;

        public static float LocalTime { get; private set; }
        public static float DeltaTime { get; private set; }

        /// <summary>
        /// Starts a coroutine and returns a handle that can be used to manage its execution.
        /// </summary>
        /// <remarks>The coroutine will begin execution immediately in the specified segment. Use the
        /// returned <see cref="CoroutineHandle"/> to pause, resume, or stop the coroutine as needed.</remarks>
        /// <param name="coroutine">An enumerator that yields progress values to control the coroutine's execution.</param>
        /// <param name="segment">The timing segment in which the coroutine will run. Defaults to <see cref="Segment.Update"/>.</param>
        /// <returns>A <see cref="CoroutineHandle"/> that can be used to track or control the coroutine's execution.</returns>
        public static CoroutineHandle RunCoroutine(IEnumerator<float> coroutine, Segment segment = Segment.Update)
        {
            var processArray = Instance.ProcessPool[(int)segment];

            var handle = Instance.HandlePool.Get(out var handleIndex);
            handle.Version = Instance._handleIncrement++;

            ref var processData = ref processArray.Get(out var processIndex);
            processData.Coroutine = coroutine;
            processData.Paused = false;
            processData.ArrayIndex = processIndex;
            processData.WaitUntil = LocalTime;
            processData.HandleIndex = handleIndex;
            processData.HandleVersion = handle.Version;

            return handle;
        }

        /// <summary>
        /// Determines whether a coroutine associated with the specified handle is currently active.
        /// </summary>
        /// <remarks>A coroutine is considered active if it exists and is currently being managed by the
        /// system.</remarks>
        /// <param name="handle">The handle representing the coroutine to check.</param>
        /// <returns><see langword="true"/> if the coroutine associated with the specified handle is active; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool IsCoroutineActive(ushort handleVersion)
        {
            var processPool = Instance.ProcessPool;
            foreach (var processArray in processPool)
                foreach (var processData in processArray.Elements)
                    if (processData.HandleVersion.Equals(handleVersion))
                        return processData.Coroutine != null;
            return false;
        }

        /// <summary>
        /// Pauses the execution of a coroutine identified by the specified handle version.
        /// </summary>
        /// <remarks>If a coroutine matching the specified handle version is found, its execution is
        /// paused. If no matching coroutine is found, the method performs no action.</remarks>
        /// <param name="handleVersion">The version of the handle associated with the coroutine to be paused.</param>
        public static void PauseCoroutine(ushort handleVersion)
        {
            var processPool = Instance.ProcessPool;
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

        /// <summary>
        /// Resumes a paused coroutine identified by the specified handle version.
        /// </summary>
        /// <remarks>If the coroutine is found and was paused, it will be resumed. If the coroutine was
        /// paused during a waiting period, its wait time will be adjusted to ensure it resumes immediately if the wait
        /// period has already elapsed.</remarks>
        /// <param name="handleVersion">The version of the handle associated with the coroutine to be resumed.</param>
        public static void ResumeCoroutine(ushort handleVersion)
        {
            var processPool = Instance.ProcessPool;
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

        /// <summary>
        /// Terminates a coroutine identified by the specified handle version.
        /// </summary>
        /// <remarks>This method searches through the internal process pool to locate and terminate the
        /// coroutine that matches the provided handle version. If no matching coroutine is found, the method completes
        /// without performing any action.</remarks>
        /// <param name="handleVersion">The version of the handle associated with the coroutine to terminate.</param>
        public static void KillCoroutine(ushort handleVersion)
        {
            var processPool = Instance.ProcessPool;
            for (int s = 0; s < processPool.Length; s++)
            {
                ref var processArray = ref processPool[s];
                for (int i = 0; i < processArray.Count; i++)
                    if (processArray.Elements[i].HandleVersion.Equals(handleVersion))
                    {
                        KillCoroutine(ref processArray.Elements[i], processArray);
                        return;
                    }
            }
        }

        /// <summary>
        /// Stops all coroutines across all segments.
        /// </summary>
        /// <remarks>This method iterates through all segments and terminates any active coroutines within
        /// them. It is typically used to ensure that no coroutines are running, such as during cleanup or reset
        /// operations.</remarks>
        public static void KillAllCoroutines()
        {
            var processPoolLength = Instance.ProcessPool.Length;
            for (int i = 0; i < processPoolLength; i++)
                KillAllCoroutines(i);
        }

        /// <summary>
        /// Terminates all coroutines associated with the specified segment index.
        /// </summary>
        /// <remarks>This method iterates through all coroutines in the specified segment and terminates
        /// them. Ensure that the provided <paramref name="segmentIndex"/> corresponds to a valid segment to avoid
        /// unexpected behavior.</remarks>
        /// <param name="segmentIndex">The index of the segment whose coroutines should be terminated. Must be a valid index within the process
        /// pool.</param>
        public static void KillAllCoroutines(int segmentIndex)
        {
            var processArray = Instance.ProcessPool[segmentIndex];
            for (int i = 0; i < processArray.Count; i++)
                KillCoroutine(ref processArray.Elements[i], processArray);
        }

        /// <summary>
        /// Terminates the coroutine associated with the specified process data and releases its resources.
        /// </summary>
        /// <remarks>This method sets the coroutine reference in the provided <paramref
        /// name="processData"/> to <see langword="null"/>  and resets its handle version. It also releases the
        /// associated handle back to the internal handle pool.</remarks>
        /// <param name="processData">The process data containing the coroutine to terminate. Cannot be null.</param>
        public static void KillCoroutine(ref ProcessData processData, ManagedArray<ProcessData> processArray)
        {
            processData.Coroutine = null;
            processData.HandleVersion = 0;

            Instance.HandlePool.Elements[processData.HandleIndex].Version = 0;
            Instance.HandlePool.Return(processData.HandleIndex);

            processArray.Return(processData.ArrayIndex);
        }
    }

    /// <summary>
    /// Provides timing and coroutine management functionality, including processing update segments and managing
    /// coroutine lifecycles. This class is a singleton and extends <see cref="PersistentSingleton{T}"/>.
    /// </summary>
    /// <remarks>The <see cref="Timing"/> class is responsible for managing coroutines and processing
    /// different update segments (Update, FixedUpdate, and LateUpdate). It maintains internal pools for coroutine
    /// handles and process data, and ensures proper cleanup of resources when destroyed.  This class should be used to
    /// schedule and manage coroutines in a structured and efficient manner.</remarks>
    public partial class Timing : PersistentSingleton<Timing>
    {
        public ManagedArray<ProcessData>[] ProcessPool { get; private set; }
        public ManagedArray<CoroutineHandle> HandlePool { get; private set; }

        private ushort _handleIncrement = 1;
        private ushort _processIncrement = 0;

        /// <summary>
        /// Called when the object is being destroyed.
        /// </summary>
        /// <remarks>This method ensures that all coroutines started by the object are stopped before
        /// destruction. It also invokes the base class's <see cref="OnDestroy"/> method to perform any additional
        /// cleanup.</remarks>
        public override void OnDestroy()
        {
            KillAllCoroutines();
            base.OnDestroy();
        }

        /// <summary>
        /// Initializes the necessary resources and pools for managing coroutine handles and process data.
        /// </summary>
        /// <remarks>This method is called during the object's initialization phase and sets up internal
        /// data structures  required for handling coroutine processes. It overrides the base implementation to perform
        /// additional  setup specific to this class.</remarks>
        public override void Awake()
        {
            base.Awake();

            HandlePool = new ManagedArray<CoroutineHandle>();
            ProcessPool = new ManagedArray<ProcessData>[4];
            for (int i = 0; i < ProcessPool.Length; i++)
                ProcessPool[i] = new ManagedArray<ProcessData>();
        }

        public void Update() => ProcessSegment(Segment.Update);
        public void FixedUpdate() => ProcessSegment(Segment.FixedUpdate);
        public void LateUpdate() => ProcessSegment(Segment.LateUpdate);

        /// <summary>
        /// Processes the specified segment by performing the necessary updates and iterations.
        /// </summary>
        /// <remarks>If the specified segment is <see cref="Segment.Update"/>, a lazy segment processing
        /// operation is performed before proceeding with the update and iteration steps. The method updates the time
        /// associated with the segment and iterates over the corresponding process data in the process pool.</remarks>
        private void ProcessSegment(Segment segment)
        {
            if (segment == Segment.Update)
                ProcessLazySegment();

            UpdateTime(segment);
            var processArray = ProcessPool[(int)segment];
            for (int i = 0; i < processArray.Count; i++)
                IterateProcessData(ref processArray.Elements[i], processArray);
        }

        /// <summary>
        /// Processes a batch of tasks in the lazy update segment.
        /// </summary>
        /// <remarks>This method processes a fixed number of tasks (100) from the lazy update segment per
        /// frame.  If all tasks in the segment are processed before reaching the batch limit, the processing  index is
        /// reset to the beginning of the segment for the next frame.</remarks>
        private void ProcessLazySegment()
        {
            const int LazyUpdateBatchSize = 100;

            UpdateTime(Segment.LazyUpdate);
            var processArray = ProcessPool[(int)Segment.LazyUpdate];
            for (int i = 0; i < LazyUpdateBatchSize; i++)
                if (_processIncrement >= processArray.Count)
                {
                    _processIncrement = 0;
                    return;
                }
                else IterateProcessData(ref processArray.Elements[_processIncrement++], processArray);
        }

        /// <summary>
        /// Iterates and processes the specified <see cref="ProcessData"/> instance, advancing its coroutine execution.
        /// </summary>
        /// <remarks>This method advances the coroutine execution of the provided <paramref
        /// name="processData"/> instance  while the local time is greater than or equal to the instance's wait
        /// condition. If the coroutine cannot  proceed or the instance is paused, the method exits early.</remarks>
        private static void IterateProcessData(ref ProcessData processData, ManagedArray<ProcessData> processArray)
        {
            if (processData.Coroutine == null || processData.Paused)
                return;

            while (LocalTime >= processData.WaitUntil)
                if (!StepCoroutine(ref processData, processArray))
                    break;
        }

        /// <summary>
        /// Advances the execution of a coroutine by one step and updates its state.
        /// </summary>
        /// <remarks>This method attempts to move the coroutine to its next state by invoking <see
        /// cref="IEnumerator.MoveNext"/>.  If the coroutine yields a value, it updates the <c>WaitUntil</c> property of
        /// <paramref name="processData"/>  based on the yielded value. If the coroutine completes or an exception
        /// occurs, the coroutine is terminated  and removed from the collection.</remarks>
        private static bool StepCoroutine(ref ProcessData processData, ManagedArray<ProcessData> processArray)
        {
            try
            {
                if (processData.Coroutine.MoveNext())
                {
                    float current = processData.Coroutine.Current;
                    processData.WaitUntil = float.IsNaN(current) ? 0f : LocalTime + current;
                    return true;
                }
                else
                {
                    KillCoroutine(ref processData, processArray);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                KillCoroutine(ref processData, processArray);
                return false;
            }
        }

        private void UpdateTime(Segment segment)
        {
            if (segment == Segment.FixedUpdate)
            {
                DeltaTime = Time.fixedDeltaTime;
                LocalTime = Time.fixedTime;
            }
            else
            {
                DeltaTime = Time.deltaTime;
                LocalTime = Time.time;
            }
        }
    }
}