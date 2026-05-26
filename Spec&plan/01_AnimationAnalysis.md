# 01 — Animation Analysis

Detailed read of `Animation.mkv` based on per-frame inspection.

## Source video

- Duration: **10.417 s**
- Frame rate: **24 fps**
- Frame count: **250**
- Resolution: 1920×1080
- Background color: `#444444` (RGB 68,68,68)

## Visual elements

The scene contains a single screen-facing display made of two independently transformed layers:

1. **Shape layer** — an outline-only geometric shape (triangle / hexagon / circle), drawn as a thin stroke. Color tinted, alpha-masked, can rotate around Z.
2. **Number layer** — a digit ("1" / "2" / "3") rendered inside the shape. Color tinted to match the shape. Does **not** rotate with the shape. Slides horizontally (UV scroll) during transitions.

Both layers share the same tint color at all times.

## State table

| State | Time window | Shape | Digit | Color (sampled) | Rotation |
|---|---|---|---|---|---|
| 1 | 0.00 – 2.30 s | Triangle (point up) | 1 | Yellow-green `#C2C541` | 0° |
| 1→2 transition | 2.30 – 4.40 s (~2.1 s) | Triangle morphing to Hexagon | 1 sliding off, 2 sliding on | crossfade yellow-green → red `#AC5654` | rotates ~180° on Z |
| 2 | 4.40 – 7.70 s | Hexagon | 2 | Red/coral `#AC5654` | 0° (held) |
| 2→3 transition | 7.70 – 8.70 s (~1.0 s) | Hexagon morphing to Circle | 2 sliding off, 3 sliding on | red → green `#3BB73D` | minimal rotation (small wobble) |
| 3 | 8.70 – 10.42 s | Circle | 3 | Green `#3BB73D` | 0° |

## Important behavioral notes

- **The shape morphs, it does not crossfade between two textures.** In-between frames show a single continuous outline that bulges out from triangle corners into hexagon edges, or relaxes hexagon corners into a circular curve. There is no double-stroke ghosting, which rules out two-quad alpha blending.
- **The number layer is a horizontal scroll.** Mid-transition you see two adjacent digits ("1 2" or "2 3") sliding past each other, meaning the digits are part of a single horizontal strip sampled through a clipped window (RotationTexture.png, 1024×256, contains "1 2 3 4" laid out horizontally).
- **The number does not rotate with the shape.** Even when the triangle rotates 180°, the "1" stays upright. So the digit is on a different Z-rotation transform than the shape outline.
- **Color crossfades smoothly across the transition window**, not as a step at the end. The animation curve appears to be SmoothStep-like.
- **Transition 2→3 is roughly half the duration of transition 1→2** and has much less rotation. This asymmetry is preserved in the implementation — transitions are not uniform.
- **Rest states are perfectly static.** No idle animation on states 1, 2, or 3.

## Reference textures

| Texture | Size | Content | Purpose |
|---|---|---|---|
| `Triangle.png` | 1024×1024 | White triangle outline on black | Shape reference (used by Plan A only, optional in Plan B) |
| `Hexagon.png` | 1024×1024 | White hexagon outline on black | Shape reference |
| `Circle.png` | 1024×1024 | White circle outline on black | Shape reference |
| `RotationTexture.png` | 1024×256 | Digits "1 2 3 4" horizontally | Number strip, sampled with a U-offset to show one digit at a time |

All outline textures are white-on-black, which means they work cleanly as alpha masks where the white outline becomes opaque and the black field becomes transparent. The number strip is sampled through a clipped UV window so that only one digit is visible per shape at rest, with two adjacent digits visible during a horizontal scroll transition.

## Time references for implementation

These are the keyframe times that the state machine and animator should hit:

- `t = 0.0` — State 1 fully resolved
- `t = 2.3` — Begin transition 1→2
- `t = 4.4` — State 2 fully resolved
- `t = 7.7` — Begin transition 2→3
- `t = 8.7` — State 3 fully resolved
- `t = 10.4` — End of reference clip

Total animation length matches a typical HMI state-transition flow. In Part 2 the actual timing will come from the CSV, not these hardcoded values.
