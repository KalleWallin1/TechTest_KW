# 07 — Unity Project Structure

## Folder layout

```
TechnicalTask_KW_2026/
├── Spec&plan/                          ← THIS FOLDER (the planning docs)
│   ├── 00_README.md
│   ├── 01_AnimationAnalysis.md
│   ├── 02_ImplementationPlan.md
│   ├── 03_TechnicalSpec_PlanA_Geometric.md
│   ├── 04_TechnicalSpec_PlanB_SDF.md
│   ├── 05_StateController_and_CSV.md
│   ├── 06_State4_Design.md
│   ├── 07_ProjectStructure.md
│   └── 08_OpenQuestions.md
│
├── UnityProject/                       ← The Unity project root
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── PlanA_GeometricMorph.unity
│   │   │   └── PlanB_SDFMorph.unity
│   │   ├── Scripts/
│   │   │   ├── StateController.cs
│   │   │   ├── CSVDriver.cs
│   │   │   ├── MorphMeshGenerator.cs        (Plan A)
│   │   │   ├── ShapeMorphController.cs      (Plan A)
│   │   │   ├── ShapeMorphSDFController.cs   (Plan B)
│   │   │   ├── NumberStripController.cs     (shared)
│   │   │   ├── SceneSwitcher.cs             (Space toggles between scenes)
│   │   │   └── UIPanel.cs                   (minimal debug UI)
│   │   ├── Shaders/
│   │   │   ├── ShapeMorphGeometric.shader
│   │   │   ├── ShapeMorphSDF.shader
│   │   │   └── NumberStrip.shader
│   │   ├── Materials/
│   │   │   ├── ShapeMorph_Geometric.mat
│   │   │   ├── ShapeMorph_SDF.mat
│   │   │   └── NumberStrip.mat
│   │   ├── Textures/
│   │   │   ├── Triangle.png             (reference / Plan A optional)
│   │   │   ├── Hexagon.png
│   │   │   ├── Circle.png
│   │   │   └── RotationTexture.png      (used by both plans for the number layer)
│   │   ├── Prefabs/
│   │   │   ├── ShapeMorph_PlanA.prefab
│   │   │   └── ShapeMorph_PlanB.prefab
│   │   ├── StreamingAssets/
│   │   │   ├── ShortData.csv            (from Part 2)
│   │   │   └── LongData.csv             (from Part 2, optional)
│   │   └── Settings/
│   │       └── URP-PipelineAsset.asset
│   │
│   ├── Packages/
│   ├── ProjectSettings/
│   └── Library/   (gitignored)
│
├── Build/                              ← Output Windows build
│   ├── TechnicalTask_KW.exe
│   ├── TechnicalTask_KW_Data/
│   └── UnityPlayer.dll
│
├── ProcessDescription.md               ← The required written process description
└── README.md                           ← How to run the build, how to open the project
```

## Scene contents (both scenes share the same structure)

### Common GameObjects

- **`Camera`** — orthographic, clear color `#444444`, near 0.3, far 100
- **`Canvas` (Screen Space Overlay)** — minimal UI: state label, time, data source buttons, key hints
- **`EventSystem`**
- **`StateController` (empty GameObject)** — holds the `StateController` and `CSVDriver` components, plus `SceneSwitcher`

### Plan A scene additions

- **`ShapeMorph_PlanA` prefab** — empty parent with two children:
  - `ShapeRing` — MeshFilter + MeshRenderer + `ShapeMorphController`, mesh generated at Awake by `MorphMeshGenerator`, material `ShapeMorph_Geometric.mat`
  - `NumberQuad` — quad mesh, `NumberStripController`, material `NumberStrip.mat`

### Plan B scene additions

- **`ShapeMorph_PlanB` prefab** — empty parent with two children:
  - `ShapeQuad` — quad mesh, `ShapeMorphSDFController`, material `ShapeMorph_SDF.mat`
  - `NumberQuad` — quad mesh, `NumberStripController`, material `NumberStrip.mat`

In both scenes the `StateController` is wired to the prefab's shape and number controllers in the inspector.

## Render pipeline

- **URP** (Universal Render Pipeline)
- Color space: **Linear** (correct color math for the smooth tint transitions)
- Anti-aliasing: MSAA 4x (helps the geometric ring; SDF doesn't need it)
- Single point of camera clear color matching `#444444` background

## Build configuration

- Target: **Windows 64-bit Standalone**
- Both scenes added to Build Settings
- Build scripts in `Assets/Editor/BuildScript.cs` to produce the build with a single menu command (`Tools > Build Windows`)
- Output directory: `../Build/` (relative to the Unity project root)

## Version control

`.gitignore` excludes:
- `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `MemoryCaptures/`
- `*.csproj`, `*.sln`, `*.suo`, `*.user`
- Visual Studio / Rider folders

Initial commit will include only `Assets/`, `Packages/`, `ProjectSettings/`.

## Optional: VR support

The brief mentions VR as an ideal way to develop and experience the result. If VR is enabled:
- Switch URP asset to one with XR enabled
- Add OpenXR plugin
- Camera moves to an `XR Origin` rig
- Both Plan A and Plan B work in VR because they're 3D objects in 3D space; the shapes just need to be positioned at a comfortable viewing distance from the user's head
- The pulsing state 4 is especially nice in VR because the depth perception makes the breathing motion more tangible

This is a stretch goal — not part of the core deliverable.
