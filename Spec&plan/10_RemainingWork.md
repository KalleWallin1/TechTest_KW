# 10 — Remaining Work

Snapshot of what's pending as of 2026-05-28 (late session). Companion to the spec docs 00–09; this file is the current "what's left to do" reference.

See `00_README.md` for the doc index and `09_CSVAnalysis.md` for the most recent architectural decisions.

## How to use this file

Items are grouped by **priority for delivery**, not by category. The first group must be done to ship; the second group is recommended polish; the rest are optional.

Each item includes:

- **What** — the concrete change
- **Where** — files / GameObjects involved
- **Why** — the rationale or what it unlocks
- **Effort** — rough estimate (S = minutes, M = an hour, L = several hours)

---

## ✅ Done since the previous draft

These items were on the previous version of this doc; they're now resolved:

- ✅ **`MainAnimation.anim` authored.** Rotation Z, Position, Scale curves keyframed against the CSV timeline at 24 fps. Sampled by CSVDriver each Update via `transformAnimationClip.SampleAnimation(animationTarget, playheadTime)`.
- ✅ **CSVDriver re-gained per-transition morph timing** with explicit start/end frames and an optional morph curve. The transform animation lives in the .anim; morph progress is parametric.
- ✅ **Tint window decoupled from morph window.** Each transition has its own `tintStartFrame`/`tintEndFrame` (drives tint crossfade + pulse).
- ✅ **Digit cube rotation has its own window** (`cubeStartFrame`/`cubeEndFrame`), independent of tint and morph. Cube reads `StateController.RotationProgress` (repurposed from a stale field).
- ✅ **Rotation conflict resolved.** `ShapeMorphBlendshapeController.applyZRotationToTransform` toggled off so the .anim owns the Z rotation cleanly.
- ✅ **Video reference working.** `VideoPlayerScrubber` component provides Play/Pause/scrub for a separate `Anim_to_unity` GameObject. Codec re-encoded to H.264 baseline profile with every-frame keyframes; Material switched from URP/Lit to URP/Unlit so the texture isn't shaded dark.

---

## ① Must do for the deliverable

### 1.1 — VR setup (planned addition)

- **What:** Enable VR rendering in the project so the deliverable can be experienced in a headset. The brief notes VR as the ideal way to experience the result and bonus points are available.
- **Where:**
  - **Package Manager** → install:
    - `XR Plugin Management` (4.x)
    - `OpenXR Plugin` (1.x)
    - Optionally `XR Interaction Toolkit` (only if you want controller input — not needed for a passive viewing experience).
  - **Project Settings → XR Plug-in Management**:
    - Tick **OpenXR** under the Windows Standalone tab.
    - Under the **OpenXR** sub-settings, add an **Interaction Profile** matching your headset (e.g. Oculus Touch Controller, Meta Quest Touch, Valve Index, etc.). At least one is required even for passive viewing.
    - Set Render Mode to **Single Pass Instanced** for performance.
  - **Project Settings → Player → Other Settings → Color Space**: confirm **Linear** (URP requires this for proper VR rendering).
  - **Scene**:
    - Replace the existing `Main Camera` with an **XR Origin (XR Rig)** GameObject (`GameObject → XR → XR Origin (VR)`). The default Main Camera should be deleted or disabled.
    - Position the XR Origin so the morph widget and reference video Quad are both in comfortable view (~1.5–2 m in front of the camera, slightly above eye height usually feels best for HMD).
    - The `Anim_to_unity` Quad displaying the reference video should be world-space positioned next to the morph widget, NOT camera-attached, so both are visible from the same head position.
  - **URP Renderer Asset**: confirm the active URP asset (e.g. `Assets/Settings/PC_RPAsset.asset`) has **Anti-aliasing (MSAA)** at 4x or 8x for VR (jaggies are very noticeable in HMD), and that `Renderer List` includes the Forward renderer (default).
- **Why:** Bonus points per the brief; also a meaningfully better viewing experience for the evaluator.
- **Effort:** M (setup is mostly clicking through Package Manager + Project Settings, but verifying everything renders correctly in HMD can take iteration).
- **Validation checklist:**
  - [ ] Headset connected (Quest Link, Air Link, or wired HMD).
  - [ ] Hit Play in editor — should render to HMD with morph + video both visible.
  - [ ] Test the build (Windows x64 standalone with VR) — launching the .exe should auto-detect the HMD.
  - [ ] Verify the morph reads correctly in 3D — it's a 2D shape on a quad, so make sure it faces the user and isn't edge-on.

### 1.2 — Windows standalone build

- **What:** Build a Windows 64-bit standalone of the project.
- **Where:** File → Build Settings → Windows x64 → Build. Output goes into `Build/` at the repo root. With VR enabled (item 1.1), the build will automatically support VR runtimes.
- **Why:** Required deliverable per the brief.
- **Effort:** S.

### 1.3 — Write `ProcessDescription.md`

