using UnityEngine;

namespace TechnicalTask
{
    // Rotates this GameObject's Transform to face the target camera each LateUpdate. Drop
    // on MarkerRoot (or any world-space marker container) so its contents always present
    // a readable face to the viewer regardless of camera position.
    //
    // The default Y-axis-only billboard is recommended for HMI markers — keeps the marker
    // upright (no roll/pitch) while still rotating to face the camera. Especially important
    // in VR, where pitch/roll billboarding feels disorienting as the user looks around.
    [ExecuteAlways]
    public class BillboardToCamera : MonoBehaviour
    {
        [Tooltip("Camera to face. Defaults to Camera.main if left empty. In VR this is normally the XR Origin's head camera.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("If true, only rotates around the world Y axis — marker stays upright. Best for HMI markers and VR. If false, full 3-axis billboard (marker tilts to fully face camera).")]
        [SerializeField] private bool yAxisOnly = true;

        [Tooltip("Flip if the marker's face points the wrong way after billboarding. The default rotates so local +Z points at the camera.")]
        [SerializeField] private bool flipFacing = false;

        private void LateUpdate()
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null) return;

            Vector3 toCamera = cam.transform.position - transform.position;
            if (yAxisOnly) toCamera.y = 0f;
            if (toCamera.sqrMagnitude < 1e-6f) return;

            transform.rotation = Quaternion.LookRotation(flipFacing ? -toCamera : toCamera, Vector3.up);
        }
    }
}
