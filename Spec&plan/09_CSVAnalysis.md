# 09 — CSV Data Analysis (received 2026-05-26)

This addendum supersedes the placeholder schema in `05_StateController_and_CSV.md` now that the actual CSVs are in hand.

## Schema (both files)

```
frame, object_id, x, y, z, state
```

- `frame` — integer, 1-indexed
- `object_id` — integer, object identifier
- `x, y, z` — float, world-space position. **May be empty** (object position lost / object leaving the scene)
- `state` — integer, 1-4

## Short_Data_Animation_Match.csv

- **250 rows**, single object (`object_id = 1`), frame range 1–250
- Frame count matches the 250-frame reference video exactly at 24 fps
- **This file is the CSV form of the reference animation** — replaying it should reproduce the reference

### State transitions

| Frame | Time | State change |
|---|---|---|
| 1 | 0.04 s | start in state 1 |
| 43 | 1.79 s | 1 → 2 |
| 181 | 7.54 s | 2 → 3 |
| 231 | 9.62 s | 3 → 4 |
| (250) | 10.42 s | end |

### Reconciling CSV state values with visible video timing

The CSV state value changes a few frames *before* the visible transition begins in the reference. Comparing CSV state changes to my frame-by-frame video analysis:

| CSV state change | Visible transition begins | Lag |
|---|---|---|
| Frame 43 (1→2) | ~Frame 55 | ~0.5 s |
| Frame 181 (2→3) | ~Frame 185 | ~0.17 s |

**Interpretation:** the CSV state value is the *target* state — the value the system is now headed toward. The renderer interpolates smoothly toward that target. The "lag" is the easing curve playing out, not a deliberate delay. This matches the `StateController` design already in the plan: `GoToState(newState, transitionDuration)` is invoked the instant the CSV state value changes, and the smoothstep curve produces the gentle visible ramp.

### Position data behavior

- XYZ ranges roughly `X: [-2, 23]`, `Y: [-3, 19]`, `Z: [1, 82]`
- The object starts at ~`(22, 18, 25)` (state 1), drifts toward origin during the 1→2 transition, holds near `(2, 0, 3)` during state 2 (~5.5 s), then **shoots up Z from ~3 to ~82** during state 3
- **The last 25 frames (226–250) have empty XYZ** — covering the tail of state 3 (frames 226–230) and all of state 4 (frames 231–250). Empty XYZ correlates with the object leaving the scene.
- Position data is noisy at the rest moments (visible jitter in X/Y/Z plots) — looks like real sensor data, not hand-keyframed values

### State 4 in this file

- 20 rows long (frames 231–250)
- **All 20 rows have empty XYZ**
- The state-4 segment of the short data is **not visible in the reference video** (the video ends at state 3) — meaning State 4 is a creative addition the assessment expects the implementer to design and showcase

## Long_Data_Free_Form.csv

- **12,682 rows**, **22 objects**, frame range 16–5000 (≈208 s at 24 fps)
- Each object exists for a window of frames, then disappears
- Objects overlap in time — typical 3–4 objects active simultaneously
- World extent: `X: [-82, 107]`, `Y: [-98, 103]`, `Z: [5, 74]` — much larger than the short file's extent

### State 4 pattern across all objects

Eleven of the 22 objects end in state 4. Every one of those:
- Has state 4 appear in the final 80–99 % of its lifespan (never mid-life)
- Has empty XYZ for **every** state-4 row
- Disappears from the dataset immediately after the state-4 segment ends

This is statistically conclusive:

> **State 4 = "object is leaving the scene / track is being lost".**
> An object enters state 4 just before it stops being tracked. Its last known position is wherever it was at the last non-empty XYZ row.

This is exactly the kind of "exit / deactivation / end-of-life" code that real-world tracking systems emit (radar, vision, lidar pipelines all tend to have one).

### Implications for the State 4 design

The pentagram + pulsing effect from `06_State4_Design.md` reads as a "farewell" animation in this context — the object's final visual identity before it vanishes. To strengthen the read:
- During state 4, optionally fade alpha to zero across the duration of the state-4 segment, so the object visually dissolves out of the scene as it ends
- The last known position can be held (since we have no XYZ during state 4) — the object stays in place and breathes out
- Alternatively: shrink the scale toward zero alongside the pulse, so the breath visually "exhales"

This adds nothing to the triangle count (free in both Plan A and Plan B).

## Updated Part 2 architecture

### `CSVDriver` redesign

```csharp
public class CSVDriver : MonoBehaviour {
    public TextAsset shortDataCsv;
    public TextAsset longDataCsv;

    [Header("Object spawning")]
    public GameObject shapeMorphPrefab;   // PlanA or PlanB variant per scene
    public Transform  worldRoot;          // parent for spawned objects

    [Header("Playback")]
    public DataSource activeSource = DataSource.Short;
    public float      playbackRate = 1.0f;
    public bool       loop = true;
    public float      transitionDuration = 1.0f;  // how long state interpolations take
}
```

Two playback modes are required because the two files are structured differently:

