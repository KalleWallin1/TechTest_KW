# 06 — State 4 Design

The brief says State 4 is "open to interpretation". The interpretation chosen is a **5-pointed star with a pulsing breathing animation** — combining the two preferred options discussed during planning.

## Visual concept

- **Shape:** 5-pointed star, outline-only, same stroke style as the other states
- **Color:** a fourth distinct color to extend the yellow-green / red / green palette. Chosen as **blue/cyan `#3BC5C7`** to round out a natural four-color progression (warm yellow → warm red → cool green → cool blue). This is symmetric with the existing palette saturation/lightness range.
- **Pulse:** continuous sinusoidal animation while State 4 is active:
  - Stroke width modulated: `stroke = baseStroke * (1 + 0.25 * sin(time * 2π * 0.5))` — half-Hz breathing
  - Color intensity slightly modulated in sync: `tint.rgb *= (0.85 + 0.15 * sin(time * 2π * 0.5))`
  - The star itself doesn't rotate continuously — the pulse is the "alive" indicator
- **Digit:** "4", same number-strip layer

## Why pulsing

The first three states are all static at rest — only their transitions are animated. State 4 being the "creative" state, having it be continuously animated (rather than just statically different) makes it read as conceptually different from the others. It feels like a "live" or "active" state, which is a natural HMI semantic (think: warning indicator, recording-in-progress dot, etc.).

The pulse is intentionally subtle (25% stroke width swing, 15% brightness swing, half-Hz so it's a slow breathing cadence rather than a panicked flash). It should feel calm and intentional, not alarming.

## Implementation

### Plan A (geometric)

- Add a fifth shape preset (after star) — but we only need 4. Star is included in the baked vertex attributes.
- Wire stroke-width pulsing in the shader: take a `_PulseAmount` (0–1) and `_Time` and modulate the inner/outer ring expansion in the vertex shader.
- Actually simpler: keep the mesh fixed, scale the entire transform's `localScale.xy` with the pulse. Stroke width effectively widens because the whole mesh scales up — close enough visually for this scale.
  - Better: in the vertex shader, displace outer ring vertices outward by `pulse * sin(time)` and inner ring inward, growing the stroke specifically. Cleaner result.

### Plan B (SDF)

- Just modulate `_StrokeWidth` directly in the shader based on `_Time` and `_PulseAmount`.
- Color modulation done on CPU side (StateController) or in shader — equivalent.
- Star SDF used when `currentState == 3` or `nextState == 3`, blended with the polygon SDF based on transition T.

## Transition into State 4

The transition into State 4 (most likely from State 3, circle) is:
- Color: green → blue/cyan
- Shape: circle → star (5 points sprouting outward)
- Rotation: optionally +180° (matches the "big" transition style of 1→2)
- Pulse amount: 0 → 1 across the transition window
- Digit: 3 → 4

Visually this should be the most dramatic transition in the sequence — circle to star is a topologically bigger change than triangle-to-hexagon.

## Out-of-scope flourishes

Not doing (kept the scope tight, but easy to add later):
- A halo/glow ring around the star (would add tris in Plan A; could be a free SDF effect in Plan B)
- Particle effects on the star points
- An audio cue
- Rotation on the star itself (would compete visually with the pulse)

If time allows after the core demo is solid, Plan B could gain a free SDF-based glow halo (just sample the SDF at a larger threshold and add a soft falloff). This stays at 4 triangles and would only add a small fragment-shader cost.
