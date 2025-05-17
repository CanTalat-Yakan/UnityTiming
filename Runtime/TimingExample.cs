using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEssentials
{
    public class TimingExample : MonoBehaviour
    {
        private int number = 0;
        private const int numberOfItems = 1000;
        [NonSerialized]
        private bool[] bools = new bool[numberOfItems];

        public void Start()
        {
            Timing.RunCoroutine(MyCoroutine2());
            for (int i = 0; i < numberOfItems; i++)
                Timing.RunCoroutine(MyCoroutine(i));

            Debug.Log(number);
        }

        IEnumerator<float> MyCoroutine(int index)
        {
            for (int i = 0; i < numberOfItems; i++)
            {
                number++;
                bools[i] = i % 2 == 0;
                yield return 0;
            }
            Debug.Log(number);
        }

        IEnumerator<float> MyCoroutine2()
        {
            Debug.Log("Start");
            yield return 5f;
            Debug.Log("5 second passed");
            yield return Timing.WaitForOneFrame;
            Debug.Log("1 frame passed");
        }
    }
}