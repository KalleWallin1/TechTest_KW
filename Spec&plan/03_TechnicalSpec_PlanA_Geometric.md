# 03 — Plan A Technical Spec: Geometric Vertex Morph

## Overview

The shape is a single procedurally generated ring mesh. Each vertex carries baked positions for every morph target. The vertex shader lerps between the current target and the next target based on a transition parameter, producing continuous geometric deformation.

## Mesh topology

- **Ring of N segments**, default `N = 32`
- Two rings of vertices:
  - **Outer ring** at radius `R + strokeHalfWidth`
  - **Inner ring** at radius `R - strokeHalfWidth`
- Connected as a closed quad strip → forms a thin outline ring
- Vertex count: `2N = 64`
- Triangle count: `2N = 64` (each quad = 2 triangles)

`N` is a parameter and can be reduced if needed for budget. At `N = 32` the circle is smooth enough to be visually convincing and the star has clean enough points. At `N = 24` it would be 48 tris and still acceptable.

## Vertex attributes (baked at mesh generation)

| Attribute | Purpose |
|---|---|
| `POSITION` | Current displayed position (initialized to triangle, overwritten each frame by the shader from the four target attributes) |
| `TEXCOORD0` | UV (for any masking, currently unused but reserved) |
| `TEXCOORD1` (float2) | Triangle target position |
| `TEXCOORD2` (float2) | Hexagon target position |
| `TEXCOORD3` (float2) | Circle target position |
| `TEXCOORD4` (float2) | Star target position |
| `COLOR` | 1.0 for outer ring verts, 0.0 for inner ring verts (used to extrude the stroke in the vertex shader, optional) |

Storing all four targets as vertex attributes means the shader doesn't need to read from constant buffers indexed by state — the lerp math is straight per-vertex linear algebra. Memory overhead is trivial at this triangle count.

## Generating target positions

For each segment index `i ∈ [0, N)`, parameter `t = i / N` (range `[0, 1)`):

### Triangle (N=3 corners)
- For a regular triangle with rounded corners, sample the perimeter at parameter `t`
- Implementation: subdivide the perimeter into 3 corner arcs (small radius) and 3 straight edges; place vertex `i` at the correct arc-length along this perimeter

### Hexagon (N=6 corners)
- Same approach, 6 corner arcs and 6 edges
- The white outline in `Hexagon.png` has visibly rounded corners, so corner-radius ≈ 4% of the shape's bounding radius

### Circle
- `(cos(2π·t), sin(2π·t)) * R`
- The simplest target

### Star (5-pointed)
- Alternate between outer radius `R` and inner radius `R * 0.5` over 10 vertices (5 outer points, 5 inner valleys)
- Sample at parameter `t` along the star's perimeter (with optional rounded corners for visual softness)

For each of these, the same `i → position` mapping is used per ring, with the outer ring at `R+stroke/2` and the inner ring at `R-stroke/2` along the **outward normal of the shape at that point**. So the stroke follows the shape's contour rather than being a simple radial offset (which would distort at sharp corners).

## Shader

`Shaders/ShapeMorphGeometric.shader`

```hlsl
Properties {
    _Color ("Tint", Color) = (1,1,1,1)
    _MorphCurrent ("Current Shape Index", Range(0,3)) = 0   // 0=tri, 1=hex, 2=circle, 3=star
    _MorphNext ("Next Shape Index", Range(0,3)) = 0
    _MorphT ("Transition Progress", Range(0,1)) = 0          // 0=current, 1=next
}
```

Vertex shader logic (pseudo-HLSL):

```hlsl
float2 GetTargetPos(int idx, float2 tri, float2 hex, float2 cir, float2 sta) {
    if (idx == 0) return tri;
    if (idx == 1) return hex;
    if (idx == 2) return cir;
    return sta;
}

v2f vert(appdata v) {
    float2 a = GetTargetPos((int)_MorphCurrent, v.tri, v.hex, v.cir, v.sta);
    float2 b = GetTargetPos((int)_MorphNext,    v.tri, v.hex, v.cir, v.sta);
    float t = smoothstep(0, 1, _MorphT);  // smooth easing
    float2 p = lerp(a, b, t);
    o.pos = UnityObjectToClipPos(float4(p, 0, 1));
    o.col = _Color;
    return o;
}
```

Fragment shader: solid color output, no texture sampling.

```hlsl
half4 frag(v2f i) : SV_Target {
    return half4(i.col.rgb, i.col.a);
}
```

## Rotation handling

Z rotation is applied at the GameObject Transform level, not in the shader. The `StateController` lerps `transform.localEulerAngles.z` from current to next over the transition window.

Per-transition rotation amount is configurable per state pair (since the reference shows 1→2 rotates ~180° but 2→3 barely rotates).

## Triangle budget

- Shape mesh: **64 triangles** (32 segments × 2 quads × 1 tri each — actually 32 segments × 2 tris per quad = 64)
- Number quad: **2 triangles**
- **Total: 66 triangles**

If we use `N = 24` segments: 48 + 2 = 50 triangles total. Still well within budget.

## Files

- `Assets/Scripts/MorphMeshGenerator.cs` — runs at edit-time (or first Awake) to generate the mesh with all four target attributes baked in
- `Assets/Scripts/ShapeMorphController.cs` — drives the material's `_MorphCurrent`, `_MorphNext`, `_MorphT` properties from `StateController`
- `Assets/Shaders/ShapeMorphGeometric.shader`
- `Assets/Prefabs/ShapeMorph_PlanA.prefab`
- `Assets/Scenes/PlanA_GeometricMorph.unity`
