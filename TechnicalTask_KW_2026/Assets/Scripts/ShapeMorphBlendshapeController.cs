using UnityEngine;

namespace TechnicalTask
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class ShapeMorphBlendshapeController : MonoBehaviour
    {
        [SerializeField] private StateController       stateController;
        [SerializeField] private SkinnedMeshRenderer   skinnedRenderer;
        [SerializeField] private bool                  applyZRotationToTransform = true;

        [Header("Blendshape name per state (index 0 = State 1 = triangle, ..., index 3 = State 4 = square)")]
        [Tooltip("Leave a name empty if that state corresponds to the FBX base mesh (no blendshape needed).")]
        [SerializeField] private string[] blendshapeNamesByState = { "Triangle", "Hexagon", "Circle", "Square" };

        [Header("Manual override — drives the renderer directly when StateController is missing or this toggle is on")]
        [SerializeField] private bool   useManualOverride;
        [SerializeField] private Color  manualTint            = Color.white;
        [SerializeField, Range(0, 3)] private int   manualCurrentShape  = 0;
        [SerializeField, Range(0, 3)] private int   manualNextShape     = 0;
        [SerializeField, Range(0, 1)] private float manualTransitionT   = 0f;
        [SerializeField] private float manualRotationDegrees = 0f;
        [SerializeField, Range(0, 1)] private float manualPulseAmount   = 0f;

        private static readonly int ColorId        = Shader.PropertyToID("_Color");
        private static readonly int PulseAmountId  = Shader.PropertyToID("_PulseAmount");

        private MaterialPropertyBlock mpb;
        private int[] blendshapeIndicesByState;

        private void Awake()
        {
            if (skinnedRenderer == null) skinnedRenderer = GetComponent<SkinnedMeshRenderer>();
            mpb = new MaterialPropertyBlock();
            ResolveBlendshapeIndices();
        }

        private void OnValidate()
        {
            if (skinnedRenderer == null) skinnedRenderer = GetComponent<SkinnedMeshRenderer>();
            ResolveBlendshapeIndices();
        }

        [ContextMenu("Resolve Blendshape Indices")]
        public void ResolveBlendshapeIndices()
        {
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
            {
                blendshapeIndicesByState = null;
                return;
            }

            var mesh = skinnedRenderer.sharedMesh;
            blendshapeIndicesByState = new int[blendshapeNamesByState.Length];
            for (int i = 0; i < blendshapeNamesByState.Length; i++)
            {
                string name = blendshapeNamesByState[i];
                blendshapeIndicesByState[i] = string.IsNullOrEmpty(name) ? -1 : mesh.GetBlendShapeIndex(name);
            }
        }

        [ContextMenu("List Blendshape Names In Mesh")]
        public void LogAvailableBlendshapeNames()
        {
            if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
            {
                Debug.LogWarning("No SkinnedMeshRenderer or mesh assigned.", this);
                return;
            }
            var mesh = skinnedRenderer.sharedMesh;
            int n = mesh.blendShapeCount;
            if (n == 0)
            {
                Debug.LogWarning($"Mesh '{mesh.name}' has no blendshapes.", this);
                return;
            }
            var sb = new System.Text.StringBuilder($"Mesh '{mesh.name}' has {n} blendshapes:\n");
            for (int i = 0; i < n; i++) sb.AppendLine($"  [{i}] {mesh.GetBlendShapeName(i)}");
            Debug.Log(sb.ToString(), this);
        }

        private void LateUpdate()
        {
            if (skinnedRenderer == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            if (blendshapeIndicesByState == null || blendshapeIndicesByState.Length != blendshapeNamesByState.Length)
                ResolveBlendshapeIndices();

            int   current;
            int   next;
            float t;
            Color tint;
            float rotDegrees;
            float pulse;

            if (useManualOverride || stateController == null)
            {
                current    = manualCurrentShape;
                next       = manualNextShape;
                t          = manualTransitionT;
                tint       = manualTint;
                rotDegrees = manualRotationDegrees;
                pulse      = manualPulseAmount;
            }
            else
            {
                current    = stateController.CurrentShapeIndex;
                next       = stateController.NextShapeIndex;
                t          = stateController.MorphT;
                tint       = stateController.BlendedTint;
                tint.a    *= stateController.BlendedAlpha;
                rotDegrees = stateController.BlendedZRotationDegrees;
                pulse      = stateController.BlendedPulseAmount;
            }

            ApplyBlendshapeWeights(current, next, t);

            skinnedRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, tint);
            mpb.SetFloat(PulseAmountId, pulse);
            skinnedRenderer.SetPropertyBlock(mpb);

            if (applyZRotationToTransform)
            {
                var e = transform.localEulerAngles;
                transform.localEulerAngles = new Vector3(e.x, e.y, rotDegrees);
            }
        }

        // Sets blendshape weights such that the current state's shape contributes
        // (1-T)*100 and the next state's shape contributes T*100. If a state's
        // blendshape name is empty (-1), that state corresponds to the FBX base mesh
        // and no weight is applied for it — the absence of weight IS the base shape.
        private void ApplyBlendshapeWeights(int current, int next, float t)
        {
            if (blendshapeIndicesByState == null) return;

            for (int i = 0; i < blendshapeIndicesByState.Length; i++)
            {
                int idx = blendshapeIndicesByState[i];
                if (idx < 0) continue;
                skinnedRenderer.SetBlendShapeWeight(idx, 0f);
            }

            if (current == next)
            {
                SetWeightForState(current, 100f);
            }
            else
            {
                SetWeightForState(current, (1f - t) * 100f);
                SetWeightForState(next,    t        * 100f);
            }
        }

        private void SetWeightForState(int state, float weight)
        {
            if (blendshapeIndicesByState == null) return;
            if (state < 0 || state >= blendshapeIndicesByState.Length) return;
            int idx = blendshapeIndicesByState[state];
            if (idx < 0) return;
            skinnedRenderer.SetBlendShapeWeight(idx, weight);
        }
    }
}
