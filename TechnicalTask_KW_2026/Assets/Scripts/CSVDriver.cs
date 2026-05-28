using System.Collections.Generic;
using UnityEngine;

namespace TechnicalTask
{
    public class CSVDriver : MonoBehaviour
    {
        [System.Serializable]
        public class TransitionTiming
        {
            [Tooltip("Free-text label for the inspector — has no effect on behavior.")]
            public string label;

            [Header("Which transition this entry configures")]
            [Tooltip("Source state, 0-based (0 = State 1 triangle).")]
            public int fromState;
            [Tooltip("Destination state, 0-based.")]
            public int toState;

            [Header("Frame timing (read off the reference video)")]
            [Tooltip("CSV frame where the shape's rotation begins.")]
            public int rotationStartFrame;
            [Tooltip("CSV frame where the rotation finishes.")]
            public int rotationEndFrame;
            [Tooltip("CSV frame where the shape morph begins.")]
            public int morphStartFrame;
            [Tooltip("CSV frame where the morph finishes.")]
            public int morphEndFrame;

            [Header("Animation curves — the Y value IS the output (no multipliers).")]
            [Tooltip("Output: degrees rotated this transition. Input: 0..1 over the rotation window. Author the last key's Y to set the total rotation (e.g. 180). Reshape to control timing.")]
            public AnimationCurve rotationCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            [Tooltip("Output: scale multiplier. 1 = no change. Input: 0..1 over the union window. Bell (0,1)→(0.5,1.15)→(1,1) for a grow-and-return.")]
            public AnimationCurve scaleCurve    = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            [Tooltip("Output: X-position offset from baseline. Input: 0..1 over the union window. Default flat (no offset).")]
            public AnimationCurve positionXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            [Tooltip("Output: Y-position offset from baseline. Input: 0..1 over the union window. Default flat (no offset).")]
            public AnimationCurve positionYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f);
            [Tooltip("Output: morph progress 0..1. Input: 0..1 over the morph window. Default ease-in-out.")]
            public AnimationCurve morphCurve    = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [Header("Source")]
        [SerializeField] private StateController stateController;
        [SerializeField] private TextAsset       shortDataCsv;

        [Header("Playback")]
        [SerializeField] private bool  autoStartOnAwake = true;
        [SerializeField] private bool  loop             = true;
        [SerializeField, Range(0.1f, 4f)] private float playbackRate = 1.0f;
        [Tooltip("Frame rate of the CSV. Short_Data_Animation_Match.csv was captured at 24 fps.")]
        [SerializeField] private float csvFrameRate     = 24f;