- **What:** The short written description required by the brief. Should cover:
  - The three implementation variants delivered (procedural ring mesh, blendshape mesh, SDF shader) and why all three exist.
  - The design decisions for State 4 (square over star, see spec 06).
  - The CSV interpretation (state column as target state, XYZ ignored per the chosen scope — see spec 09 for the full analysis).
  - The animation timing approach (CSV-driven state-machine + per-transition windows for tint/cube/morph + AnimationClip for free-form transform animation).
  - Anything notable about the production process — DCC files in `SourceAsset/`, reference frame extraction (`Assets/SourceData/References/`), codec adjustments for the comparison video, the AI-assisted iteration journey, etc.
- **Where:** New file at repo root: `ProcessDescription.md`.
- **Why:** Required deliverable.
- **Effort:** M.

### 1.4 — Top-level `README.md`

- **What:** How to run the build and how to open the project. Includes: where the .exe is, which scene is the main one (`MainScene.unity`), Unity version (6000.3.16f1), URP, VR runtime requirements (HMD with OpenXR support), how to launch in VR vs. desktop. Mention controls if any (Debug Scrub etc.).
- **Where:** New file at repo root: `README.md`.
- **Why:** Required for the deliverable zip.
- **Effort:** S.

### 1.5 — Package the zip

- **What:** Create the deliverable zip per the brief.
- **Contents:** `Build/` (the standalone), `TechnicalTask_KW_2026/` Unity project (sans `Library/`, `Logs/`, `Temp/`, `UserSettings/` — gitignored already), `ProcessDescription.md`, `README.md`, and optionally `Spec&plan/`.
- **Effort:** S.

---

## ② Recommended polish

### 2.1 — State 4 alpha fade-out (spec 09 finding)

- **What:** When the shape enters State 4 (the "leaving the scene" terminal state per spec 09's CSV analysis), fade alpha 1 → 0 over the State 4 duration (frames ~231 → 250).
- **Where:** Easiest in `MainAnimation.anim` — animate the shape's material `_Color.a` (or `_BaseMap` alpha) on the `cir→sqr` transition window. Alternative: extend `StateController` with a `BlendedAlpha` output and have controllers consume it.
- **Why:** CSV analysis (spec 09) found State 4 always corresponds to objects leaving the scene with empty XYZ. The alpha fade communicates this visually.
- **Effort:** S–M depending on approach.

### 2.2 — Verify State 4 pulse on the active variant

- **What:** Scrub to frame 240+ in CSVDriver, confirm the State 4 (square) is visibly pulsing per `_PulseAmount`.
- **Where:** Whichever shape variant you're showing — Plan A material (`ShapeMorph_Geometry.mat`) or blendshape variant. Both reference shaders that implement `_PulseAmount`.
- **Why:** The pulse logic is wired but visual verification hasn't been done end-to-end since the timing refactor.
- **Effort:** S.

### 2.3 — Delete vestigial scripts

- **What:** Remove `Assets/Scripts/StatePositionApplicator.cs` and `Assets/Scripts/StateScaleApplicator.cs`.
- **Why:** Both are no-ops now — `StateController.BlendedScaleMultiplier` is always 1 and `BlendedPositionOffset` is always Vector2.zero (CSVDriver passes those defaults via SetPose). The applicators just multiply by 1 / add zero each frame. Keeping them is confusing.
- **When:** Safe to do now — transform animation is fully owned by `MainAnimation.anim`.
- **Effort:** S.

### 2.4 — Decide on `UVEdgeSmooth.shader`

- **What:** Either delete `Assets/Shaders/UVEdgeSmooth.shader` or wire it into a material that uses it.
- **Why:** Present but unreferenced by any material. Dead code.
- **Effort:** S.

### 2.5 — Document the single scene choice

- **What:** Note in `README.md` that `MainScene` contains all three implementation variants. Specify which GameObjects to enable/disable for A/B comparison (or which one is the "primary" the evaluator sees on Play).
- **Why:** Spec 07 planned two scenes for clean A/B comparison; reality is one scene with all variants. Document the navigation so an evaluator isn't lost.
- **Effort:** S (in the README).

---

## ③ Cleanup

### 3.1 — Mark unused `StateController` properties deprecated or remove

- **What:** `StateController.BlendedZRotationDegrees` is always 0 (CSVDriver passes 0). `BlendedScaleMultiplier` is always 1. `BlendedPositionOffset` is always Vector2.zero. `RotationProgress` is now meaningful (drives digit cube) so keep that one. The first three could be removed or marked `[System.Obsolete("Now driven by AnimationClip — always returns default")]`.
- **Where:** `Assets/Scripts/StateController.cs`.
- **Why:** Clarity. Currently three of those properties give a false impression they're driving something.
- **Effort:** S.

### 3.2 — Add status banners to drifted spec docs

- **What:** Add a "Status: superseded — see 10_RemainingWork.md" banner near the top of specs that drifted from the current architecture (see §⑤ Known spec drift below).
- **Where:** `Spec&plan/05_StateController_and_CSV.md` and `Spec&plan/07_ProjectStructure.md` are the most-drifted.
- **Why:** A reviewer reading the specs in order will see outdated claims. The banner steers them to the current reference.
- **Effort:** S.

---

## ④ Stretch (skip unless time allows)

### 4.1 — Long data file driver

- **What:** Implement the multi-object scene from spec 09 — parse `Long_Data_Free_Form.csv` (22 objects, 12,682 rows), maintain an object pool keyed by `object_id`, spawn/despawn objects per their lifespan, drive each from its CSV slice.
- **Where:** New `Assets/Scripts/CSVLongDriver.cs` + `CSVObjectController.cs` + `CSVObjectPool.cs` per spec 09's plan.
- **Why:** Brief calls this optional. Would showcase the SDF variant's scalability (Plan B stays under 100 tris × N objects, Plan A wouldn't).
- **Effort:** L.

