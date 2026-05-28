using UnityEngine;

namespace TechnicalTask
{
    // Multiplies the GameObject's localScale by StateController.BlendedScaleMultiplier
    // each frame. Drop this on the shape and/or the digit cube (or a shared parent) to
    // make them shrink/grow during state transitions.
    public class StateScaleApplicator : MonoBehaviour
    {
        [SerializeField] private StateController stateController;
        [SerializeField] private bool captureBaseScaleAtAwake = true;
        [SerializeField] private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            if (captureBaseScaleAtAwake) baseScale = transform.localScale;
        }

        private void LateUpdate()
        {
            if (stateController == null) return;
            float m = stateController.BlendedScaleMultiplier;
            transform.localScale = baseScale * m;
        }

        [ContextMenu("Capture Current Scale As Base")]
        public void CaptureCurrentScaleAsBase()
        {
            baseScale = transform.localScale;
        }
    }
}
