# Nimthara GV acceptance matrix

Baseline: all runtime, Editor compile and Blender checks NOT_VERIFIED. Results are updated per phase; source inspection is not a gameplay pass.

| Check | Setup / steps | Expected | Actual / status |
|---|---|---|---|
| Compiler/API | Unity 6000.6.4f1 batch import isolated copy | No C# errors/new obsolete warnings | Import running; NOT_VERIFIED |
| Identity prerequisites | Each id, both orders, duplicates, other ids | Independent AND; counts at most four | Source implemented; NOT_VERIFIED |
| Migration | Install twice, retain custom child/node edit | Four gates, five+ nodes, no scene reset | Source implemented; NOT_VERIFIED |
| Motion/collision | Step fixed updates at yaw 0/90 | Bolts first, local X panels, barrier until clear | Source implemented; NOT_VERIFIED |
