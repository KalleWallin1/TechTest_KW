using System.Collections.Generic;
using UnityEngine;

namespace TechnicalTask
{
    // Parses a multi-object CSV (frame, object_id, x, y, z, state) such as
    // Long_Data_Free_Form.csv and drives one MarkerRoot prefab instance per object_id.
    //
    // Each spawned instance carries its own StateController + visual controllers
    // + AnimationClip (via an Animator on the prefab). This driver only:
    //   1. groups CSV rows by object_id and records per-object state events + positions
    //   2. spawns a prefab when an object's lifespan covers the current frame
    //   3. despawns the prefab when the lifespan ends (or on loop wrap)
    //   4. each frame, calls SetPose on every live instance's StateController and
    //      writes its localPosition from the per-object position track.
    //
    // CSVDriver is the single-object counterpart, used for the short data demo.
    // Unlike CSVDriver, this component does NOT take a hand-authored TransitionTimings
    // list — there are too many transitions in the long data to author by hand. Instead
    // it uses three global window settings (tint, cube, morph) applied to every state
    // change uniformly.
    //
    // Prefab requirements:
    //   - Must contain a StateController on the root or a descendant (looked up via
    //     GetComponentInChildren). The driver sets ExternalControl = true on it.
    //   - All visual controllers' StateController serializeField references should
    //     point to the prefab's OWN StateController (not a scene instance) so prefab
    //     instantiation produces self-contained instances.
    //   - Animation (if any) should be set up via an Animator with a controller on
    //     the prefab — each spawned instance gets independent playback.
    public class CSVMultidataDriver : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private TextAsset  longDataCsv;
        [Tooltip("Prefab instantiated per object_id. Must carry its own StateController (root or descendant).")]
        [SerializeField] private GameObject markerPrefab;
        [Tooltip("Optional parent for spawned instances. Defaults to this transform.")]
        [SerializeField] private Transform  spawnParent;

        [Header("Animation — optional. The clip is sampled each frame on each live instance's animation target, using time-since-spawn so each object's animation starts fresh when it appears.")]
        [Tooltip("AnimationClip sampled per-instance. Same clip used by CSVDriver for the short data. Leave null if the prefab carries its own Animator + Controller instead.")]
        [SerializeField] private AnimationClip transformAnimationClip;
        [Tooltip("Child name (or path, '/' separated) within each spawned instance to receive the AnimationClip. Leave empty to target the root. Default 'MorphSources' matches MainPrefab's child layout.")]
        [SerializeField] private string animationTargetChildName = "MorphSources";
        [Tooltip("If true, the clip loops over each instance's lifespan (instance time wraps at clip length). If false, the clip plays once then clamps to its last frame.")]
        [SerializeField] private bool loopInstanceAnimation = true;

        [Header("Marker position — applied to each spawned instance's localPosition each frame.")]
        [Tooltip("Scale factor applied to the raw CSV XYZ. The long data uses larger coordinates than the short data — try 0.02–0.1 for a comfortable in-scene size.")]
        [SerializeField] private float   worldScale  = 0.05f;
        [Tooltip("Constant offset added AFTER scaling. Useful to center the swarm in front of the camera.")]
        [SerializeField] private Vector3 worldOffset = Vector3.zero;
        [Tooltip("Component-wise multiply on the scaled position. (1,1,1) for no remap, (1,1,-1) to flip Z if the CSV uses a different handedness, etc.")]
        [SerializeField] private Vector3 axisRemap   = new Vector3(1f, 1f, 1f);
        [Tooltip("If true, lerps between adjacent CSV samples for sub-frame smoothness. If false, snaps to the nearest CSV frame.")]
        [SerializeField] private bool    interpolateBetweenFrames = true;

        [Header("Transition windows — applied uniformly to every state change in the CSV.")]
        [Tooltip("Frames it takes for the tint to crossfade after a state change.")]
        [SerializeField] private int tintCrossfadeFrames = 20;
        [Tooltip("Frames it takes for the number cube to rotate after a state change. Set equal to tintCrossfadeFrames to keep cube and tint in sync.")]
        [SerializeField] private int cubeRotationFrames  = 20;
        [Tooltip("Frames it takes for the shape morph to play out after a state change.")]
        [SerializeField] private int morphFrames         = 24;
        [Tooltip("Offset (frames) added to morph start, relative to the state-change frame. Use a positive value if the morph should lag the tint change (e.g. matching the short data's first transition where the morph starts ~47 frames after the tint).")]
        [SerializeField] private int morphStartOffsetFrames = 0;

