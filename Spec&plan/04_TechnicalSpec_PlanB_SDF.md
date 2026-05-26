# 04 — Plan B Technical Spec: SDF Shader

## Overview

The shape is rendered on a single quad (2 triangles). The fragment shader computes the signed distance to a parametric shape and shades a thin outline where the distance is close to zero. Shape parameters (vertex count, corner radius, star inset) lerp continuously, producing a mathematically exact morph between any pair of states.

## Mesh

- One quad, 4 vertices, **2 triangles**, sized to fit the largest possible shape in local space
- UV range `[0, 1]` mapped to a normalized shape space `[-1, 1]` in the fragment shader

## Shape parameterization

All four shapes are expressed by three parameters:

| Shape | N (vertex count) | cornerRadius | starInset |
|---|---|---|---|
| Triangle | 3 | 0.04 | 0.0 |
| Hexagon | 6 | 0.04 | 0.0 |
| Circle | 6 (or any N) | 1.0 (max, fully rounded) | 0.0 |
| Star | 5 | 0.04 | 0.5 |

Where:
- **N** = number of corners/points (lerped, fractional values produce in-between polygons)
- **cornerRadius** = `[0, 1]`, how rounded the corners are; at `1.0` corners merge into a circle
- **starInset** = `[0, 1]`, how deep the alternating "valley" radius is between points; `0` = regular polygon, `>0` = star shape

Fractional N values are tricky — a clean lerp from N=3 to N=6 isn't well-defined for a true regular polygon SDF. Two valid approaches:

### Approach 1: Smooth fractional polygon SDF (Inigo Quilez's polygon SDF)

```hlsl
// Regular polygon SDF, smooth in N
float sdRegularPolygon(float2 p, float r, float N) {
    float an = 3.141592 / N;
    float halfAn = an;
    float2 q = float2(abs(p.x), p.y);  // mirror
    float a = atan2(q.x, q.y);
    a = mod(a, 2.0 * an) - an;
    float d = length(q) * cos(a) - r * cos(an);
    return d;
}
```

This is continuous in N but the geometry "rotates" as N changes (because the angular spacing changes). To avoid the rotation artifact we apply an explicit Z-rotation to the sample point to keep the polygon's "top corner up" through the morph.

### Approach 2: Two SDFs, distance-lerp

Compute `sdfA` (current shape) and `sdfB` (next shape) separately, then `lerp(sdfA, sdfB, t)`. This produces a smooth visual morph between any two shapes even if their topology differs (polygon vs. star).

We use **Approach 2** as the primary technique because it cleanly handles the star case (where the "starInset" parameter doesn't really lerp meaningfully through 0 — it's a topological discontinuity). Approach 1 is used when both endpoints are regular polygons.

In practice:
- **Triangle ↔ Hexagon ↔ Circle:** smooth lerp of (N, cornerRadius) — these all share the regular-polygon SDF, so Approach 1 works
- **Anything ↔ Star:** distance-lerp between the rounded-polygon SDF and the star SDF — Approach 2

## Star SDF

Standard 5-pointed star SDF (also from Inigo Quilez's public domain shader library):

```hlsl
float sdStar5(float2 p, float r, float inset) {
    // 5-pointed star with outer radius r, inner valley at r*(1-inset)
    const float2 k1 = float2(0.809016994, -0.587785252);
    const float2 k2 = float2(-0.809016994, -0.587785252);
    p.x = abs(p.x);
    p -= 2.0 * max(dot(k1, p), 0.0) * k1;
    p -= 2.0 * max(dot(k2, p), 0.0) * k2;
    p.x = abs(p.x);
    p.y -= r;
    float2 ba = float2(-inset * 0.5, inset) * r;
    float h = clamp(dot(p, ba) / dot(ba, ba), 0.0, 1.0);
    return length(p - ba * h) * sign(p.y * ba.x - p.x * ba.y);
}
```

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
    _MorphCurrent ("Current Shape", Range(0,3)) = 0     // 0=tri, 1=hex, 2=circle, 3=star
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
- **Mathematically exact** — true circles, exact stars, no polygon approximation
- **Resolution-independent** — looks crisp at any zoom (relevant for VR)
- **State 4 pulsing effect is free** — just modulate `_StrokeWidth` in the shader, no extra geometry
- Shows understanding that "3D elements" doesn't have to mean "geometrically detailed meshes" — the cleverness is in the shader, which is core technical-artist work