### 4.2 — SDF glow halo on State 4

- **What:** Render a soft halo around the square in Plan B by sampling the SDF at a larger threshold with falloff.
- **Where:** `Assets/Shaders/ShapeMorphSDF.shader`.
- **Why:** Free in shader cost, reinforces the "creative State 4" semantic.
- **Effort:** S.

### 4.3 — Continuous-forward cube rotation on loop

- **What:** Currently when the CSV loops back from frame 250 → 0, the digit cube snaps backward 270° from "digit 4" to "digit 1." Add an accumulator that knows about loops so the cube keeps rotating forward indefinitely.
- **Where:** `NumberCubeController.cs`.
- **Why:** Nice-to-have polish. The snap matches what tint does on loop, so this is purely cosmetic.
- **Effort:** S.

---

## ⑤ Known spec drift

Specs that no longer reflect the current architecture (worth noting in section 3.2 cleanup):

| Spec | Claim vs reality |
|---|---|
| `05_StateController_and_CSV.md` | Describes a TransitionTiming list with per-transition curves for everything. **Current reality**: CSVDriver has per-transition `tintStartFrame`/`tintEndFrame`, `cubeStartFrame`/`cubeEndFrame`, `morphStartFrame`/`morphEndFrame`+ `morphCurve`. Transform animation (rotation/scale/position) moved to `MainAnimation.anim`. |
| `07_ProjectStructure.md` | Plans separate `PlanA_GeometricMorph.unity` and `PlanB_SDFMorph.unity` scenes. **Reality**: single `MainScene.unity` with all variants in it. Also adds `MainAnimation.anim` and reference frames in `Assets/SourceData/References/` not described in this doc. |
| `02_ImplementationPlan.md` | "Dual track" Plan A vs Plan B. **Reality**: three variants — procedural ring (Plan A), blendshape mesh (Plan A alt, the user's primary), and SDF (Plan B). |
| `06_State4_Design.md` | Pulse + alpha fade-out as core behaviors. **Pulse is wired but alpha fade is unimplemented** (see item 2.1). |
| `09_CSVAnalysis.md` | Long-data multi-object scene with object pool. **Unimplemented** (see item 4.1). |

These aren't bugs — the specs were planning artifacts. They're useful as a record of the design journey for `ProcessDescription.md`, but should carry a "see 10_RemainingWork.md for current state" pointer.

---

## ⑥ Decisions captured

These aren't tasks per se — they're choices that shaped the current state.

| Question | Decision |
|---|---|
| Does `XYZ` position from CSV drive anything? | **No** — widget stays centered. XYZ ignored. |
| Curves vs. Animation Window for transform animation? | **Animation Window** (`MainAnimation.anim`). Curves were tried inside CSVDriver but the editor UX was too painful. |
| Single scene vs split Plan A/B? | **Single** (`MainScene.unity`). To compare implementations, enable/disable the relevant GameObjects. |
| Include `Spec&plan/` in deliverable zip? | TBD — recommended yes (showcases process). |
| State 4 alpha fade behavior? | TBD — see item 2.1. Default proposal: animate `_Color.a` from 1 → 0 over frames ~231–250 in the .anim. |
| Long data file? | Skip for delivery unless time allows. |
| In-Unity video sync with CSVDriver? | **Dropped.** Replaced by standalone `VideoPlayerScrubber` component on a separate `Anim_to_unity` GameObject. The two are visually compared by setting matching frame numbers. |

---

## ⑦ Quick "what's the path to ship?"

Minimum to deliver:

1. Do **1.1** (VR setup) — bonus points and meaningfully better viewing experience.
2. Do **1.2** (Windows build) — required.
3. Do **1.3** (ProcessDescription.md) — required.
4. Do **1.4** (README.md) — required.
5. Do **1.5** (zip the deliverable) — required.

That's the critical path. Everything else is polish. If you have an hour, also do **2.1** (alpha fade) and **2.3** (delete vestigial scripts). If you have an evening, **3.2** (spec banners) makes the included docs less confusing to a reviewer.

**Realistic time estimate:** 1.1 (VR) is the biggest single chunk — budget 1–2 hours including HMD testing iteration. 1.2/1.4/1.5 are 5 minutes each. 1.3 (the write-up) is the most variable — could be 30 minutes for a tight version, or 2 hours for a polished one with the design journey baked in.