        [Header("Fade-out — each object fades alpha over the last N frames of its lifespan.")]
        [Tooltip("Number of frames before the object's last data row over which alpha ramps from 1 → 0. Set to 0 to disable fade.")]
        [SerializeField] private int fadeOutTailFrames = 20;

        [Header("Playback")]
        [SerializeField] private bool  autoStartOnAwake = true;
        [SerializeField] private bool  loop             = true;
        [SerializeField, Range(0.1f, 4f)] private float playbackRate = 1.0f;
        [Tooltip("Frame rate of the CSV. Long_Data_Free_Form.csv was captured at 24 fps.")]
        [SerializeField] private float csvFrameRate     = 24f;

        [Header("Debug scrub")]
        [SerializeField] private bool debugScrubMode;
        [SerializeField] private int  debugScrubFrame = 1;

        [Header("Runtime (read-only)")]
        [SerializeField] private bool  debugIsPlaying;
        [SerializeField] private float debugPlayheadFrame;
        [SerializeField] private int   debugClipFrames;
        [SerializeField] private int   debugObjectCount;
        [SerializeField] private int   debugLiveInstanceCount;

        private struct StateEvent
        {
            public int Frame;
            public int TargetState;
        }

        private struct PositionSample
        {
            public int Frame;
            public Vector3 Position;
        }

        private class ObjectTrack
        {
            public int ObjectId;
            public int FirstFrame;
            public int LastFrame;
            public readonly List<StateEvent>     StateEvents = new List<StateEvent>();
            public readonly List<PositionSample> Positions   = new List<PositionSample>();
        }

        private class LiveInstance
        {
            public GameObject      Go;
            public Transform       Tx;
            public StateController State;
            public Transform       AnimTarget;
            public float           SpawnTime;
        }

        private readonly Dictionary<int, ObjectTrack>  tracks = new Dictionary<int, ObjectTrack>();
        private readonly Dictionary<int, LiveInstance> live   = new Dictionary<int, LiveInstance>();
        private readonly List<int> scratchIds = new List<int>();
        private float playheadTime;
        private int   clipFrames;
        private bool  playing;

        private void Awake()
        {
            if (spawnParent == null) spawnParent = transform;
            ParseCsv();
            if (autoStartOnAwake) Play();
        }

        private void OnDisable() => DespawnAll();

        [ContextMenu("Play")]
        public void Play()
        {
            playing      = true;
            playheadTime = 0f;
            DespawnAll();
        }

        [ContextMenu("Pause")]
        public void Pause() => playing = false;

        [ContextMenu("Reparse CSV")]
        public void ReparseCsv() { DespawnAll(); ParseCsv(); }

        private void Update()
        {
            if (debugScrubMode)
            {
                playheadTime = Mathf.Max(0, debugScrubFrame - 1) / csvFrameRate;
            }
            else if (playing)
            {
                playheadTime += Time.deltaTime * playbackRate;
                float clipSeconds = clipFrames / csvFrameRate;
                if (loop && clipSeconds > 0f)
                {
                    while (playheadTime >= clipSeconds)
                    {
                        playheadTime -= clipSeconds;
                        DespawnAll(); // restart all object lifespans cleanly on loop wrap
                    }
                }
                else if (clipSeconds > 0f && playheadTime > clipSeconds)
                {
                    playheadTime = clipSeconds;
                }
            }

            float frame = playheadTime * csvFrameRate;
            UpdateSpawns(frame);
            DriveLiveInstances(frame);

            debugIsPlaying         = playing;
            debugPlayheadFrame     = frame;
            debugClipFrames        = clipFrames;
            debugObjectCount       = tracks.Count;
            debugLiveInstanceCount = live.Count;
        }

