# 06 — State 4 Design

The brief says State 4 is "open to interpretation". The interpretation chosen is a **square with a pulsing breathing animation** — extending the triangle/hexagon/circle sequence with a fourth regular polygon, plus a subtle "alive" indicator that distinguishes State 4 from the three static rest states.

## Decision: square over 5-pointed star

An earlier draft of this spec proposed a 5-pointed star for State 4. That has been changed to a **square** for one key reason: implementation budget.

- A star requires either a dedicated SDF (`sdStar5`) and an Approach-2 distance-lerp between two different SDF families, or — in Plan A — a different perimeter-sampling algorithm than the regular polygons (alternating outer/inner radii, stroke pinch at sharp points).
- A square is just a regular polygon at `N = 4`. It reuses **every** piece of machinery already in place for triangle/hexagon/circle: same vertex attribute layout, same perimeter sampler, same SDF function, same parameter lerp.
- That means the State 4 implementation cost in both plans drops to "set N=4, set the tint, enable the pulse" — essentially zero new code.

The aesthetic outcome is also clean: the four states now form a tidy 3 → 4 → 6 → ∞ progression in N (triangle, square, hexagon, circle), which is a natural visual sequence in its own right.

## Visual concept

- **Shape:** square, outline-only, same stroke style as the other states
- **Color:** a fourth distinct color to extend the yellow-green / red / green palette. Chosen as **blue/cyan `#3BC5C7`** to round out a natural four-color progression (warm yellow → warm red → cool green → cool blue). Symmetric with the existing palette in saturation/lightness.
- **Pulse:** continuous sinusoidal animation while State 4 is active:
  - Stroke width modulated: `stroke = baseStroke * (1 + 0.25 * sin(time * 2π * 0.5))` — half-Hz breathing
  - Color intensity slightly modulated in sync: `tint.rgb *= (0.85 + 0.15 * sin(time * 2π * 0.5))`
  - The square itself doesn't rotate continuously — the pulse is the "alive" indicator
- **Digit:** "4", same number-strip layer

## Why pulsing

The first three states are all static at rest — only their transitions are animated. State 4 being the "creative" state, having it be continuously animated (rather than just statically different) makes it read as conceptually different from the others. It feels like a "live" or "active" state, which is a natural HMI semantic (think: warning indicator, recording-in-progress dot, etc.).

The pulse is intentionally subtle (25% stroke width swing, 15% brightness swing, half-Hz so it's a slow breathing cadence rather than a panicked flash). It should feel calm and intentional, not alarming.

Combined with the CSV analysis (`09_CSVAnalysis.md`) finding that State 4 is the **terminal / leaving-scene state**, the pulse reads as a "farewell breath" — the object is on its way out, and the alpha-fade overlay (see below) makes that visually explicit.

## Implementation

### Plan A (geometric)

- The fourth vertex attribute (`TEXCOORD4`) holds the **square target position** at `N = 4` — same perimeter-sampling code as triangle and hexagon, just with four corner arcs and four edges. No special star-point handling, no inner/outer alternation.
- Pulse: in the vertex shader, displace outer-ring vertices outward by `_PulseAmount * sin(_Time * π) * stroke` and inner-ring inward by the same, growing/shrinking the stroke width specifically. Cleaner than scaling the whole transform.

### Plan B (SDF)

- `N = 4` is plugged directly into the same `sdRegularPolygon` function used for the other states. No `sdStar5`, no `starInset`, no Approach-2 distance-lerp.
- Pulse: modulate `_StrokeWidth` directly in the fragment shader, based on `_Time` and `_PulseAmount`.
- Color modulation done either on CPU side (StateController writes the tint each frame) or in shader — equivalent; CPU side keeps the shader simple.

## Transition into State 4

The transition into State 4 (typically from State 3, circle) is:
- Color: green → blue/cyan
- Shape: circle → square (corners sharpen out of the round)
- Rotation: optionally +45° so the square sits as a diamond on its corner, then settles back to flat-side-down — gives the transition a bit more visual energy. Or 0° if a square-on-edge is preferred for stability.
- Pulse amount: 0 → 1 across the transition window
- Digit: 3 → 4

Visually this should still be a meaningful transition — circle to square is a topology change (∞ → 4 corners) — but it's less dramatic than the dropped circle-to-star would have been. The pulse + color shift + fade-out together carry the State 4 weight.

## Alpha fade-out (from CSV analysis)

Per `09_CSVAnalysis.md`, State 4 is when an object is leaving the scene. The fade-out behavior:

- On entry to State 4, count the remaining rows of state 4 the object has — that's the fade duration.
- Alpha lerps from 1 → 0 across that window.
- Last known XYZ position is held (CSV has empty XYZ for all state-4 rows).
- When the last state-4 row passes, the object is removed (or returned to pool in long-data mode).

This costs zero extra triangles in either plan.

## Out-of-scope flourishes

Not doing (kept the scope tight, but easy to add later):
- A halo/glow ring around the square (would add tris in Plan A; could be a free SDF effect in Plan B)
- Particle effects at the corners
- An audio cue
- Continuous rotation on the square itself (would compete visually with the pulse)

If time allows after the core demo is solid, Plan B could gain a free SDF-based glow halo (just sample the SDF at a larger threshold and add a soft falloff). This stays at 4 triangles and would only add a small fragment-shader cost.
