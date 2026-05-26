# 05 — StateController and CSV Driving

## StateController API

The state machine is shared between Plan A and Plan B. The same script drives either shader.

```csharp
public class StateController : MonoBehaviour {
    [System.Serializable]
    public struct StatePreset {
        public int      shapeIndex;     // 0=tri, 1=hex, 2=circle, 3=star
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

Part 2 of the brief provides two CSV files in the `Data` folder. The exact format is not yet known (CSVs will be shared by Rightware when the assessment continues). The expected schema based on context:

### Short data file

Likely a sequence of `(time, state)` rows:

```
time,state
0.0,1
2.3,2
7.7,3
9.5,4
```

Or possibly `(time, state, transitionDuration)`:

```
time,state,duration
0.0,1,0
2.3,2,2.1
7.7,3,1.0
9.5,4,0.8
```

Or even per-frame state values:

```
frame,state
0,1
1,1
...
55,2
...
```

The parser will be flexible — it will detect the column layout from the header row and adapt.

### Long data file

The brief calls this optional. It is presumably either:
- A higher-resolution version of the short data
- A more complex sequence with more state changes
- Per-frame data over a longer duration

Will be processed if time permits and if it adds something meaningful to the demo.

## CSVDriver MonoBehaviour

```csharp
public class CSVDriver : MonoBehaviour {
    public TextAsset shortDataCsv;
    public TextAsset longDataCsv;
    public StateController stateController;

    public enum DataSource { Short, Long, None }
    public DataSource activeSource = DataSource.Short;

    // Parses the CSV and produces a List<StateEvent> { time, targetState, transitionDuration }
    // Playback loop advances through events based on Time.time
}
```

### Playback model

The driver maintains a `playheadTime` that advances with `Time.deltaTime`. When `playheadTime` crosses an event time, it calls `stateController.GoToState(event.targetState, event.transitionDuration)`.

Looping: when `playheadTime` exceeds the last event time + some hold, reset to 0 and re-trigger from the beginning. This makes the demo run continuously for evaluation.

### UI

Minimal in-scene UI:
- Current state, current time displayed (TextMeshPro)
- Buttons: Short Data / Long Data / Manual
- In Manual mode: keys 1–4 to trigger states directly
- Key `P` toggles pause
- Key `Space` switches between Plan A and Plan B scenes
- Key `R` resets the playhead

## Adapting to the actual CSVs

When the real CSV files arrive, the CSVDriver parser will be updated to match. The state controller and shaders are independent of CSV format — they consume `(state, time)` events.

If the CSV contains values that don't map cleanly to discrete states (e.g. a continuous numeric value), it would route into:
- A direct value mapping (e.g. value × 4 → state index)
- A threshold mapping (e.g. >0.75 → state 4)
- Or driving a continuous shape parameter directly (e.g. a fractional "morph progress" instead of discrete states)

Will be decided once the CSVs are visible.