        // Spawns prefab instances for any track that covers the current frame and isn't
        // yet alive, and despawns instances whose track has ended (or that are out of
        // window after a debug scrub).
        private void UpdateSpawns(float frame)
        {
            if (markerPrefab == null) return;

            foreach (var kv in tracks)
            {
                int id  = kv.Key;
                var trk = kv.Value;
                bool inWindow = frame >= trk.FirstFrame && frame <= trk.LastFrame;
                if (inWindow && !live.ContainsKey(id))
                {
                    var go = Instantiate(markerPrefab, spawnParent);
                    go.name = $"{markerPrefab.name}_obj{id}";
                    var sc = go.GetComponentInChildren<StateController>();
                    if (sc != null) sc.ExternalControl = true;

                    // Resolve animation target child (or root) once at spawn.
                    Transform animTarget = string.IsNullOrEmpty(animationTargetChildName)
                        ? go.transform
                        : go.transform.Find(animationTargetChildName);

                    live[id] = new LiveInstance {
                        Go         = go,
                        Tx         = go.transform,
                        State      = sc,
                        AnimTarget = animTarget,
                        SpawnTime  = playheadTime,
                    };
                }
            }

            scratchIds.Clear();
            foreach (var kv in live)
            {
                int id = kv.Key;
                if (!tracks.TryGetValue(id, out var trk) || frame < trk.FirstFrame || frame > trk.LastFrame)
                {
                    scratchIds.Add(id);
                }
            }
            for (int i = 0; i < scratchIds.Count; i++)
            {
                int id = scratchIds[i];
                if (live.TryGetValue(id, out var inst))
                {
                    if (inst.Go != null) Destroy(inst.Go);
                    live.Remove(id);
                }
            }
        }

        private void DriveLiveInstances(float frame)
        {
            foreach (var kv in live)
            {
                int id = kv.Key;
                var inst = kv.Value;
                if (!tracks.TryGetValue(id, out var trk)) continue;

                ApplyPose(inst, trk, frame);
                ApplyPosition(inst, trk, frame);
                ApplyAnimation(inst);
            }
        }

        // Samples the configured AnimationClip onto this instance's animation target at
        // time-since-spawn (optionally wrapping at clip length so the animation loops
        // across long lifespans). Mirrors CSVDriver.SampleAnimation pattern.
        private void ApplyAnimation(LiveInstance inst)
        {
            if (transformAnimationClip == null || inst.AnimTarget == null) return;
            float t = playheadTime - inst.SpawnTime;
            if (loopInstanceAnimation && transformAnimationClip.length > 0f)
            {
                t = Mathf.Repeat(t, transformAnimationClip.length);
            }
            transformAnimationClip.SampleAnimation(inst.AnimTarget.gameObject, t);
        }

        // Resolves the active state event for this object at the given frame, derives
        // tint/cube/morph progress from the global windows, computes fade alpha, and
        // pushes everything to the instance's StateController.
        private void ApplyPose(LiveInstance inst, ObjectTrack trk, float frame)
        {
            if (inst.State == null || trk.StateEvents.Count == 0) return;

            int activeIndex = -1;
            for (int i = trk.StateEvents.Count - 1; i >= 0; i--)
            {
                if (trk.StateEvents[i].Frame <= frame) { activeIndex = i; break; }
            }

            float alpha = ComputeAlpha(trk, frame);

            if (activeIndex <= 0)
            {
                // Before or on the first event for this object — hold its initial state.
                int s = trk.StateEvents.Count > 0 ? trk.StateEvents[0].TargetState : 0;
                inst.State.SetPose(s, s, 0f, 0f, 0f, 0f, 1f, Vector2.zero, alpha);
                return;
            }

            int prevState  = trk.StateEvents[activeIndex - 1].TargetState;
            int thisState  = trk.StateEvents[activeIndex].TargetState;
            int eventFrame = trk.StateEvents[activeIndex].Frame;

            float generalT = Smoothstep(InvLerp(eventFrame, eventFrame + tintCrossfadeFrames, frame));
            float cubeT    = Smoothstep(InvLerp(eventFrame, eventFrame + cubeRotationFrames,  frame));
            float morphT   = Smoothstep(InvLerp(eventFrame + morphStartOffsetFrames,
                                                eventFrame + morphStartOffsetFrames + morphFrames, frame));

            inst.State.SetPose(prevState, thisState, generalT, cubeT, morphT, 0f, 1f, Vector2.zero, alpha);
        }

        private float ComputeAlpha(ObjectTrack trk, float frame)
        {
            if (fadeOutTailFrames <= 0) return 1f;
            int fadeStart = trk.LastFrame - fadeOutTailFrames;
            return 1f - Smoothstep(InvLerp(fadeStart, trk.LastFrame, frame));
        }

