using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Decide UNA vez al dar Play quién maneja la cámara (y lo cambia si se conecta/desconecta el visor):
    ///  · Visor real (Quest por Link/Air Link o build): no toca nada; el XR Origin ya está sentado en la batería.
    ///  · Sin visor (Play en el PC): "vista de PC" sentada en la batería. Apaga en Play el XR Device Simulator /
    ///    XR Interaction Simulator del equipo y el TrackedPoseDriver de la cámara (si no, pelean con esta vista y la
    ///    imagen parpadea, y sus teclas WASD chocan con A S D F G H J K), y esconde las manos/controles del rig.
    ///    Nada se guarda: al salir de Play todo vuelve como estaba.
    ///  · useXRSimulator = true: para probar con el simulador del equipo. DRUMIX no mueve la cámara; solo deja la
    ///    cabeza a altura de sentado (una vez) y el simulador manda. La UI queda en panel world-space.
    public class DrumixDesktopView : MonoBehaviour
    {
        public Camera target;
        [Tooltip("Si está, desktopPosition/desktopEuler son relativos a este Transform (DRUMIX_ROOT en escenas del equipo).")]
        public Transform space;
        [Tooltip("XR Origin del equipo (sus manos/controles se esconden en la vista de PC).")]
        public Transform rig;
        [Tooltip("Sentado en la batería: ojos a 1.15 m, 0.45 m detrás de la caja.")]
        public Vector3 desktopPosition = new Vector3(0f, 1.15f, -0.45f);
        [Tooltip("15° hacia abajo (en Unity, X positivo mira hacia abajo).")]
        public Vector3 desktopEuler = new Vector3(15f, 0f, 0f);
        public float desktopFov = 70f;
        public float waitForHeadset = 0.6f;
        [Tooltip("Probar con el XR Device Simulator del equipo en vez de la vista de PC (teclado A S D F G H J K).")]
        public bool useXRSimulator = false;

        float timer; bool decided, desktop;
        readonly List<GameObject> simulatorsOff = new List<GameObject>();
        readonly List<Behaviour> trackingOff = new List<Behaviour>();
        readonly List<Renderer> handsOff = new List<Renderer>();

        void Start()
        {
            if (!target) target = GetComponent<Camera>() ? GetComponent<Camera>() : Camera.main;
            if (Application.isMobilePlatform) decided = true; // Quest/Android: nunca tocar la cámara
        }

        void LateUpdate()
        {
            if (desktop && DrumixXR.HeadsetActive()) { ExitDesktop(); return; } // se conectó el visor
            if (desktop && target) { Apply(); return; }
            if (decided || !target) return;
            timer += Time.unscaledDeltaTime;
            if (DrumixXR.HeadsetActive()) { decided = true; return; }
            if (timer < waitForHeadset) return;
            decided = true;
            if (useXRSimulator && DrumixXR.SimulatorPresent()) { EnterSimulator(); return; }
            EnterDesktop();
        }

        void EnterDesktop()
        {
            desktop = true;
            simulatorsOff.AddRange(DrumixXR.DisableSimulators());
            trackingOff.AddRange(DrumixXR.DisableCameraTracking(target));
            if (rig)
                foreach (var r in rig.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled && !r.transform.IsChildOf(target.transform)) { r.enabled = false; handsOff.Add(r); }
            // Las baquetas siguen a los controles, que sin visor quedan quietos en el piso del rig: se esconden en la vista de PC.
            foreach (var stick in DrumixUtil.FindAll<DrumixStick>())
                foreach (var r in stick.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled && !handsOff.Contains(r)) { r.enabled = false; handsOff.Add(r); }
            Apply();
            target.fieldOfView = desktopFov;
            Debug.Log("[DRUMIX] Sin visor: vista de PC sentada en la batería" + (simulatorsOff.Count > 0 ? " (simulador XR apagado mientras juegas; para usarlo marca 'useXRSimulator' en 'Vista PC (DRUMIX)')" : "") +
                      ". Enter = empezar, A S D F G H J K = tocar.");
        }

        void ExitDesktop()
        {
            desktop = false;
            foreach (var b in trackingOff) if (b) b.enabled = true;
            foreach (var r in handsOff) if (r) r.enabled = true;
            trackingOff.Clear(); handsOff.Clear(); // los simuladores se quedan apagados: ya hay visor real
            Debug.Log("[DRUMIX] Visor conectado: la cámara vuelve al visor.");
        }

        void EnterSimulator()
        {
            DrumixXR.SimulatorMode = true;
            // Cabeza a altura de sentado: si el rig quedó con los ojos en el piso o muy alto, se corrige UNA vez el Camera Offset.
            var offset = DrumixXR.CameraOffset(rig);
            if (offset && space)
            {
                float want = space.TransformPoint(new Vector3(0f, desktopPosition.y, 0f)).y;
                float have = target.transform.position.y;
                if (Mathf.Abs(have - want) > 0.35f) offset.position += Vector3.up * (want - have);
            }
            Debug.Log("[DRUMIX] Simulador XR: el rig del equipo manda la cámara (sentado en la batería). UI en panel VR.");
        }

        void Apply()
        {
            Vector3 p = space ? space.TransformPoint(desktopPosition) : desktopPosition;
            Quaternion r = space ? Quaternion.Euler(0f, space.eulerAngles.y, 0f) * Quaternion.Euler(desktopEuler) : Quaternion.Euler(desktopEuler);
            target.transform.SetPositionAndRotation(p, r);
        }

        public bool IsDesktop { get { return desktop && !DrumixXR.HeadsetActive(); } }
    }
}
