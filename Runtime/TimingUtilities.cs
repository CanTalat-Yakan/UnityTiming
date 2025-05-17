using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEssentials
{
    public partial class Timing
    {// ================== Corrected Utility Coroutines ==================

        /// <summary>
        /// Waits until the condition is true before continuing
        /// </summary>
        public static IEnumerator<float> WaitUntil(System.Func<bool> condition)
        {
            while (!condition())
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits while the condition is true before continuing
        /// </summary>
        public static IEnumerator<float> WaitWhile(System.Func<bool> condition)
        {
            while (condition())
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits for the specified number of seconds in real time (ignoring time scale)
        /// </summary>
        public static IEnumerator<float> WaitForRealSeconds(float seconds)
        {
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < seconds)
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits until the end of the current frame before continuing
        /// </summary>
        public static IEnumerator<float> WaitForEndOfFrame()
        {
            yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits until the next fixed update before continuing
        /// </summary>
        public static IEnumerator<float> WaitForFixedUpdate()
        {
            yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits for the specified number of frames before continuing
        /// </summary>
        public static IEnumerator<float> WaitForFrames(int frameCount)
        {
            while (frameCount-- > 0)
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits until the predicate returns true before continuing
        /// </summary>
        public static IEnumerator<float> WaitUntil(System.Func<bool> predicate, float timeout)
        {
            float startTime = Time.time;
            while (!predicate() && (Time.time - startTime < timeout || timeout <= 0f))
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits while the predicate returns true before continuing
        /// </summary>
        public static IEnumerator<float> WaitWhile(System.Func<bool> predicate, float timeout)
        {
            float startTime = Time.time;
            while (predicate() && (Time.time - startTime < timeout || timeout <= 0f))
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits until all coroutines in the list have completed
        /// </summary>
        public static IEnumerator<float> WaitForAll(params CoroutineHandle[] handles)
        {
            foreach (var handle in handles)
            {
                while (Instance._handleToIndex.ContainsKey(handle))
                    yield return WaitForOneFrame;
            }
        }

        /// <summary>
        /// Waits until any coroutine in the list has completed
        /// </summary>
        public static IEnumerator<float> WaitForAny(params CoroutineHandle[] handles)
        {
            bool anyRunning = true;
            while (anyRunning)
            {
                anyRunning = false;
                foreach (var handle in handles)
                {
                    if (!Instance._handleToIndex.ContainsKey(handle))
                        yield break;
                    anyRunning = true;
                }
                yield return WaitForOneFrame;
            }
        }

        /// <summary>
        /// Waits for the specified number of seconds (affected by time scale)
        /// </summary>
        public static IEnumerator<float> WaitForSeconds(float seconds)
        {
            float startTime = LocalTime;
            while (LocalTime - startTime < seconds)
                yield return WaitForOneFrame;
        }

        /// <summary>
        /// Waits for the specified number of seconds (unaffected by time scale)
        /// </summary>
        public static IEnumerator<float> WaitForSecondsRealtime(float seconds)
        {
            return WaitForRealSeconds(seconds);
        }

        /// <summary>
        /// Waits until the next frame where the condition is true
        /// </summary>
        public static IEnumerator<float> WaitUntilNext(System.Func<bool> condition)
        {
            bool wasFalse = true;
            while (true)
            {
                if (!condition())
                {
                    wasFalse = true;
                }
                else if (wasFalse)
                {
                    yield break;
                }
                yield return WaitForOneFrame;
            }
        }
    }
}
