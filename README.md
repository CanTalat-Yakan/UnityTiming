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

## Use Case Examples for Timing Script

Each snippet below demonstrates a distinct usage scenario:


---
1. Basic Coroutine Execution in Update Segment
```csharp
using UnityEngine;
using System.Collections;

public class BasicUpdateCoroutine : MonoBehaviour
{
    void Start()
    {
        Timing.RunCoroutine(MyCoroutine());
    }

    IEnumerator<float> MyCoroutine()
    {
        Debug.Log("Start");
        yield return 1f;
        Debug.Log("1 second passed");
        yield return Timing.WaitForOneFrame;
        Debug.Log("1 frame passed");
    }
}
```

---
2. FixedUpdate Coroutine (e.g., physics or time-critical logic)
```csharp
public class PhysicsCoroutine : MonoBehaviour
{
    void Start()
    {
        Timing.RunCoroutine(FixedRoutine(), Segment.FixedUpdate);
    }

    IEnumerator<float> FixedRoutine()
    {
        while (true)
        {
            Debug.Log("FixedUpdate tick");
            yield return 0.02f; // 50Hz
        }
    }
}

```

---
3. LateUpdate Coroutine (e.g., camera logic or post-frame logic)

```csharp
public class PostFrameRoutine : MonoBehaviour
{
    void Start()
    {
        Timing.RunCoroutine(CameraRoutine(), Segment.LateUpdate);
    }

    IEnumerator<float> CameraRoutine()
    {
        while (true)
        {
            Debug.Log("After everything else");
            yield return Timing.WaitForOneFrame;
        }
    }
}

```

---

4. SlowUpdate Coroutine (e.g., infrequent operations)
```csharp

public class SlowTask : MonoBehaviour
{
    void Start()
    {
        Timing.RunCoroutine(SlowRoutine(), Segment.SlowUpdate);
    }

    IEnumerator<float> SlowRoutine()
    {
        while (true)
        {
            Debug.Log("Every few frames");
            yield return 1.0f;
        }
    }
}

```

---

5. Coroutine Cancellation
```csharp

public class CancellableCoroutine : MonoBehaviour
{
    CoroutineHandle handle;

    void Start()
    {
        handle = Timing.RunCoroutine(CancellableRoutine());
        Invoke("StopRoutine", 2f);
    }

    IEnumerator<float> CancellableRoutine()
    {
        while (true)
        {
            Debug.Log("Running");
            yield return 0.5f;
        }
    }

    void StopRoutine()
    {
        Timing.KillCoroutines(handle);
        Debug.Log("Stopped");
    }
}

```

---

6. Replacement Function Use
```csharp

public class CoroutineReplacementExample : MonoBehaviour
{
    void Start()
    {
        Timing.ReplacementFunction = Replace;
        Timing.RunCoroutine(FaultyRoutine());
    }

    IEnumerator<float> FaultyRoutine()
    {
        yield return float.NaN; // Will trigger replacement
    }

    IEnumerator<float> Replace(IEnumerator<float> original, CoroutineHandle handle)
    {
        Debug.Log("Replacing faulty coroutine");
        yield return 1f;
        Debug.Log("Replaced logic executed");
    }
}

```

---

7. OnPreExecute Hook
```csharp

public class PreExecuteLogger : MonoBehaviour
{
    void OnEnable()
    {
        Timing.OnPreExecute += PreTick;
    }

    void OnDisable()
    {
        Timing.OnPreExecute -= PreTick;
    }

    void PreTick()
    {
        Debug.Log("Before coroutine segment executes");
    }
}

```

---

8. Accessing Timing Data
```csharp

public class TimeAccess : MonoBehaviour
{
    void Update()
    {
        Debug.Log($"LocalTime: {Timing.LocalTime}, DeltaTime: {Timing.DeltaTime}");
    }
}

```

---

9. Tracking Current Coroutine Handle
```csharp

public class CurrentCoroutineTest : MonoBehaviour
{
    void Start()
    {
        Timing.RunCoroutine(TrackHandle());
    }

    IEnumerator<float> TrackHandle()
    {
        Debug.Log($"Handle at runtime: {Timing.CurrentCoroutine.IsValid}");
        yield return 1f;
    }
}

```

---

These cover coroutine scheduling, segmentation, timing access, lifecycle control, and extension points.