        [Header("Per-transition controls — one labeled entry per state change. Edit the curves directly; the Y value of each curve IS the output value applied to the shape.")]
        [SerializeField] private List<TransitionTiming> transitionTimings = new List<TransitionTiming>
        {
            new TransitionTiming {
                label = "1 → 2 (tri → hex)",
                fromState = 0, toState = 1,
                rotationStartFrame = 43, rotationEndFrame = 90, morphStartFrame = 90, morphEndFrame = 113,
                rotationCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 180f),
                scaleCurve     = MakeBellCurve(1f, 1.15f),
                positionXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                positionYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                morphCurve     = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            },
            new TransitionTiming {
                label = "2 → 3 (hex → cir)",
                fromState = 1, toState = 2,
                rotationStartFrame = 181, rotationEndFrame = 181, morphStartFrame = 181, morphEndFrame = 205,
                rotationCurve  = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                scaleCurve     = AnimationCurve.Linear(0f, 1f, 1f, 1f),
                positionXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                positionYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                morphCurve     = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            },
            new TransitionTiming {
                label = "3 → 4 (cir → sqr)",
                fromState = 2, toState = 3,
                rotationStartFrame = 231, rotationEndFrame = 250, morphStartFrame = 231, morphEndFrame = 250,
                rotationCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 180f),
                scaleCurve     = MakeBellCurve(1f, 1.15f),
                positionXCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                positionYCurve = AnimationCurve.Linear(0f, 0f, 1f, 0f),
                morphCurve     = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            },
        };

        // Constructs an AnimationCurve with a smooth bell shape: starts at baseline, peaks
        // at the given value at t=0.5, returns to baseline at t=1.
        private static AnimationCurve MakeBellCurve(float baseline, float peak)
        {
            return new AnimationCurve(
                new Keyframe(0f,   baseline, 0f, (peak - baseline) * Mathf.PI),
                new Keyframe(0.5f, peak,     0f, 0f),
                new Keyframe(1f,   baseline, -(peak - baseline) * Mathf.PI, 0f));
        }

        [Tooltip("Window length (frames) used for a transition with no entry in the table above.")]
        [SerializeField] private int fallbackTransitionFrames = 30;

        [Header("Debug scrub — pause playback and drag the slider for frame-by-frame comparison")]
        [SerializeField] private bool debugScrubMode;
        [Range(1, 250)] [SerializeField] private int debugScrubFrame = 1;

        [Header("Runtime (read-only)")]
        [SerializeField] private bool   debugIsPlaying;
        [SerializeField] private float  debugPlayheadFrame;
        [SerializeField] private int    debugClipFrames;
        [SerializeField] private int    debugEventCount;

        private struct StateEvent
        {
            public int Frame;
            public int TargetState;
        }

        private readonly List<StateEvent> events = new List<StateEvent>();
        private float playheadTime;
        private int   clipFrames;
        private bool  playing;

        private void Awake()
        {
            if (stateController == null) stateController = GetComponent<StateController>();
            if (stateController != null) stateController.ExternalControl = true;

            ParseCsv();
            if (autoStartOnAwake) Play();
        }

        [ContextMenu("Play")]
        public void Play()
        {
            playing      = true;
            playheadTime = 0f;
            // Video state is reconciled in Update.
        }

        [ContextMenu("Pause")]
        public void Pause()
        {
            playing = false;
            // Video state is reconciled in Update.
        }

        [ContextMenu("Reparse CSV")]
        public void ReparseCsv() => ParseCsv();

        private void Update()
        {
            // 1) Update playheadTime from the active mode.
            if (debugScrubMode)
            {
                // Scrubbing — playhead tracks the slider, so disabling scrub resumes from here.
                playheadTime = Mathf.Max(0, debugScrubFrame - 1) / csvFrameRate;
            }
            else if (playing)
            {
                playheadTime += Time.deltaTime * playbackRate;
                float clipSeconds = clipFrames / csvFrameRate;
                if (loop && clipSeconds > 0f)
                {
                    while (playheadTime >= clipSeconds) playheadTime -= clipSeconds;
                }
                else if (clipSeconds > 0f && playheadTime > clipSeconds)
                {
                    playheadTime = clipSeconds;
                }
            }

            float frame = playheadTime * csvFrameRate;

            // Drive StateController.
            ApplyPoseForFrame(frame);

            debugIsPlaying     = playing;
            debugPlayheadFrame = frame;
            debugClipFrames    = clipFrames;
            debugEventCount    = events.Count;
        }

        // Evaluates the pose for an arbitrary playhead frame and pushes it to StateController.
        // Shared by playback and scrub.
        private void ApplyPoseForFrame(float frame)
        {
            if (stateController == null || events.Count == 0) return;

            int activeEventIndex = -1;
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (events[i].Frame <= frame) { activeEventIndex = i; break; }
            }

            if (activeEventIndex <= 0)
            {
                int s = events[0].TargetState;
                stateController.SetPose(s, s, 0f, 0f, 0f, 0f, 1f, Vector2.zero);
                return;
            }

            int prevState = events[activeEventIndex - 1].TargetState;
            int thisState = events[activeEventIndex].TargetState;

            // Accumulated Z rotation at the start of this transition (sum of the final
            // rotationCurve output of every prior transition).
            float baseRotation = 0f;
            for (int i = 1; i < activeEventIndex; i++)
            {
                var prior = FindTiming(events[i - 1].TargetState, events[i].TargetState);
                if (prior != null && prior.rotationCurve != null && prior.rotationCurve.length > 0)
                    baseRotation += prior.rotationCurve.Evaluate(1f);
            }

            float   rotProgress, morphProgress, generalT;
            float   zRotation       = baseRotation;
            float   scaleMultiplier = 1f;
            Vector2 positionOffset  = Vector2.zero;

            var timing = FindTiming(prevState, thisState);
            if (timing != null)
            {
                float rotNormT   = InvLerp(timing.rotationStartFrame, timing.rotationEndFrame, frame);
                float morphNormT = InvLerp(timing.morphStartFrame,    timing.morphEndFrame,    frame);
                float genStart   = Mathf.Min(timing.rotationStartFrame, timing.morphStartFrame);
                float genEnd     = Mathf.Max(timing.rotationEndFrame,   timing.morphEndFrame);
                float genNormT   = InvLerp(genStart, genEnd, frame);

                // Curves output their values DIRECTLY now (degrees, multiplier, offset).
                zRotation       = baseRotation + EvaluateCurveOrDefault(timing.rotationCurve, rotNormT, 0f);
                morphProgress   = EvaluateCurveOrSmoothstep(timing.morphCurve, morphNormT);
                scaleMultiplier = EvaluateCurveOrDefault(timing.scaleCurve,     genNormT, 1f);
                float posX      = EvaluateCurveOrDefault(timing.positionXCurve, genNormT, 0f);
                float posY      = EvaluateCurveOrDefault(timing.positionYCurve, genNormT, 0f);
                positionOffset  = new Vector2(posX, posY);

                rotProgress = rotNormT;
                generalT    = Smoothstep(genNormT);
            }
            else
            {
                int start = events[activeEventIndex].Frame;
                float t = Smoothstep(InvLerp(start, start + fallbackTransitionFrames, frame));
                rotProgress = morphProgress = generalT = t;
            }

            stateController.SetPose(prevState, thisState, generalT, rotProgress, morphProgress, zRotation, scaleMultiplier, positionOffset);
        }

        private TransitionTiming FindTiming(int from, int to)
        {
            if (transitionTimings == null) return null;
            for (int i = 0; i < transitionTimings.Count; i++)
                if (transitionTimings[i].fromState == from && transitionTimings[i].toState == to)
                    return transitionTimings[i];
            return null;
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

        // Evaluates a curve at t, returning the given default if the curve is null or empty.
        // Used for rotation/scale/position curves where the curve outputs the actual value
        // (degrees, multiplier, offset) and missing curves should fall back to "no effect".
        private static float EvaluateCurveOrDefault(AnimationCurve curve, float t, float defaultValue)
        {
            if (curve == null || curve.length == 0) return defaultValue;
            return curve.Evaluate(t);
        }

        // Evaluates a curve at t, falling back to smoothstep — used for morph progress,
        // which still operates as a 0..1 normalized progress curve.
        private static float EvaluateCurveOrSmoothstep(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length == 0) return Smoothstep(t);
            return curve.Evaluate(t);
        }

        private void ParseCsv()
        {
            events.Clear();
            clipFrames = 0;

            if (shortDataCsv == null)
            {
                Debug.LogWarning("CSVDriver: shortDataCsv is not assigned.", this);
                return;
            }

            string[] lines = shortDataCsv.text.Split('\n');
            int previousState = -1;
            int maxFrame      = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("frame", System.StringComparison.OrdinalIgnoreCase)) continue;

                string[] cols = line.Split(',');
                if (cols.Length < 6) continue;
                if (!int.TryParse(cols[0], out int frame))    continue;
                if (!int.TryParse(cols[5], out int csvState)) continue;

                int zeroBasedState = Mathf.Clamp(csvState - 1, 0, StateController.StateCount - 1);
                maxFrame = Mathf.Max(maxFrame, frame);

                if (zeroBasedState != previousState)
                {
                    events.Add(new StateEvent { Frame = frame, TargetState = zeroBasedState });
                    previousState = zeroBasedState;
                }
            }

            clipFrames = maxFrame;
        }
    }
}