#### Short data mode

- One persistent shape object (no spawn/destroy)
- Drives the object's transform.position from CSV XYZ (interpolated between frames)
- Drives the object's StateController from the CSV state column
- When XYZ goes empty: hold last position
- When playhead reaches frame 250 and `loop`: reset and replay

#### Long data mode

- Maintains a pool of shape objects keyed by `object_id`
- On the frame an object's first row appears: spawn a new shape object (or take one from the pool), set its initial state and position
- Each frame, for each currently-active object_id: update position from XYZ (if non-empty), update state from CSV state value
- When an object's last frame passes: despawn the object (or return to pool)
- All running concurrently — could have many shape objects on screen at once

This makes the long data the "real" VR experience — a 3.5-minute scene of objects flowing through space with state-driven visual identities.

### Coordinate system

The CSV XYZ values need scaling and possibly axis remapping to fit a Unity world space comfortable for VR viewing.

- Short data: X 0–23, Y -3–19, Z 1–82. Mostly comfortable in meters but Z range of 82 is far. Consider scale factor 0.1–0.2 to bring everything into a 5–10 m room-scale volume.
- Long data: much larger extents (180 m in X, 200 m in Y). At Unity-default 1 unit = 1 m, this would be a city block. For non-VR viewing this is fine with a wide-view camera. For VR, apply a scale factor (e.g. 0.05) so the whole scene fits inside ~10 m around the viewer.

A `worldScale` parameter on `CSVDriver` controls this. Default values: `1.0` for desktop, `0.05` for VR.

### Frame-to-time mapping

- CSV `frame` field is a frame number at 24 fps
- Playback advances time at `Time.deltaTime * playbackRate`
- For each active object, find the rows whose frame straddle the current playhead time and lerp position between them
- State change is a step (not interpolated) — but the `StateController.GoToState` call triggers a smooth visual transition over `transitionDuration`

### State 4 fade-out behavior

When an object's state transitions to 4:
- The StateController switches to state 4 (star + pulse + blue tint)
- An additional `lifeRemaining` is computed: count how many CSV rows of state 4 the object has left
- Alpha fades from 1 → 0 over the duration of the state-4 window
- When the last state-4 row passes, the object is removed (or pooled)

This visually communicates the "leaving the scene" semantic without needing the user to read documentation.

## Empty XYZ handling

Three places empty XYZ can occur:
- During state 3 (e.g. short data frames 226–230 — the tail of state 3 just before state 4 begins). Treatment: hold last known position; the object is between active tracking and exit.
- During state 4 — always empty across both files. Treatment: hold last known position, run the state-4 farewell animation.
- Theoretically during any state — unlikely but the parser should handle gracefully. Treatment: hold last known position.

## Decisions locked from this analysis

1. **CSV state value is target state, not visible state.** The visible animation interpolates toward it via `StateController.GoToState`.
2. **State 4 is terminal / exit / leaving-scene.** Pentagram + pulse design from spec 06 is unchanged; alpha fade-out added.
3. **Empty XYZ = hold last position.** Never tries to extrapolate or move to origin.
4. **Long data is multi-object** with spawn/despawn keyed on `object_id` lifespan. Object pool keeps allocation cost low.
5. **World scale** is a `CSVDriver` parameter, defaulting to suit non-VR; smaller value for VR.
6. **Transition duration** is configurable; from the short data analysis it should default to about 1.0–1.5 s, which produces the right visual feel relative to the reference video.

## Per-triangle-budget note for multi-object

In the long-data scene there can be many objects on-screen at once (the timeline plot showed up to ~5 overlapping). Triangle budget per object:

- Plan A: 66 tris × ~5 = ~330 tris (clearly over the 100-tri limit if the limit applies to the whole scene)
- Plan B: 4 tris × ~5 = 20 tris (well under)

The brief's 100-tri limit is most naturally interpreted as applying to **a single instance of the animation** (Part 1), not necessarily to the multi-object Part 2 scene. But this is **another reason Plan B is the better default** — it remains under budget even when the scene becomes busy. In the written process doc this can be highlighted: the SDF approach scales cleanly to multi-object scenes where geometric morphing would blow past the limit.

If a strict interpretation is needed for the long-data scene (single-instance budget), I will:
- Use Plan B for the long-data scene (it always fits)
- Note in the readme that Plan A is suitable for Part 1 / short-data only

## Updated file list (since spec 07)

Add:
- `Assets/Scripts/CSVDriver.cs` (updated to support both modes)
- `Assets/Scripts/CSVObjectPool.cs` (object pool for long-data multi-object spawning)
- `Assets/Scripts/CSVObjectController.cs` (drives a single shape object from a CSV stream — position interpolation, state forwarding, alpha fade on state 4)
- `Assets/StreamingAssets/Short_Data_Animation_Match.csv`
- `Assets/StreamingAssets/Long_Data_Free_Form.csv`

Open question now resolved (from spec 08): CSV format is known. The previous "TBD" entries about CSV layout are closed.
