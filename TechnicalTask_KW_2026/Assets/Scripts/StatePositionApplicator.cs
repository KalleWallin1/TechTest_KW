using UnityEngine;

namespace TechnicalTask
{
    // Adds StateController.BlendedPositionOffset (XY) to the GameObject's base localPosition
    // each frame. Drop this on whichever transform should sway during state transitions.
    public class StatePositionApplicator : MonoBehaviour
    {
        [SerializeField] private StateController stateController;
        [SerializeField] private bool captureBasePositionAtAwake = true;
        [SerializeField] private Vector3 basePosition = Vector3.zero;

        private void Awake()
        {
            if (captureBasePositionAtAwake) basePosition = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (stateController == null) return;
            Vector2 offset = stateController.BlendedPositionOffset;
            transform.localPosition = basePosition + new Vector3(offset.x, offset.y, 0f);
        }

        [ContextMenu("Capture Current Position As Base")]
        public void CaptureCurrentPositionAsBase()
        {
            basePosition = transform.localPosition;
        }
    }
}
