using UnityEngine;
using UnityEngine.XR;

namespace Drumix
{
    /// Tracking mínimo (cabeza/manos) con la API XR estable. Solo se usa en el rig de respaldo,
    /// cuando el proyecto no tiene un prefab "XR Origin". Sin visor no hace nada (modo PC).
    [DefaultExecutionOrder(-100)]
    public class DrumixTrackedPose : MonoBehaviour
    {
        public XRNode node = XRNode.RightHand;
        void OnEnable() { Application.onBeforeRender += Apply; }
        void OnDisable() { Application.onBeforeRender -= Apply; }
        void Update() { Apply(); }
        void FixedUpdate() { Apply(); }
        void Apply()
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return;
            Vector3 p; Quaternion r;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out p)) transform.localPosition = p;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out r)) transform.localRotation = r;
        }
    }
}
