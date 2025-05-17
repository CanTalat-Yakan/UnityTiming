# Unity Essentials

**Unity Essentials** is a lightweight, modular utility namespace designed to streamline development in Unity. 
It provides a collection of foundational tools, extensions, and helpers to enhance productivity and maintain clean code architecture.

## 📦 This Package

This package is part of the **Unity Essentials** ecosystem.  
It integrates seamlessly with other Unity Essentials modules and follows the same lightweight, dependency-free philosophy.

## 🌐 Namespace

All utilities are under the `UnityEssentials` namespace. This keeps your project clean, consistent, and conflict-free.

```csharp
using UnityEssentials;
```

# Timing
Robust coroutine management system with segment-based execution, precise timing control, and zero allocation performance.


## Features
- Zero GC allocations during runtime
- Frame-perfect timing control
- Segment-based execution (Update, FixedUpdate, LateUpdate, SlowUpdate)
- Pause/Resume functionality
- Coroutine replacement system
- Instance-based lifecycle management

## Usage Examples

Each snippet below demonstrates a distinct usage scenario:


1. Basic Coroutine Execution
Frame-by-frame execution with precise timing control:
```csharp
public class BasicCoroutine : MonoBehaviour
{
    void Start() =>
        Timing.RunCoroutine(MyRoutine());

    IEnumerator<float> MyRoutine()
    {
        Debug.Log("Start");
        yield return 1f;
        Debug.Log("1 second passed");
        yield return Timing.WaitForOneFrame;
        Debug.Log("1 frame passed");
    }
```

2. FixedUpdate Physics Coroutine
```csharp
public class PhysicsCoroutine : MonoBehaviour
{
    void Start() =>
        Timing.RunCoroutine(PhysicsRoutine(), Segment.FixedUpdate);

    IEnumerator<float> PhysicsRoutine()
    {
        while (true)
        {
            Debug.Log("FixedUpdate @ 50Hz");
            yield return 0.02f;
        }
    }
}
```


3. LateUpdate Camera Logic
```csharp
public class CameraController : MonoBehaviour
{
    void Start() =>
        Timing.RunCoroutine(PostFrameLogic(), Segment.LateUpdate);

    IEnumerator<float> PostFrameLogic()
    {
        while (true)
        {
            Debug.Log("Camera update after main logic");
            yield return Timing.WaitForOneFrame;
        }
    }
}
```


4. Complex Event Sequencing
```csharp
public class SlowOperations : MonoBehaviour
{
    void Start() =>
        Timing.RunCoroutine(InfrequentChecks(), Segment.SlowUpdate);

    IEnumerator<float> InfrequentChecks()
    {
        while (true)
        {
            Debug.Log("Non-critical background check");
            yield return 2.0f;
        }
    }
}
```


5. Coroutine Lifecycle Management
```csharp
public class LifecycleExample : MonoBehaviour
{
    CoroutineHandle handle;

    void Start()
    {
        handle = Timing.RunCoroutine(TimedRoutine());
        Invoke(nameof(StopEarly), 3f);
    }

    IEnumerator<float> TimedRoutine()
    {
        while (true)
        {
            Debug.Log("Persistent operation");
            yield return 0.5f;
        }
    }

    void StopEarly()
    {
        Timing.KillCoroutine(handle);
        Debug.Log("Coroutine stopped prematurely");
    }
}
```


6. Coroutine Replacement System
```csharp
public class FaultRecovery : MonoBehaviour
{
    void Start()
    {
        Timing.ReplacementFunction = RecoveryHandler;
        Timing.RunCoroutine(PotentiallyFaulty());
    }

    IEnumerator<float> PotentiallyFaulty()
    {
        yield return float.NaN; // Trigger replacement
    }

    IEnumerator<float> RecoveryHandler(IEnumerator<float> original, CoroutineHandle handle)
    {
        Debug.Log("Recovering from fault");
        yield return 1f;
        Debug.Log("Recovery complete");
    }
}
```


7. Pre-Execution Hook
```csharp
public class ExecutionMonitor : MonoBehaviour
{
    void OnEnable() => Timing.OnPreExecute += LogPreUpdate;
    void OnDisable() => Timing.OnPreExecute -= LogPreUpdate;

    void LogPreUpdate()
    {
        Debug.Log($"Segment about to execute at {Timing.LocalTime}");
    }
}
```


8. Time Data Access
```csharp
public class TimeDisplay : MonoBehaviour
{
    void Update()
    {
        Debug.Log($"Frame Time: {Timing.LocalTime:0.00}, Delta: {Timing.DeltaTime:0.000}");
    }
}
```


9. Current Coroutine Tracking
```csharp
public class ContextAwareCoroutine : MonoBehaviour
{
    void Start()
    {
        Timing.RunCoroutine(HandleAwareRoutine());
    }

    IEnumerator<float> HandleAwareRoutine()
    {
        Debug.Log($"Running under handle: {Timing.CurrentCoroutine}");
        yield return 1f;
        Debug.Log($"Still same handle: {Timing.CurrentCoroutine}");
    }
}
```

##Key Features
-yield return 0 - Execute next frame (Update segment)
-yield return Timing.WaitUntil(() => condition) - Pause until condition met
-while(condition) { yield return 0; } - Frame-perfect conditional looping
-Cross-segment coordination
-Precise timeout handling
-Efficient resource polling strategies

##Best Practices
-Use yield return 0 for frame-by-frame logic
-Pre-calculate wait durations outside loops
-Use KillCoroutine instead of null checks
-Leverage segments for proper execution order
-Use WaitUntil for event-driven logic