        // Looks up the position sample for the current frame in this object's positions
        // list, optionally interpolates to the next sample, applies the world transform,
        // and writes to the instance's localPosition.
        private void ApplyPosition(LiveInstance inst, ObjectTrack trk, float frame)
        {
            if (inst.Tx == null || trk.Positions.Count == 0) return;

            // Direct index assuming contiguous frames, then nudge forward/back if not.
            int idx = Mathf.FloorToInt(frame) - trk.Positions[0].Frame;
            if (idx < 0) idx = 0;
            if (idx >= trk.Positions.Count) idx = trk.Positions.Count - 1;
            while (idx > 0                       && trk.Positions[idx].Frame     > frame) idx--;
            while (idx + 1 < trk.Positions.Count && trk.Positions[idx + 1].Frame <= frame) idx++;

            Vector3 a = trk.Positions[idx].Position;
            Vector3 raw;
            if (interpolateBetweenFrames && idx + 1 < trk.Positions.Count)
            {
                Vector3 b      = trk.Positions[idx + 1].Position;
                float   spanA  = trk.Positions[idx].Frame;
                float   spanB  = trk.Positions[idx + 1].Frame;
                float   t      = (spanB > spanA) ? Mathf.Clamp01((frame - spanA) / (spanB - spanA)) : 0f;
                raw = Vector3.Lerp(a, b, t);
            }
            else
            {
                raw = a;
            }

            Vector3 scaled = new Vector3(
                raw.x * worldScale * axisRemap.x,
                raw.y * worldScale * axisRemap.y,
                raw.z * worldScale * axisRemap.z);

            inst.Tx.localPosition = scaled + worldOffset;
        }

        private void DespawnAll()
        {
            foreach (var kv in live)
            {
                if (kv.Value.Go != null) Destroy(kv.Value.Go);
            }
            live.Clear();
        }

        private static float InvLerp(float a, float b, float v)
        {
            if (Mathf.Abs(b - a) < 1e-5f) return v >= b ? 1f : 0f;
            return Mathf.Clamp01((v - a) / (b - a));
        }

        private static float Smoothstep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private void ParseCsv()
        {
            tracks.Clear();
            clipFrames = 0;

            if (longDataCsv == null)
            {
                Debug.LogWarning("CSVMultidataDriver: longDataCsv is not assigned.", this);
                return;
            }

            string[] lines = longDataCsv.text.Split('\n');
            var lastKnownPos = new Dictionary<int, Vector3>();
            var lastState    = new Dictionary<int, int>();
            int maxFrame     = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("frame", System.StringComparison.OrdinalIgnoreCase)) continue;

                string[] cols = line.Split(',');
                if (cols.Length < 6) continue;
                if (!int.TryParse(cols[0], out int frame))    continue;
                if (!int.TryParse(cols[1], out int objectId)) continue;
                if (!int.TryParse(cols[5], out int csvState)) continue;

                if (!tracks.TryGetValue(objectId, out var trk))
                {
                    trk = new ObjectTrack { ObjectId = objectId, FirstFrame = frame, LastFrame = frame };
                    tracks[objectId] = trk;
                }
                trk.FirstFrame = Mathf.Min(trk.FirstFrame, frame);
                trk.LastFrame  = Mathf.Max(trk.LastFrame,  frame);

                // Position may be empty for the "leaving the scene" tail; hold last known.
                Vector3 pos = lastKnownPos.TryGetValue(objectId, out var lkp) ? lkp : Vector3.zero;
                bool hasX = float.TryParse(cols[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                bool hasY = float.TryParse(cols[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
                bool hasZ = float.TryParse(cols[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
                if (hasX && hasY && hasZ)
                {
                    pos = new Vector3(x, y, z);
                    lastKnownPos[objectId] = pos;
                }
                trk.Positions.Add(new PositionSample { Frame = frame, Position = pos });

                int zeroBasedState = Mathf.Clamp(csvState - 1, 0, StateController.StateCount - 1);
                if (!lastState.TryGetValue(objectId, out int prev) || prev != zeroBasedState)
                {
                    trk.StateEvents.Add(new StateEvent { Frame = frame, TargetState = zeroBasedState });
                    lastState[objectId] = zeroBasedState;
                }

                maxFrame = Mathf.Max(maxFrame, frame);
            }

            clipFrames = maxFrame;
        }
    }
}
