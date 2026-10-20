# Student 4 measured performance

Unity 6000.6.4f1, Apple M1, 8 GiB memory, Metal, Editor batch mode, two job workers. Each workload samples 720 frames at fixed simulation delta 1/60 with a target rate of 60. Wall intervals include Editor, rendering, harness and pacing; these are not target-player FPS benchmarks. Scopes are inclusive and nested; never add their totals as independent CPU frame costs.

The integrated and Control QA profiles ran sequentially without another QA run. The final animation profile was rerun alone after a diagnostic run overlapped its earlier measurement. Raw `profile-single.xml` identifies that rerun. Setup/door-opening time is excluded from the sampled window.

## Wall frame intervals

| Build | p50 ms | p95 ms | Max ms | Frames >33.333 ms |
| --- | ---: | ---: | ---: | ---: |
| integrated | 16.663 | 16.960 | 21.093 | 0 |
| feature | 16.652 | 16.895 | 26.888 | 0 |
| control | 16.678 | 16.797 | 18.532 | 0 |

## Inclusive component samples

Zeros for absent modules are shown as **absent**, even if Unity registers a recorder handle with no calls. “Max frame” is the sum of calls to that scope within one sampled frame, not a maximum individual call. Physics.Simulate includes the entire scene.

| Build | Scope | Calls | Total ms | Max frame ms | Max calls/frame |
| --- | --- | ---: | ---: | ---: | ---: |
| integrated | Route | 7 | 0.309 | 0.128 | 4 |
| integrated | Motor | 2160 | 10.327 | 2.897 | 4 |
| integrated | Decision | 59 | 0.180 | 0.028 | 4 |
| integrated | Animation | 2876 | 6.668 | 0.027 | 4 |
| integrated | Body | absent / no samples | 0.000 | 0.000 | 0 |
| integrated | Effects | absent / no samples | 0.000 | 0.000 | 0 |
| integrated | Fire | 4 | 1.041 | 1.041 | 4 |
| integrated | WreckCreate | absent / no samples | 0.000 | 0.000 | 0 |
| integrated | WreckTick | absent / no samples | 0.000 | 0.000 | 0 |
| integrated | Physics.Simulate | 599 | 73.306 | 0.617 | 1 |
| feature | Route | 78 | 1.399 | 0.674 | 11 |
| feature | Motor | 2160 | 5.895 | 0.753 | 4 |
| feature | Decision | 63 | 8.116 | 3.099 | 4 |
| feature | Animation | 2880 | 48.183 | 22.734 | 4 |
| feature | Body | 2880 | 368.972 | 16.971 | 4 |
| feature | Effects | 2887 | 6.165 | 0.317 | 8 |
| feature | Fire | 16 | 1.998 | 1.539 | 4 |
| feature | WreckCreate | 4 | 20.901 | 20.901 | 4 |
| feature | WreckTick | 716 | 0.697 | 0.401 | 4 |
| feature | Physics.Simulate | 600 | 58.105 | 1.916 | 1 |
| control | Route | 504 | 4.038 | 0.775 | 96 |
| control | Motor | 2160 | 9.860 | 1.995 | 4 |
| control | Decision | 59 | 20.713 | 7.716 | 4 |
| control | Animation | 2876 | 7.364 | 0.032 | 4 |
| control | Body | absent / no samples | 0.000 | 0.000 | 0 |
| control | Effects | absent / no samples | 0.000 | 0.000 | 0 |
| control | Fire | 4 | 0.200 | 0.200 | 4 |
| control | WreckCreate | absent / no samples | 0.000 | 0.000 | 0 |
| control | WreckTick | absent / no samples | 0.000 | 0.000 | 0 |
| control | Physics.Simulate | 599 | 61.078 | 0.276 | 1 |

## Largest wall intervals and attribution limits

The tables below correlate the five longest wall intervals with measured scopes. A scope appearing in a slow frame does not prove causation. The Physics.Simulate column is scene-wide; “destruction” also includes visual effects and unrelated physics. There is no substantial >33.333 ms spike in these sampled runs; scene load/import time is outside the window.

### integrated

