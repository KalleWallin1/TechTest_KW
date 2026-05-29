using UnityEngine;

namespace TechnicalTask
{
    public class StateController : MonoBehaviour
    {
        public const int StateCount = 4;

        [System.Serializable]
        public struct StatePreset
        {
            public int   shapeIndex;   // 0=tri, 1=hex, 2=circle, 3=square
            public int   digitIndex;   // 0..3 → digits "1".."4" on the number strip
            public Color tint;
            public bool  pulsing;
        }

        [Header("Per-state data (length 4: index 0 = State 1, ..., index 3 = State 4). Per-transition values (rotation amount, scale amplitude, position offset, frame timing) all live on CSVDriver's Transition Timings list.")]
        [SerializeField] private StatePreset[] presets = DefaultPresets();

        [Header("Default duration if GoToState is called without a duration (manual cycle only — CSVDriver bypasses this).")]
        [SerializeField] private float defaultTransitionDuration = 1.0f;

        [Header("Runtime (read-only — populated each frame for inspector visibility)")]
        [SerializeField] private int     debugCurrentState;
        [SerializeField] private int     debugNextState;
        [SerializeField, Range(0f, 1f)] private float debugTransitionT;
        [SerializeField, Range(0f, 1f)] private float debugRotationProgress;
        [SerializeField, Range(0f, 1f)] private float debugMorphT;
        [SerializeField] private Color   debugBlendedTint;
        [SerializeField] private float   debugBlendedZRotationDegrees;
        [SerializeField, Range(0f, 1f)] private float debugBlendedDigitU;
        [SerializeField, Range(0f, 1f)] private float debugBlendedPulseAmount;
        [SerializeField] private float   debugBlendedScaleMultiplier;
        [SerializeField] private Vector2 debugBlendedPositionOffset;
        [SerializeField, Range(0f, 1f)] private float debugBlendedAlpha;

        public int   CurrentState     { get; private set; } = 0;
        public int   NextState        { get; private set; } = 0;
        public float TransitionT      { get; private set; } = 0f;
        public float MorphT           { get; private set; } = 0f;
        public float RotationProgress { get; private set; } = 0f;

        public bool IsTransitioning => transitionDuration > 0f && elapsed < transitionDuration;

        public Color   BlendedTint             { get; private set; }
        public float   BlendedZRotationDegrees { get; private set; }
        public float   BlendedDigitU           { get; private set; }
        public float   BlendedPulseAmount      { get; private set; }
        public float   BlendedScaleMultiplier  { get; private set; } = 1f;
        public Vector2 BlendedPositionOffset   { get; private set; } = Vector2.zero;
        public float   BlendedAlpha            { get; private set; } = 1f;

        public int CurrentShapeIndex => presets[CurrentState].shapeIndex;
        public int NextShapeIndex    => presets[NextState].shapeIndex;

        // When true, the internal timer is disabled and an external driver (e.g. CSVDriver)
        // sets the pose each frame via SetPose. The manual GoToState/timer path is bypassed.
        public bool ExternalControl { get; set; }

        private float elapsed;
        private float transitionDuration;

        private void Awake()
        {
            EnsureArraysSized();
            SetStateImmediate(0);
        }

        private void OnValidate()
        {
            EnsureArraysSized();
        }

        private void Update()
        {
            if (ExternalControl) return;

            if (IsTransitioning)
            {
                elapsed += Time.deltaTime;
                float linearT = Mathf.Clamp01(elapsed / transitionDuration);
                TransitionT   = Smoothstep(linearT);

                if (linearT >= 1f)
                {
                    CurrentState       = NextState;
                    transitionDuration = 0f;
                    elapsed            = 0f;
                    TransitionT        = 0f;
                }
            }
            // In timer mode (no CSVDriver), rotation/scale/position are not animated —
            // the rich per-transition values live on CSVDriver. This path is for basic
            // state/tint testing only.
            RotationProgress = TransitionT;
            MorphT           = TransitionT;
            ApplyBlendedOutputs(zRotationDegrees: 0f, scaleMultiplier: 1f, positionOffset: Vector2.zero);
        }

        public void GoToState(int newState, float duration = -1f)
        {
            newState = Mathf.Clamp(newState, 0, StateCount - 1);
            if (duration < 0f) duration = defaultTransitionDuration;

            if (newState == CurrentState && !IsTransitioning) return;

            if (duration <= 0f)
            {
                SetStateImmediate(newState);
                return;
            }

            if (IsTransitioning) CurrentState = NextState;

            NextState          = newState;
            transitionDuration = duration;
            elapsed            = 0f;
            TransitionT        = 0f;
        }

