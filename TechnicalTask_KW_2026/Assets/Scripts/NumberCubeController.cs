using UnityEngine;

namespace TechnicalTask
{
    [RequireComponent(typeof(MeshRenderer))]
    public class NumberCubeController : MonoBehaviour
    {
        [SerializeField] private StateController stateController;
        [SerializeField] private MeshRenderer    meshRenderer;

        [Tooltip("Y rotation accumulated per state transition. Default 90 — matches the 4-sided number cube.")]
        [SerializeField] private float rotationPerTransition = 90f;
        [Tooltip("Y rotation that displays State 1 (digit '1') facing the camera. Set per the cube's UV/face orientation.")]
        [SerializeField] private float initialYRotation = -180f;

        [Header("Manual override — for verifying without the StateController")]
        [SerializeField] private bool   useManualOverride;
        [SerializeField] private Color  manualTint            = Color.white;
        [SerializeField, Range(0, 3)] private int   manualCurrentDigit = 0;
        [SerializeField, Range(0, 3)] private int   manualNextDigit    = 0;
        [SerializeField, Range(0, 1)] private float manualTransitionT  = 0f;
        [SerializeField, Range(0, 1)] private float manualPulseAmount  = 0f;

        private static readonly int ColorId       = Shader.PropertyToID("_Color");
        private static readonly int PulseAmountId = Shader.PropertyToID("_PulseAmount");

        private MaterialPropertyBlock mpb;
        private float baseYRotation;
        private int   lastSeenCurrent = -1;

        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (meshRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();

            float yRotation;
            Color tint;
            float pulse;

            if (useManualOverride || stateController == null)
            {
                yRotation = initialYRotation
                          + manualCurrentDigit * rotationPerTransition
                          + (manualNextDigit - manualCurrentDigit) * rotationPerTransition * manualTransitionT;
                tint  = manualTint;
                pulse = manualPulseAmount;
            }
            else
            {
                if (lastSeenCurrent < 0)
                {
                    lastSeenCurrent = stateController.CurrentState;
                    baseYRotation   = initialYRotation + stateController.CurrentState * rotationPerTransition;
                }
                if (stateController.CurrentState != lastSeenCurrent)
                {
                    baseYRotation  += rotationPerTransition;
                    lastSeenCurrent = stateController.CurrentState;
                }

                yRotation = stateController.IsTransitioning
                    ? baseYRotation + rotationPerTransition * stateController.TransitionT
                    : baseYRotation;

                tint  = stateController.BlendedTint;
                pulse = stateController.BlendedPulseAmount;
            }

            ApplyYRotation(yRotation);

            meshRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, tint);
            mpb.SetFloat(PulseAmountId, pulse);
            meshRenderer.SetPropertyBlock(mpb);
        }

        private void ApplyYRotation(float yDegrees)
        {
            var e = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(e.x, yDegrees, e.z);
        }
    }
}
