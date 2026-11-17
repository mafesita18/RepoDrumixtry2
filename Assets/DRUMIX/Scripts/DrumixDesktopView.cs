using UnityEngine;
using UnityEngine.XR;

namespace Drumix
{
    /// Si NO hay visor conectado (Play en el PC), pone la cámara en una vista cómoda que ve
    /// la batería, el público y la UI. Con visor no toca nada.
    public class DrumixDesktopView : MonoBehaviour
    {
        public Camera target;
        [Tooltip("Si está, desktopPosition/desktopEuler son relativos a este Transform (DRUMIX_ROOT en escenas del equipo).")]
        public Transform space;
        [Tooltip("Sentado en la batería: ojos a 1.15 m, 0.45 m detrás de la caja.")]
        public Vector3 desktopPosition = new Vector3(0f, 1.15f, -0.45f);
        [Tooltip("15° hacia abajo (en Unity, X positivo mira hacia abajo).")]
        public Vector3 desktopEuler = new Vector3(15f, 0f, 0f);
        public float desktopFov = 70f;
        public float waitForHeadset = 0.6f;
        float timer; bool decided;

        void Start()
        {
            if (!target) target = GetComponent<Camera>() ? GetComponent<Camera>() : Camera.main;
            if (Application.isMobilePlatform) decided = true; // Quest/Android: nunca tocar la cámara
        }
        bool desktop;
        void LateUpdate()
        {
            // En modo PC se re-aplica cada frame: el TrackedPoseDriver del XR Origin no la mueve.
            if (desktop && XRSettings.isDeviceActive && InputDevices.GetDeviceAtXRNode(XRNode.CenterEye).isValid) desktop = false; // se conectó el visor
            if (desktop && target) { Apply(); return; }
            if (decided || !target) return;
            timer += Time.unscaledDeltaTime;
            if (XRSettings.isDeviceActive && InputDevices.GetDeviceAtXRNode(XRNode.CenterEye).isValid) { decided = true; return; }
            if (timer < waitForHeadset) return;
            decided = true; desktop = true;
            Apply();
            target.fieldOfView = desktopFov;
            Debug.Log("[DRUMIX] Sin visor: vista de PC. Enter = empezar, A S D F G H J K = tocar.");
        }
        void Apply()
        {
            Vector3 p = space ? space.TransformPoint(desktopPosition) : desktopPosition;
            Quaternion r = space ? Quaternion.Euler(0f, space.eulerAngles.y, 0f) * Quaternion.Euler(desktopEuler) : Quaternion.Euler(desktopEuler);
            target.transform.SetPositionAndRotation(p, r);
        }
        public bool IsDesktop { get { return desktop && !XRSettings.isDeviceActive; } }
    }
}
