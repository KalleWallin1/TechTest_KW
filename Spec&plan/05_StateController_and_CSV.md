# 05 — StateController and CSV Driving

## StateController API

The state machine is shared between Plan A and Plan B. The same script drives either shader.

```csharp
public class StateController : MonoBehaviour {
    [System.Serializable]
    public struct StatePreset {
        public int      shapeIndex;     // 0=tri, 1=hex, 2=circle, 3=square
        public int      digitIndex;     // 0=1, 1=2, 2=3, 3=4
        public Color    tint;
        public float    rotationDegrees;  // Z rotation at rest
        public bool     pulsing;        // State 4 only
    }

    public StatePreset[] presets;       // configured in inspector, length 4

    public int   CurrentState { get; private set; }
    public int   NextState    { get; private set; }
    public float TransitionT  { get; private set; }  // 0..1

    public void GoToState(int newState, float transitionDuration);
    public void SetStateImmediate(int newState);
}
```

The controller exposes shape index, digit index, color, rotation, and pulse parameters that the shape and number layers read every frame. It does not know about the shader — it just sets material properties on the shape material and number material.

### Behaviors during transition

| Parameter | Source A | Source B | Blend |
|---|---|---|---|
| Shape index | `presets[current].shapeIndex` | `presets[next].shapeIndex` | Both passed to shader, lerped by T |
| Color | `presets[current].tint` | `presets[next].tint` | Lerp by smoothstep(T) on CPU, single tint sent to shader |
| Z rotation | `presets[current].rotationDegrees` | `presets[next].rotationDegrees + rotationDelta` | Lerped by smoothstep(T) on CPU, set on Transform |
| Digit U offset | digit `current` | digit `next` | Lerped by T, sent to number shader as U offset |
| Pulse amount | `presets[current].pulsing ? 1 : 0` | `presets[next].pulsing ? 1 : 0` | Lerped by T |

### Per-transition configurable rotation

Each transition can override the rotation delta. Default behavior:
- 1→2: +180° (visible in reference)
- 2→3: 0° (visible in reference — barely any rotation)
- 3→4: +180° (matches the "big" transition feel for State 4)
- Any other pair: smart default of (next - current) * 60° or similar

These are configured as a 2D array `transitionRotations[current, next]` in the inspector.

### Easing

All blends use `smoothstep(0, 1, t)` for the visual T value, which produces an S-curve that matches the perceived ease-in/ease-out in the reference animation.

## CSV format

> **Status:** confirmed from the files received 2026-05-26. Schema and per-file analysis live in **`09_CSVAnalysis.md`** — that document is the source of truth. This section is a short summary; full state-semantic analysis (including the State 4 = "leaving scene" finding) is in spec 09.

Both files share the same six-column schema:

```
frame, object_id, x, y, z, state
```

- `frame` — integer, 1-indexed, 24 fps
- `object_id` — integer, used in long data to identify the persistent object across frames
- `x, y, z` — float world-space position; **may be empty** (object leaving scene / lost tracking)
- `state` — integer 1–4, **target state** for the renderer

### `Short_Data_Animation_Match.csv`

- 250 rows, single object (`object_id = 1`), one row per video frame
- This file is the CSV form of the reference animation — replaying it reproduces the reference
- State transitions: frame 43 (1→2), frame 181 (2→3), frame 231 (3→4)
- Frames 226–250 have empty XYZ (object leaving scene)

### `Long_Data_Free_Form.csv`

- 12,682 rows, 22 objects, frame range 16–5000 (~208 s)
- Multi-object: 3–4 objects active simultaneously at peak
- 11 of 22 objects end in state 4 with empty XYZ — confirms state 4 as terminal/exit
- World extent much larger than short file — needs `worldScale` (see spec 09)

### Parser behavior

- State value is the **target state**: `StateController.GoToState(newState, transitionDuration)` is called the instant the state column changes; the smoothstep easing produces the visible transition.
- Empty XYZ: hold last known position.
- Frame timing: 24 fps mapping, with playback rate and loop both configurable on `CSVDriver`.

## CSVDriver and per-object controller

`CSVDriver` parses the file and orchestrates playback. `CSVObjectController` drives a single shape instance from a slice of CSV rows (position, state, alpha fade). See `09_CSVAnalysis.md` for the full design — including the long-data object-pool mode, world-scale parameter, and state-4 fade-out behavior.

```csharp
public class CSVDriver : MonoBehaviour {
    public TextAsset shortDataCsv;
    public TextAsset longDataCsv;

    public GameObject shapeMorphPrefab;   // PlanA or PlanB variant per scene
    public Transform  worldRoot;

    public enum DataSource { Short, Long, None }
    public DataSource activeSource       = DataSource.Short;
    public float      playbackRate       = 1.0f;
    public bool       loop               = true;
    public float      transitionDuration = 1.0f;
    public float      worldScale         = 1.0f;   // 0.05 for VR
}
```

### Playback model

The driver maintains a `playheadTime` that advances with `Time.deltaTime * playbackRate`.

- **Short mode:** one persistent shape object; each frame, find the CSV row at the current playhead, lerp position between adjacent rows, and forward the state value to `StateController.GoToState` when it changes.
- **Long mode:** maintain a pool of shape objects keyed by `object_id`. Spawn on an object's first frame, despawn after its last. Each active object runs its own `CSVObjectController` against its CSV slice.

Looping: when `playheadTime` exceeds the last frame, reset to 0 and replay. The demo runs continuously for evaluation.

### UI

Minimal in-scene UI:
- Current state, current time displayed (TextMeshPro)
- Buttons: Short Data / Long Data / Manual
- In Manual mode: keys 1–4 to trigger states directly
- Key `P` toggles pause
- Key `Space` switches between Plan A and Plan B scenes
- Key `R` resets the playhead
