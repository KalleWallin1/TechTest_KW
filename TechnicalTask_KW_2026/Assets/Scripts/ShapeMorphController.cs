using UnityEngine;

namespace TechnicalTask
{
    [RequireComponent(typeof(MeshRenderer))]
    public class ShapeMorphController : MonoBehaviour
    {
        [SerializeField] private StateController stateController;
        [SerializeField] private MeshRenderer    meshRenderer;
        [SerializeField] private bool            applyZRotationToTransform = true;

        [Header("Visible only when no StateController is wired — for shader-only iteration")]
        [SerializeField] private bool   useManualOverride;
        [SerializeField] private Color  manualTint            = Color.white;
        [SerializeField, Range(0, 3)] private int   manualCurrentShape   = 0;
        [SerializeField, Range(0, 3)] private int   manualNextShape      = 0;
        [SerializeField, Range(0, 1)] private float manualTransitionT    = 0f;
        [SerializeField] private float manualRotationDegrees = 0f;
        [SerializeField, Range(0, 1)] private float manualPulseAmount    = 0f;

        private static readonly int ColorId        = Shader.PropertyToID("_Color");
        private static readonly int MorphCurrentId = Shader.PropertyToID("_MorphCurrent");
        private static readonly int MorphNextId    = Shader.PropertyToID("_MorphNext");
        private static readonly int MorphTId       = Shader.PropertyToID("_MorphT");
        private static readonly int PulseAmountId  = Shader.PropertyToID("_PulseAmount");

        private MaterialPropertyBlock mpb;

        private void Awake()
        {
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            mpb = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (meshRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();

            meshRenderer.GetPropertyBlock(mpb);

            if (useManualOverride || stateController == null)
            {
                mpb.SetColor(ColorId,        manualTint);
                mpb.SetFloat(MorphCurrentId, manualCurrentShape);
                mpb.SetFloat(MorphNextId,    manualNextShape);
                mpb.SetFloat(MorphTId,       manualTransitionT);
                mpb.SetFloat(PulseAmountId,  manualPulseAmount);
                if (applyZRotationToTransform) SetZRotation(manualRotationDegrees);
            }
            else
            {
                mpb.SetColor(ColorId,        stateController.BlendedTint);
                mpb.SetFloat(MorphCurrentId, stateController.CurrentShapeIndex);
                mpb.SetFloat(MorphNextId,    stateController.NextShapeIndex);
                mpb.SetFloat(MorphTId,       stateController.MorphT);
                mpb.SetFloat(PulseAmountId,  stateController.BlendedPulseAmount);
                if (applyZRotationToTransform) SetZRotation(stateController.BlendedZRotationDegrees);
            }

            meshRenderer.SetPropertyBlock(mpb);
        }

        private void SetZRotation(float degrees)
        {
            var e = transform.localEulerAngles;
            transform.localEulerAngles = new Vector3(e.x, e.y, degrees);
        }
    }
}
