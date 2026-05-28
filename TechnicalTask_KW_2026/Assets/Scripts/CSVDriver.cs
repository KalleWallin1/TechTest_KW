using System.Collections.Generic;
using UnityEngine;

namespace TechnicalTask
{
    // Parses the short CSV (frame, object_id, x, y, z, state) and drives StateController
    // through each state change at the CSV-specified frame. The morph timing (when each
    // shape morph begins/ends) is configured per transition via the Transition Timings
    // list. Free-form transform animation (rotation/scale/position) is expected to be
    // authored separately in a Unity AnimationClip and sampled via transformAnimationClip.
    public class CSVDriver : MonoBehaviour
    {
        [System.Serializable]
        public class TransitionTiming
        {
            [Tooltip("Free-text label for the inspector — has no effect on behavior.")]
            public string label;
            [Tooltip("Source state, 0-based (0 = State 1 triangle).")]
            public int fromState;
            [Tooltip("Destination state, 0-based.")]
            public int toState;

            [Header("Tint window — drives tint crossfade and pulse amount.")]
            [Tooltip("CSV frame where the tint crossfade begins.")]
            public int tintStartFrame;
            [Tooltip("CSV frame where the tint crossfade finishes.")]
            public int tintEndFrame;

            [Header("Cube window — drives the digit cube Y rotation.")]
            [Tooltip("CSV frame where the number cube begins rotating.")]
            public int cubeStartFrame;
            [Tooltip("CSV frame where the number cube finishes rotating.")]
            public int cubeEndFrame;

            [Header("Morph window — controls the shape morph progress 0→1.")]
            [Tooltip("CSV frame where the shape morph begins.")]
            public int morphStartFrame;
            [Tooltip("CSV frame where the shape morph finishes.")]
            public int morphEndFrame;
            [Tooltip("Morph progress curve. Input: 0..1 over the morph window. Output: 0..1 morph progress (0 = current shape, 1 = next shape).")]
            public AnimationCurve morphCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [Header("Source")]
        [SerializeField] private StateController stateController;
        [SerializeField] private TextAsset       shortDataCsv;

        [Header("Transform animation — optional. If both are set, the clip is sampled at playheadTime each Update and applied to animationTarget's Transform.")]
        [SerializeField] private AnimationClip   transformAnimationClip;
        [SerializeField] private GameObject      animationTarget;

        [Header("Playback")]
        [SerializeField] private bool  autoStartOnAwake = true;
        [SerializeField] private bool  loop             = true;
        [SerializeField, Range(0.1f, 4f)] private float playbackRate = 1.0f;
        [Tooltip("Frame rate of the CSV. Short_Data_Animation_Match.csv was captured at 24 fps.")]
        [SerializeField] private float csvFrameRate     = 24f;

        [Header("Per-transition morph timing — one entry per state pair. Fallback applies when no entry matches.")]
        [SerializeField] private List<TransitionTiming> transitionTimings = new List<TransitionTiming>
        {
            new TransitionTiming {
                label = "1 → 2 (tri → hex)",
                fromState = 0, toState = 1,
                tintStartFrame = 43, tintEndFrame = 63,
                cubeStartFrame = 43, cubeEndFrame = 63,
                morphStartFrame = 90, morphEndFrame = 113,
                morphCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            },
            new TransitionTiming {
                label = "2 → 3 (hex → cir)",
                fromState = 1, toState = 2,
                tintStartFrame = 181, tintEndFrame = 201,
                cubeStartFrame = 181, cubeEndFrame = 201,
                morphStartFrame = 181, morphEndFrame = 205,
                morphCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            },
            new TransitionTiming {
                label = "3 → 4 (cir → sqr)",
                fromState = 2, toState = 3,
                tintStartFrame = 231, tintEndFrame = 250,
                cubeStartFrame = 231, cubeEndFrame = 250,
                morphStartFrame = 231, morphEndFrame = 250,
                morphCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
            },
        };

        [Tooltip("Fallback morph window length (frames) used when a transition has no entry above.")]
        [SerializeField] private int defaultTransitionFrames = 30;

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
        }

        [ContextMenu("Pause")]
        public void Pause()
        {
            playing = false;
        }

        [ContextMenu("Reparse CSV")]
        public void ReparseCsv() => ParseCsv();

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
                    while (playheadTime >= clipSeconds) playheadTime -= clipSeconds;
                }
                else if (clipSeconds > 0f && playheadTime > clipSeconds)
                {
                    playheadTime = clipSeconds;
                }
            }

            float frame = playheadTime * csvFrameRate;
            ApplyPoseForFrame(frame);

            // Sample the user-authored transform animation, if assigned.
            if (transformAnimationClip != null && animationTarget != null)
            {
                transformAnimationClip.SampleAnimation(animationTarget, playheadTime);
            }

            debugIsPlaying     = playing;
            debugPlayheadFrame = frame;
            debugClipFrames    = clipFrames;
            debugEventCount    = events.Count;
        }

        // Finds the active state event for the given playhead frame, computes morph progress
        // from the matching TransitionTiming entry's morphStartFrame/EndFrame/Curve (or
        // a fallback smoothstep over `defaultTransitionFrames` if no entry matches), and
        // pushes the pose to StateController. Rotation/scale/position offsets are always
        // passed as zero/identity — those should be handled by an AnimationClip.
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

            int prevState  = events[activeEventIndex - 1].TargetState;
            int thisState  = events[activeEventIndex].TargetState;
            int eventFrame = events[activeEventIndex].Frame;

            float morphT, generalT, cubeT;
            var timing = FindTiming(prevState, thisState);
            if (timing != null)
            {
                float morphNormT = InvLerp(timing.morphStartFrame, timing.morphEndFrame, frame);
                morphT = (timing.morphCurve != null && timing.morphCurve.length > 0)
                    ? timing.morphCurve.Evaluate(morphNormT)
                    : Smoothstep(morphNormT);

                // Three independent timing windows now:
                //   generalT — tint crossfade and pulse amount
                //   cubeT    — digit cube Y rotation (NumberCubeController reads RotationProgress)
                //   morphT   — shape morph
                generalT = Smoothstep(InvLerp(timing.tintStartFrame, timing.tintEndFrame, frame));
                cubeT    = Smoothstep(InvLerp(timing.cubeStartFrame, timing.cubeEndFrame, frame));
            }
            else
            {
                morphT   = Smoothstep(InvLerp(eventFrame, eventFrame + defaultTransitionFrames, frame));
                generalT = morphT;
                cubeT    = morphT;
            }

            stateController.SetPose(prevState, thisState, generalT, cubeT, morphT, 0f, 1f, Vector2.zero);
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
