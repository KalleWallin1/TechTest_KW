# 08 — Open Questions / TBD

Things that need to be resolved during implementation or confirmed before final delivery.

## Blocked on input

- ~~**CSV format and content**~~ — **RESOLVED 2026-05-26.** Both CSVs received and analyzed. See `09_CSVAnalysis.md` for the full schema, state-semantic analysis, and updated CSVDriver design.
- **Whether to use the provided textures (Triangle/Hexagon/Circle.png) at all** — confirmed during planning that they can be used as reference only. Plan B uses none of them; Plan A optionally uses them as alpha masks but the spec defaults to pure-geometric.

## Decisions to make during implementation

- **Number layer rendering** — confirmed from inspection: `RotationTexture.png` is 1024×256 with "1 2 3 4" laid out horizontally. All four digits including State 4's "4" are already present, so no texture modification is needed.
- **N segments for Plan A** — 32 is the default but could drop to 24 if visual quality is acceptable, saving 16 triangles. Will test once running.
- **Whether SceneSwitcher creates a single build with both scenes (preferred) or two separate builds.** Default: single build, `Space` toggles scenes.
- **Camera setup** — orthographic vs. perspective. The reference clip looks perspective-flat (no parallax visible), so either works. Orthographic is simpler and avoids any perspective distortion issues; perspective is needed if we want VR. Default: orthographic for non-VR build, perspective for any VR-enabled build.

## Potential issues to validate

- **Plan A: stroke width at the star points** — at sharp angles, the inner ring vertex can cross the outer ring if the stroke is too wide, producing self-intersection artifacts. Either limit stroke width or apply a per-corner miter-clamp at mesh generation time.
- **Plan B: fractional N polygon SDF** — needs visual validation that the in-between shapes (e.g. N=4.5) look reasonable during the transition. If they look weird, switch to the distance-lerp approach (Approach 2 from `04_TechnicalSpec_PlanB_SDF.md`).
- **Plan B: rotating SDF inside a non-rotated quad** — at large rotation angles the shape might clip if the quad is sized too tight. Quad size needs to accommodate the diagonal of the largest shape's bounding box. Solution: size the quad to `sqrt(2) × shape_diameter`.
- **Linear vs gamma color space** — make sure the tint colors look the same as the reference. The sampled RGB values from the reference are in sRGB; they need to be set as sRGB on the materials. URP defaults to linear which handles this if the colors are entered correctly.

## Stretch goals (if time permits)

- VR build using OpenXR
- Free SDF glow halo around state 4
- Audio cues on state transitions (subtle blips at each state change)
- A "play all states" demo mode that visits each state in sequence with consistent timing, as an alternative to CSV playback for visualization

## Already decided (locked)

- Engine: Unity 6 (6000.3.16f1), URP
- Two scenes: Plan A geometric, Plan B SDF
- State 4: 5-pointed star with pulsing breathing animation, blue/cyan color
- Single Windows build with scene switcher
- Background: `#444444`
- Number digits as horizontal UV-scroll on the RotationTexture
- Rotation per transition is configurable per state pair (not uniform across transitions)
- Smoothstep easing on all transitions
- **State 4 = terminal "leaving the scene" state** with alpha fade-out (confirmed from CSV analysis — see spec 09)
- **CSV state value is target state**, not visible state — `StateController.GoToState` is invoked the instant the CSV state value changes, and the visible transition is the easing curve playing out
