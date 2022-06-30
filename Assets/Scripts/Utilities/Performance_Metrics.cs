using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;

namespace Rendergon.Utilities
{
    public static class Performance_Metrics
    {
        public class Watch
        {
            public System.Diagnostics.Stopwatch thisWatch;
            public string task;
            public int watchNo;
            public bool b_ShowDebugMssg=true;

            public Watch (string thisTask)
            {
                task = thisTask;
                watchNo = WatchNoCalculation();
                b_ShowDebugMssg = false;
                if (b_ShowDebugMssg) UnityEngine.Debug.Log("Started Watch No. " + watchNo + ". Task:" + thisTask);
                if (b_ShowDebugMssg) System.Diagnostics.Debug.WriteLine("Started Watch No. " + watchNo + ". Task:" + thisTask);
                thisWatch = System.Diagnostics.Stopwatch.StartNew();
            }

            public void StartWatch(string thisTask)
            {
                thisWatch = System.Diagnostics.Stopwatch.StartNew();
                task = thisTask;
                watchNo = WatchNoCalculation();
                if (b_ShowDebugMssg) UnityEngine.Debug.Log("Started Watch No. " + watchNo + ". Task:" + thisTask);
                if(b_ShowDebugMssg) System.Diagnostics.Debug.WriteLine("Started Watch No. " + watchNo + ". Task:" + thisTask);
            }

            public double StopWatch()
            {
                thisWatch.Stop();
                var elapsedMs = thisWatch.ElapsedMilliseconds;
                var secs = System.Math.Round((double)elapsedMs / 1000, 2);
                if (b_ShowDebugMssg) UnityEngine.Debug.Log("Stopped Watch No. " + watchNo + ". Task:" + task + ". Duration:" + secs.ToString("0.00") + " seconds.");
                if (b_ShowDebugMssg) System.Diagnostics.Debug.WriteLine("Stopped Watch No. " + watchNo + ". Task:" + task + ". Duration:" + secs.ToString("0.00") + " seconds.");

                return secs;
            }

            int WatchNoCalculation()
            {
                return UnityEngine.Random.Range(0, 20);
            }
        }
    }
}
