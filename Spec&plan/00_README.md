# Technical Artist Assessment — Spec & Plan

Author: Kalle Wallin
Date: 2026-05-26
Target engine: Unity 6 (6000.3.16f1), URP, Windows standalone build

This folder holds the planning and specification documents for the Rightware Technical Artist Assessment. It is the source of truth for the implementation that lives in the parent `TechnicalTask_KW_2026` folder.

## Contents

| File | Purpose |
|---|---|
| `00_README.md` | This file — index of the spec set |
| `01_AnimationAnalysis.md` | Detailed read of the reference animation (timing, color, motion, state structure) |
| `02_ImplementationPlan.md` | The dual-track plan: geometric vertex morph (Plan A) and SDF (Plan B). Engineering trade-offs and decision rationale |
| `03_TechnicalSpec_PlanA_Geometric.md` | Plan A spec — mesh topology, vertex attributes, shader, triangle budget |
| `04_TechnicalSpec_PlanB_SDF.md` | Plan B spec — SDF shader math, parameter blending, triangle budget |
| `05_StateController_and_CSV.md` | State machine API, CSV format, Part 2 data driving |
| `06_State4_Design.md` | State 4 — pentagram + pulsing circle interpretation |
| `07_ProjectStructure.md` | Unity folder layout, scenes, prefabs, build configuration |
| `08_OpenQuestions.md` | Things left to decide or confirm during implementation |
| `09_CSVAnalysis.md` | Analysis of the actual CSV files (received after initial spec); supersedes the placeholder schema in spec 05 |

## Quick summary

- **Part 1:** Recreate the reference animation (yellow-green triangle → red hexagon → green circle, with morph + rotation + color + number-scroll transitions).
- **Part 2:** Drive the same animation from two CSV files.
- **Approach:** Build both Plan A (geometric vertex morph, 66 tris) and Plan B (SDF shader, 4 tris) in the same Unity project. Plan B is the recommended deliverable per the "fewer the better" criterion; Plan A is the conservative fallback and a useful comparison piece.
- **State 4:** Open-ended in the brief — interpreted as a 5-pointed star with a pulsing breathing animation on stroke width and color intensity. CSV analysis (spec 09) revealed state 4 is the **terminal / leaving-scene state** for every object that has it, so an alpha fade-out is added to make this semantic visible.
