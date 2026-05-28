# 07 — Unity Project Structure

## Folder layout

The repository root is `TechTest_KW/`. The Unity project lives directly under `TechnicalTask_KW_2026/` (no extra `UnityProject/` wrapper). Planning docs, source files from Rightware, and DCC source files are siblings of the Unity project — kept outside the Unity project so they don't pollute `Assets/`.

```
TechTest_KW/                            ← repo root
├── .gitignore
│
├── Spec&plan/                          ← THIS FOLDER (the planning docs)
│   ├── 00_README.md
│   ├── 01_AnimationAnalysis.md
│   ├── 02_ImplementationPlan.md
│   ├── 03_TechnicalSpec_PlanA_Geometric.md
│   ├── 04_TechnicalSpec_PlanB_SDF.md
│   ├── 05_StateController_and_CSV.md
│   ├── 06_State4_Design.md
│   ├── 07_ProjectStructure.md
│   ├── 08_OpenQuestions.md
│   └── 09_CSVAnalysis.md
│
├── TaskSourceFiles/                    ← Provided by Rightware (read-only reference)
│   ├── Animation.mkv                   (reference video)
│   ├── Technical_Artist_Assessment.pdf (the brief)
│   ├── Assets/                         (Triangle.png, Hexagon.png, Circle.png, RotationTexture.png)
│   └── Data/                           (Short_Data_Animation_Match.csv, Long_Data_Free_Form.csv)
│
├── SourceAsset/                        ← DCC working files (kept outside Unity project)
│   ├── Sources.blend                   (Blender working file — any modeling/UV/export work)
│   └── Sources.max                     (3ds Max working file — same purpose)
│
├── TechnicalTask_KW_2026/              ← The Unity project root
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── MainScene.unity                  (currently the active scene — staging area)
│   │   │   ├── PlanA_GeometricMorph.unity
│   │   │   └── PlanB_SDFMorph.unity
│   │   ├── MainWidget/                          (the shape + number widget assets)
│   │   │   ├── MainWidgetMaterial.mat           (URP/Unlit for now; will switch to ShapeMorph shader)
│   │   │   └── Textures/
│   │   │       ├── Triangle.png                 (reference / Plan A optional alpha mask)
│   │   │       ├── Hexagon.png
│   │   │       ├── Circle.png
│   │   │       ├── Square.png                   (added — State 4, replaces star)
│   │   │       └── RotationTexture.png          (digit strip "1 2 3 4")
│   │   ├── SourceData/                          (CSV TextAssets, referenced from CSVDriver inspector)
│   │   │   ├── Short_Data_Animation_Match.csv
│   │   │   └── Long_Data_Free_Form.csv          (optional per brief)
│   │   ├── Scripts/
│   │   │   ├── StateController.cs
│   │   │   ├── CSVDriver.cs                     (parses CSV TextAsset, drives playback)
│   │   │   ├── CSVObjectController.cs           (per-object position + state + alpha fade)
│   │   │   ├── CSVObjectPool.cs                 (long-data multi-object spawn/despawn)
│   │   │   ├── MorphMeshGenerator.cs            (Plan A — mesh with baked morph targets)
│   │   │   ├── ShapeMorphController.cs          (Plan A — material driver)
│   │   │   ├── ShapeMorphSDFController.cs       (Plan B — material driver)
│   │   │   ├── NumberStripController.cs         (shared — UV scroll on digit quad)
│   │   │   ├── SceneSwitcher.cs                 (Space toggles between scenes)
│   │   │   └── UIPanel.cs                       (minimal debug UI)
│   │   ├── Shaders/
│   │   │   ├── ShapeMorphGeometric.shader
│   │   │   ├── ShapeMorphSDF.shader
│   │   │   └── NumberStrip.shader
│   │   ├── Prefabs/
│   │   │   ├── ShapeMorph_PlanA.prefab
│   │   │   └── ShapeMorph_PlanB.prefab
│   │   ├── Sources/                             (empty placeholder — TBD use)
│   │   ├── Editor/
│   │   │   └── BuildScript.cs                   (Tools > Build Windows menu command)
│   │   ├── Settings/
│   │   │   ├── PC_RPAsset.asset / PC_Renderer.asset
│   │   │   ├── Mobile_RPAsset.asset / Mobile_Renderer.asset
│   │   │   ├── DefaultVolumeProfile.asset
│   │   │   ├── SampleSceneProfile.asset
│   │   │   └── UniversalRenderPipelineGlobalSettings.asset
│   │   └── InputSystem_Actions.inputactions     (Unity 6 default; can be removed if unused)
│   ├── Packages/
│   ├── ProjectSettings/
│   ├── Library/                        (gitignored — auto-generated)
│   ├── Logs/                           (gitignored)
│   └── UserSettings/                   (gitignored)
│
├── Build/                              ← Output Windows build (gitignored)
│   ├── TechnicalTask_KW.exe
│   ├── TechnicalTask_KW_Data/
│   └── UnityPlayer.dll
│
├── ProcessDescription.md               ← The required written process description (production write-up)
└── README.md                           ← How to run the build, how to open the project
```

### Notes on out-of-Unity folders

- **`TaskSourceFiles/`** is the unmodified material handed over by Rightware. Anything from here that ends up in the build is *copied* into `TechnicalTask_KW_2026/Assets/MainWidget/Textures/` (images) or `Assets/SourceData/` (CSVs). This keeps a clean separation between "what was given" and "what was authored". Note: CSVs live in `SourceData/` (loaded as `TextAsset` via inspector reference), not `StreamingAssets/` — chosen for simpler runtime access since the data is always shipped with the build.
- **`SourceAsset/`** holds DCC working files (`.blend`, `.max`) — original source for any modeled or UV-prepared geometry that gets exported into the Unity project. These are kept outside the Unity project to avoid Unity attempting to import them, and to keep `.blend1` backup churn out of the project. If a `.fbx` is exported from one of these for use in Plan A, the `.fbx` goes into `Assets/Models/` and the `.blend`/`.max` stays here.
- The DCC source files and this folder layout are also useful raw material for the `ProcessDescription.md` write-up, which can reference the modeling/baking pipeline as well as the in-engine work.

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
