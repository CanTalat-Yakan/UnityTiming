using System.Collections.Generic;
using UnityEngine;

namespace UnityEssentials
{
    public class TimingTest : MonoBehaviour
    {
        public void Start()
        {
            Timing.RunCoroutine(MyCoroutine());
        }

        IEnumerator<float> MyCoroutine()
        {
            Debug.Log("Start");
            yield return 2f;
            Debug.Log("2 second passed");
            yield return Timing.WaitForOneFrame;
            Debug.Log("1 frame passed");
        }
    }
}
