# 04 — Plan B Technical Spec: SDF Shader

## Overview

The shape is rendered on a single quad (2 triangles). The fragment shader computes the signed distance to a parametric shape and shades a thin outline where the distance is close to zero. All four states (triangle, hexagon, circle, square) are on the same regular-polygon continuum — only `N` (vertex count) and `cornerRadius` change between them — so a single SDF function handles every shape and every transition with a clean parameter lerp.

## Mesh

- One quad, 4 vertices, **2 triangles**, sized to fit the largest possible shape in local space
- UV range `[0, 1]` mapped to a normalized shape space `[-1, 1]` in the fragment shader

## Shape parameterization

All four shapes are expressed by **two parameters** — no special-case SDFs needed since the square is also a regular polygon:

| Shape | N (vertex count) | cornerRadius |
|---|---|---|
| Triangle | 3 | 0.04 |
| Square   | 4 | 0.04 |
| Hexagon  | 6 | 0.04 |
| Circle   | any N (e.g. 6) | 1.0 (max — corners merge into a smooth circle) |

Where:
- **N** = number of corners (lerped; fractional values produce in-between polygons during a transition)
- **cornerRadius** = `[0, 1]`, how rounded the corners are; at `1.0` corners merge into a circle

The decision to use a square as State 4 (instead of a 5-pointed star) collapses what would have been two separate SDF families (regular polygon + star) into a single regular-polygon SDF. This is a meaningful simplification: no `starInset` parameter, no `sdStar5` function, no Approach-2 distance-lerp for cross-family transitions. Every state ↔ every state transition is a smooth `(N, cornerRadius)` lerp.

### Regular polygon SDF (Inigo Quilez)

```hlsl
// Regular polygon SDF, smooth in N
float sdRegularPolygon(float2 p, float r, float N) {
    float an = 3.141592 / N;
    float2 q = float2(abs(p.x), p.y);   // mirror over y-axis
    float a = atan2(q.x, q.y);
    a = mod(a, 2.0 * an) - an;
    float d = length(q) * cos(a) - r * cos(an);
    return d;
}
```

### Fractional N behavior

When N is between two integer values (e.g. 3.5 during a triangle→square transition), the SDF still produces a continuous closed shape — it just doesn't correspond to a "real" polygon. The shape visually morphs smoothly: corners migrate around the perimeter, growing in count, with the radial frequency increasing as N rises. This reads as a natural morph.

The polygon's "top corner up" orientation rotates slightly as N changes (because the angular spacing depends on N). To keep the shape stable through the morph, an explicit Z-rotation is applied to the sample point that compensates — effectively `rotate(uv, π/N)` so a flat side sits at the bottom regardless of N.

## Outline rendering

Given the signed distance `d` to the shape:

```hlsl
float strokeHalfWidth = _StrokeWidth * 0.5;
float aaWidth = fwidth(d);  // anti-aliasing width (1 pixel in screen space)
float outline = 1.0 - smoothstep(strokeHalfWidth - aaWidth, strokeHalfWidth + aaWidth, abs(d));
return half4(_Color.rgb, _Color.a * outline);
```

This produces a crisp, anti-aliased outline at any zoom level. `fwidth(d)` gives us screen-space pixel size so the antialiasing is always exactly 1 pixel.

## Shader properties

```
Properties {
    _Color ("Tint", Color) = (1,1,1,1)
    _StrokeWidth ("Stroke Width", Range(0.005, 0.1)) = 0.02
    _MorphCurrent ("Current Shape", Range(0,3)) = 0     // 0=tri, 1=hex, 2=circle, 3=square
    _MorphNext ("Next Shape", Range(0,3)) = 0
    _MorphT ("Transition T", Range(0,1)) = 0
    _PulseAmount ("Pulse Amount", Range(0,1)) = 0       // for State 4 breathing effect
    _Time2 ("Time", Float) = 0
}
```

`_PulseAmount` controls the strength of a sinusoidal stroke-width modulation used by State 4 (see `06_State4_Design.md`).

## Triangle budget

- Shape quad: **2 triangles**
- Number quad: **2 triangles**
- **Total: 4 triangles**

## Rotation handling

Z rotation is applied to the sample point in the fragment shader (rotating UV by a `_Rotation` float property) rather than transforming the quad, so the bounding quad doesn't need to grow to accommodate rotated shapes. The visible shape rotates within a fixed quad.

```hlsl
float2 RotateUV(float2 uv, float angleRad) {
    float c = cos(angleRad), s = sin(angleRad);
    return float2(c*uv.x - s*uv.y, s*uv.x + c*uv.y);
}
```

## Files

- `Assets/Shaders/ShapeMorphSDF.shader`
- `Assets/Scripts/ShapeMorphSDFController.cs` — drives the same `_MorphCurrent`, `_MorphNext`, `_MorphT`, `_Color` properties as Plan A
- `Assets/Prefabs/ShapeMorph_PlanB.prefab`
- `Assets/Scenes/PlanB_SDFMorph.unity`

## Why this is the right answer to the brief

- **4 triangles** is dramatic improvement over Plan A's 66 — directly maximizes the "fewer the better" criterion
- **Mathematically exact** — true circles, exact regular polygons at any integer N, no jagged approximation
- **Resolution-independent** — looks crisp at any zoom (relevant for VR)
- **State 4 pulsing effect is free** — just modulate `_StrokeWidth` in the shader, no extra geometry
- **Single SDF family** covers all four states — the square decision means there is no special-case shape, every transition is one continuous parameter lerp
- Shows understanding that "3D elements" doesn't have to mean "geometrically detailed meshes" — the cleverness is in the shader, which is core technical-artist work
