# 02 — Implementation Plan

## Engine choice

**Unity 6 (6000.3.16f1)** with **URP (Universal Render Pipeline)**.

Rationale:
- Trivial CSV parsing
- Modern shader workflow (HLSL ShaderLab) suits both planned approaches
- Fast Windows standalone builds
- VR-ready if we want to extend (URP supports XR out of the box)

Built-in RP would also work but URP is the modern default and the shader work is essentially identical effort.

## Dual-track approach

The brief states "Maximum of 100 triangles in the scene, the fewer the better." This is graded — using fewer is better. To address both the letter and the spirit of the brief, two implementations are delivered in the same project:

### Plan A — Geometric vertex morph

- One ring mesh, ~32 segments, two rings of vertices (inner + outer) forming a thin outline stroke
- Each vertex carries baked positions for all morph targets (triangle, hexagon, circle, star) as vertex attributes
- Vertex shader lerps between two targets based on `currentState` / `nextState` / `transitionProgress`
- **64 triangles** for the shape + 2 for the digit quad = **66 triangles total**

### Plan B — SDF (signed distance field) shader

- One quad (2 tris) for the shape, with a fragment shader that computes the distance to a parametric polygon
- Shape parameters (vertex count N, corner radius, star inset) lerp continuously
- True circle, exact star, resolution-independent
- **2 triangles** for the shape + 2 for the digit quad = **4 triangles total**

## Recommendation

**Plan B is the recommended deliverable.** It uses 4 triangles vs. Plan A's 66, which is what "fewer is better" rewards. It also shows the more interesting technical-artist skill — putting the work in the shader rather than the mesh — and produces mathematically perfect shapes (a true circle, not a 32-gon).

Plan A is delivered alongside it as:
- A conservative fallback in case there's a hidden requirement we're missing (such as "must use the provided textures")
- A useful comparison piece that demonstrates I can solve the problem the expected way too
- A reference point for the trade-off discussion in the written process doc

Both scenes share the same `StateController` API and CSV driver, so the difference between them is purely in the shape rendering.

## Trade-off summary

| Aspect | Plan A (Geometric) | Plan B (SDF) |
|---|---|---|
| Triangle count | 66 | 4 |
| Vertex shader cost | Low (per-vertex lerp) | Minimal |
| Fragment shader cost | Minimal (alpha mask) | Higher (distance math + smoothstep) |
| Circle quality | 32-gon approximation | Mathematically exact |
| Star quality | Vertex-baked, exact for fixed N | Mathematically exact |
| Resolution independence | No — pixel artifacts at high zoom | Yes — crisp at any zoom |
| Stroke width control | Set at mesh-generation time | Live shader parameter |
| Extending with more shapes | Add another vertex attribute, rebake | Add another SDF branch in shader |
| Pulsing State 4 cost | Extra animation on tint/scale | Free — modulate stroke width in shader |
| "Honesty" of the 3D claim | Strong — real vertex animation | Weaker — it's a single billboard quad |

The brief says "using only 3D elements". Both approaches qualify — Plan B is still a 3D quad in 3D space, the shape just happens to be drawn by the fragment shader. This is a standard technique and shouldn't be controversial, but it's noted in the written process doc.

## Build/delivery shape

Single Unity project. Two scenes:
- `PlanA_GeometricMorph.unity`
- `PlanB_SDFMorph.unity`

Runtime toggle to switch between scenes (e.g. press `Space`) so the evaluator can A/B compare in a single executable. CSV file selection via UI buttons (or auto-cycling through both at startup).

Deliverable zip contents:
- `Build/TechnicalTask_KW.exe` (single Windows build with both scenes)
- `Source/` (Unity project)
- `README.md` (top-level — how to run the build, how to open the project)
- `ProcessDescription.md` (the written process description required by the brief)