        public void SetStateImmediate(int newState)
        {
            newState           = Mathf.Clamp(newState, 0, StateCount - 1);
            CurrentState       = newState;
            NextState          = newState;
            transitionDuration = 0f;
            elapsed            = 0f;
            TransitionT        = 0f;
            RotationProgress   = 0f;
            MorphT             = 0f;
            ApplyBlendedOutputs(zRotationDegrees: 0f, scaleMultiplier: 1f, positionOffset: Vector2.zero);
        }

        public StatePreset GetPreset(int stateIndex) => presets[Mathf.Clamp(stateIndex, 0, StateCount - 1)];

        // Bypasses the internal timer and sets the pose directly with all blended values
        // computed externally (by CSVDriver). Per-state data (tint, digit, pulsing) is still
        // looked up locally from presets.
        public void SetPose(int currentState, int nextState, float generalT, float rotationProgress, float morphProgress, float zRotationDegrees, float scaleMultiplier, Vector2 positionOffset, float renderAlpha = 1f)
        {
            CurrentState       = Mathf.Clamp(currentState, 0, StateCount - 1);
            NextState          = Mathf.Clamp(nextState,    0, StateCount - 1);
            TransitionT        = Mathf.Clamp01(generalT);
            RotationProgress   = Mathf.Clamp01(rotationProgress);
            MorphT             = Mathf.Clamp01(morphProgress);
            BlendedAlpha       = Mathf.Clamp01(renderAlpha);
            transitionDuration = 0f;
            elapsed            = 0f;
            ApplyBlendedOutputs(zRotationDegrees, scaleMultiplier, positionOffset);
        }

        private void ApplyBlendedOutputs(float zRotationDegrees, float scaleMultiplier, Vector2 positionOffset)
        {
            var a = presets[CurrentState];
            var b = presets[NextState];

            BlendedZRotationDegrees = zRotationDegrees;
            BlendedScaleMultiplier  = scaleMultiplier;
            BlendedPositionOffset   = positionOffset;

            BlendedTint        = Color.Lerp(a.tint, b.tint, TransitionT);
            BlendedDigitU      = Mathf.Lerp(a.digitIndex, b.digitIndex, TransitionT) * (1f / StateCount);
            BlendedPulseAmount = Mathf.Lerp(a.pulsing ? 1f : 0f, b.pulsing ? 1f : 0f, TransitionT);

            debugCurrentState            = CurrentState;
            debugNextState               = NextState;
            debugTransitionT             = TransitionT;
            debugRotationProgress        = RotationProgress;
            debugMorphT                  = MorphT;
            debugBlendedTint             = BlendedTint;
            debugBlendedZRotationDegrees = BlendedZRotationDegrees;
            debugBlendedDigitU           = BlendedDigitU;
            debugBlendedPulseAmount      = BlendedPulseAmount;
            debugBlendedScaleMultiplier  = BlendedScaleMultiplier;
            debugBlendedPositionOffset   = BlendedPositionOffset;
            debugBlendedAlpha            = BlendedAlpha;
        }

        private void EnsureArraysSized()
        {
            if (presets == null || presets.Length != StateCount)
            {
                var resized = DefaultPresets();
                if (presets != null)
                {
                    for (int i = 0; i < Mathf.Min(presets.Length, StateCount); i++) resized[i] = presets[i];
                }
                presets = resized;
            }
        }

        private static StatePreset[] DefaultPresets() => new[]
        {
            new StatePreset { shapeIndex = 0, digitIndex = 0, tint = HexColor("#C2C541"), pulsing = false }, // State 1: triangle, yellow-green
            new StatePreset { shapeIndex = 1, digitIndex = 1, tint = HexColor("#AC5654"), pulsing = false }, // State 2: hexagon, red/coral
            new StatePreset { shapeIndex = 2, digitIndex = 2, tint = HexColor("#3BB73D"), pulsing = false }, // State 3: circle, green
            new StatePreset { shapeIndex = 3, digitIndex = 3, tint = HexColor("#3BC5C7"), pulsing = true  }, // State 4: square, cyan, pulsing
        };

        private static Color HexColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        private static float Smoothstep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
