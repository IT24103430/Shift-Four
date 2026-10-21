# Shift Four — Unity 6 URP setup

**Branch:** `chore/gv-migrate-to-urp`

## Purpose
The previous builder chose the Built-in `Standard` shader and did not declare a URP package. This update prioritizes URP/Lit and provides a one-time editor setup that updates Graphics, *all* Quality levels, and saved project materials.

## Procedure
1. **Back up or commit your existing work.** Extract the supplied ZIP in a separate directory; do not overwrite the team's `.git` history.
2. Open in **Unity 6000.6.4f1**. Let Package Manager resolve Universal RP and let the C# scripts compile. The project manifest requests URP `17.5.0`; if your Unity version resolves a newer matching patch, keep Unity's resulting manifest and lock file together.
3. Use **Tools → Shift Four → Configure URP Project**. It reuses an existing URP pipeline asset, if one is present, to avoid disturbing your local setup. Otherwise it generates a URP pipeline and renderer asset under Assets/Settings.
4. Check **Edit → Project Settings → Graphics → Default Render Pipeline**. Check **Project Settings → Quality** for each level, including Windows/Mac/Linux; each should point to the same URP asset.
5. Open MainLab, verify floor/walls/drone materials, and restart Unity. If imported OBJ materials are pink, run **Tools → Shift Four → Upgrade Materials to URP** and inspect model-imported materials.
6. Check gameplay, door interaction, rotor animation, and Console errors. **Do not run Generate Greybox Once on an already authored scene**: the existing tool intentionally refuses to overwrite it.
7. Commit `Assets/Settings`, materials, editor scripts, `Packages/manifest.json`, Unity-generated `Packages/packages-lock.json`, and actual `ProjectSettings` modifications after verification.

## Limits
The distributed archive predates the URP configuration you manually made on your computer. Existing locally created `ShiftFour_URP` assets and their GUIDs are not available here. The setup command intentionally reuses existing pipeline assets; run it in your current project after copying the update.

No Unity Editor or Blender runtime is available in the packaging environment; tests are structural rather than in-Editor Play-mode tests. If Unity cannot create a valid pipeline asset automatically, make one through **Assets → Create → Rendering → URP Asset (with Universal Renderer)** and rerun setup.
