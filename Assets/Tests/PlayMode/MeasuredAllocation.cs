#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine.Profiling;
namespace ShiftFour.Tests
{
    // Unity's Mono GC.GetAllocatedBytesForCurrentThread is not a functioning counter here.
    // Calibrate the actual allocation marker before accepting any zero-allocation result.
    internal static class MeasuredAllocation
    {
        public static long Events(Action action, out int samples)
        {
            bool enabled=Profiler.enabled; Profiler.enabled=true;
            try
            {
                using var recorder=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"GC.Alloc",65536,
                    ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                action(); recorder.Stop();
                Assert.That(recorder.Valid,Is.True,"GC.Alloc recorder unavailable");
                Assert.That(recorder.WrappedAround,Is.False,"Increase recorder capacity before interpreting allocation results");
                samples=recorder.Count;long events=0;
                for(int i=0;i<samples;i++)events+=recorder.GetSample(i).Count;
                return events;
            }
            finally { Profiler.enabled=enabled; }
        }
        public static long Calibrate()
        {
            long events=Events(()=>{var known=new byte[1024*1024];known[0]=1;GC.KeepAlive(known);},out var samples);
            Assert.That(samples,Is.GreaterThan(0),"Known allocation must be recorded");
            Assert.That(events,Is.GreaterThan(0),"Known allocation must generate an allocation event");
            return events;
        }
    }
}
#endif