| Frame | Phase | Wall ms | Decision ms | Body ms | Animation ms | Wreck create ms | Native physics ms |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 541 | destruction | 21.093 | 0.000 | 0.000 | 0.001 | 0.000 | 0.133 |
| 481 | firing | 18.388 | 0.000 | 0.000 | 0.011 | 0.000 | 0.617 |
| 401 | prediction_stress | 18.233 | 0.000 | 0.000 | 0.012 | 0.000 | 0.106 |
| 710 | settled | 17.800 | 0.000 | 0.000 | 0.001 | 0.000 | 0.091 |
| 345 | navigation | 17.760 | 0.000 | 0.000 | 0.011 | 0.000 | 0.109 |
### feature

| Frame | Phase | Wall ms | Decision ms | Body ms | Animation ms | Wreck create ms | Native physics ms |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 540 | destruction | 26.888 | 0.000 | 0.001 | 22.734 | 20.901 | 0.000 |
| 36 | live | 20.783 | 0.000 | 1.901 | 0.024 | 0.000 | 0.000 |
| 571 | destruction | 20.669 | 0.000 | 0.001 | 0.051 | 0.000 | 0.289 |
| 481 | firing | 19.194 | 0.000 | 0.011 | 0.048 | 0.000 | 0.411 |
| 0 | live | 18.935 | 0.000 | 16.971 | 0.035 | 0.000 | 0.000 |
### control

| Frame | Phase | Wall ms | Decision ms | Body ms | Animation ms | Wreck create ms | Native physics ms |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 47 | live | 18.532 | 0.000 | 0.000 | 0.016 | 0.000 | 0.000 |
| 273 | navigation | 17.949 | 0.000 | 0.000 | 0.014 | 0.000 | 0.124 |
| 486 | firing | 17.546 | 0.000 | 0.000 | 0.011 | 0.000 | 0.101 |
| 106 | live | 17.214 | 0.000 | 0.000 | 0.014 | 0.000 | 0.124 |
| 439 | prediction_stress | 17.177 | 0.000 | 0.000 | 0.011 | 0.000 | 0.103 |

## Observed high-cost frames

The feature's longest wall interval, **26.888 ms**, occurs at frame 540 when the fixture kills all four drones together. Four wreck creations consume **20.901 ms** inside **22.734 ms** of inclusive animation work. This is a measured one-time destruction setup burst, consistent with `DroneWreckMotion.Create` building rigidbodies, primitive/convex shapes and generated meshes; the nested values must not be added. It exceeds a 16.667 ms frame budget despite staying below the 33.333 ms reporting threshold. Only four creation samples were collected, so there is no stable per-wreck percentile claim.

At the first sampled feature frame, the four DroneBody updates total **16.971 ms** within an **18.935 ms** wall interval. Subsequent body costs dominate the sustained component total but are much smaller in most frames. This isolates a cold/start-of-window collision-synchronization cost; without deeper native call stacks the exact cause is unresolved. Control decision evaluation reaches **7.716 ms** in one sampled frame, with up to **96 route calls**. These measurements support investigating startup synchronization and tactical candidate-query reuse before any optimization. They do not justify removing collision protection or changing the shared graph architecture. A repeat development-player capture is still required to separate Editor startup overhead from production cost.

## Allocation measurement and source findings

**Allocation result: unavailable.** The known live 1 MiB allocation returned a zero delta from `GC.GetAllocatedBytesForCurrentThread` on this Mono runtime. The raw microbenchmark and frame GC columns therefore cannot establish zero allocation or allocate bytes to a component. Calibration files are retained for each build.

Cached delegates remove reflection from the repeated microbenchmark bodies. Those timings repeat methods at a single timestamp with warmed state; they do not represent full per-frame or player-capture performance. A* uses an actual all-open node-0-to-node-4 query (73 m). Individual source paths allocate route lists, cost/parent/open/closed arrays, native path wrappers/corner arrays, candidate and search lists, and tactical physics query arrays. Those are confirmed allocation sites in source, not measured byte counts. No production optimization was made without a supported allocation capture.

Four accepted shots were verified by actual player HP reductions. In the advanced feature, persistent effect objects are ticked rather than newly instantiated for every shot; fatal damage creates four wrecks and transient convex collision meshes. Wreck setup has a measured one-time cost (table above), while settling updates and native physics are measured separately. The per-frame DroneBody scopes are the largest sustained feature component in this workload; their measured cost remains within these sampled Editor frame intervals. This evidence does not justify changing the collision architecture.

Recommended completion: use Unity Profiler allocation call stacks or a supported recorder in a development player, repeat representative navigation/combat on the target machine, and inspect extended sessions. Do not claim zero GC, a capture-rate improvement or production frame guarantees from these tests.
