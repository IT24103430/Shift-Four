> **Current environment:** See [the World Builder GV environment finish](docs/WORLD_BUILDER_GV_FINISH.md) for the completed wall kit, room signage, lighting, navigation startup checks and verification workflow. The [four-room progression contract](docs/FOUR_ROOM_PROGRESSION.md) and [visual style guide](docs/VISUAL_STYLE_GUIDE.md) remain authoritative. Historical plans should not override the saved scene or accepted four automatic sliding gates.

---


# Shift Four

A short first-person security-lab escape game built as the **joint project for SE3062 Intelligent Systems and SE3032 Graphics and Visualization**. The player crosses four connected lab sections, disables four autonomous security drones, activates four consoles, and reaches the exit.

**Engine:** Unity 6.3 LTS (initial baseline pinned to `6000.3.0f1`)  
**Language:** C#  
**Modeling:** Blender  
**Repository:** one Git repository / one Unity project  
**Team:** Pamudi, Nimsara, Nimthara, Asmadala

## Start with the plan

Before changing code or assets, read these files in order:

1. [Master project plan](docs/PROJECT_PLAN.md)
2. [Development roadmap - what to build first](docs/DEVELOPMENT_ROADMAP.md)
3. [Branching strategy - `main`, `develop`, feature branches](docs/BRANCHING_STRATEGY.md)
4. [Team responsibilities and handoffs](docs/TEAM_RESPONSIBILITIES.md)
5. [Assessment evidence checklist](docs/ASSESSMENT_EVIDENCE.md)
6. Your personal guide in [`docs/members/`](docs/members/)

The official GV roles are fixed:

| Student | Name | Official GV role | Section | IS agent |
| --- | --- | --- | --- | --- |
| 1 | **Pamudi** | **World Builder** | Checkpoint | Scout |
| 2 | **Nimsara** | **Systems Engineer** | Storage | Flanker |
| 3 | **Nimthara** | **Core Developer** | Server | Guard |
| 4 | **Asmadala** | **Agent Controller** | Control | Interceptor |

Each room and IS agent is additional ownership. It does **not** replace the student's prescribed GV component.

## First-time setup

1. Install Unity Hub and the **Unity 6.3.0f1** Editor with desktop build support for the demonstration platform. If the team intentionally changes patch version, everyone must use the same one.
2. Extract/clone this repository. In Unity Hub choose **Add project from disk** and select the folder containing `Assets`, `Packages`, and `ProjectSettings`.
3. Let Unity import packages and compile. Set **Active Input Handling** to **Input Manager (Old)** or **Both** if Unity requests it. Under **Project Settings > Editor**, use **Force Text** asset serialization and **Visible Meta Files**.
4. On the designated baseline/integration machine only, choose **Tools > Shift Four > Generate Greybox Once**. This creates the initial `MainLab.unity`, section/drone prefabs, materials, NavMesh, and build-scene entry. Do this once and commit the generated assets.
5. Open `Assets/Scenes/MainLab.unity` and press **Play**. Test WASD, mouse look, left-click shooting, `E` interaction, and `R` restart after win/loss.
6. Push the verified baseline to `main`, create `develop`, then use the workflow in [BRANCHING_STRATEGY.md](docs/BRANCHING_STRATEGY.md). Everyone else clones the repository and **must not rerun the generator**.

If **Tools > Shift Four** is missing, inspect the Unity Console for the first compiler/package error. See [Setup and troubleshooting](docs/SETUP.md).

## What the baseline provides

- One connected four-section level generated from an editor tool.
- First-person movement, aiming, shooting, health/damage/recovery, consoles, door, exit, and restart scaffolding.
- Four placeholder drones with Scout, Flanker, Guard, and Interceptor scripts.
- A shared waypoint/A* structure plus `NavMeshAgent` route following.
- Separate section prefabs to reduce Unity scene conflicts.

**The baseline is not the final submission.** Placeholder art, starter policies, movement feedback, and tuning must be developed by the assigned students, with regular attributable commits and viva-ready explanations.

## Branching in one sentence

`feature/*` / `fix/*` / `docs/*` -> **PR to `develop`** -> integration test -> **PR `develop` to `main`** -> release tag.

`main` is stable/submission-ready. `develop` is the shared integration branch. Do not maintain separate long-lived GV and IS branches; both modules use the same integrated game.

## Updated planning PDF

- [Shift Four - Updated Team Implementation Plan (PDF)](docs/Shift_Four_Updated_Team_Implementation_Plan.pdf)

## Essential documents

- [PROJECT_PLAN.md](docs/PROJECT_PLAN.md) - master plan and ownership
- [DEVELOPMENT_ROADMAP.md](docs/DEVELOPMENT_ROADMAP.md) - phase-by-phase order and dependencies
- [BRANCHING_STRATEGY.md](docs/BRANCHING_STRATEGY.md) - branch naming, PR flow, protection, release tagging
- [TEAM_RESPONSIBILITIES.md](docs/TEAM_RESPONSIBILITIES.md) - each member's files, handoffs, and done criteria
- [ASSESSMENT_EVIDENCE.md](docs/ASSESSMENT_EVIDENCE.md) - proof to collect for GV/IS marks and viva
- [SCOPE.md](docs/SCOPE.md) - locked game scope
- [SETUP.md](docs/SETUP.md) - first run and build
- [ARCHITECTURE.md](docs/ARCHITECTURE.md) - runtime/code flow
- [GIT_WORKFLOW.md](docs/GIT_WORKFLOW.md) - command-level daily Git workflow
- [INTEGRATION.md](docs/INTEGRATION.md) - integration, release, demo checks
- [CONTRIBUTING.md](CONTRIBUTING.md) - contributor quick start

## Repository rule

Commit `Assets/`, `Packages/`, `ProjectSettings/`, `ArtSource/`, docs, and every required `.meta` file. Do not commit Unity-generated `Library/`, `Temp/`, logs, IDE state, or build output. Preserve individual commit history; for assessed feature work use normal merge commits rather than squashing everything into one final commit.

## Implemented visual upgrade (2026-10-14)

The current MainLab includes four dual-face security gates, four FBX drone variants and four polished section prefabs. Start with [the implementation report](docs/VISUAL_UPGRADE_CHANGE_SUMMARY.md), [executed QA](docs/QA/VISUAL_UPGRADE_TEST_MATRIX.md) and [actual before/after evidence](docs/QA/VISUAL_UPGRADE_BEFORE_AFTER.md). Reapply art with **Tools → Shift Four → Refresh Visual Upgrade**, not the legacy baseline generator. Runtime gameplay C# is unchanged.
