using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Drumix
{
    /// Un solo lugar para decidir "¿hay visor de verdad?" y para el simulador XR del equipo.
    /// Visor real = subsistema XR activo con ojo central válido (Quest por cable/Link/Air Link o build).
    /// El XR Device Simulator / XR Interaction Simulator NO cuenta como visor: crea dispositivos solo en el Input System.
    public static class DrumixXR
    {
        static readonly string[] SimulatorTypes = { "XRDeviceSimulator", "XRInteractionSimulator", "SimulatedDeviceLifecycleManager" };

        /// true cuando DrumixDesktopView decidió dejar que el simulador XR del equipo maneje la cámara (useXRSimulator).
        public static bool SimulatorMode { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { SimulatorMode = false; }

        public static bool HeadsetActive()
        {
            return XRSettings.isDeviceActive && InputDevices.GetDeviceAtXRNode(XRNode.CenterEye).isValid;
        }

        /// Vista VR (UI en panel world-space): visor real o simulador XR elegido a propósito.
        public static bool XRViewActive() { return HeadsetActive() || SimulatorMode; }

        static bool IsSimulator(MonoBehaviour mb)
        {
            if (!mb) return false;
            string n = mb.GetType().Name;
            foreach (var s in SimulatorTypes) if (n == s) return true;
            return false;
        }

        public static bool SimulatorPresent()
        {
            foreach (var mb in DrumixUtil.FindAll<MonoBehaviour>()) if (mb && mb.isActiveAndEnabled && IsSimulator(mb)) return true;
            return false;
        }

        /// Apaga (solo en Play, no se guarda) los simuladores XR activos. Devuelve los GameObjects apagados.
        public static List<GameObject> DisableSimulators()
        {
            var off = new List<GameObject>();
            foreach (var mb in DrumixUtil.FindAll<MonoBehaviour>())
                if (mb && mb.isActiveAndEnabled && IsSimulator(mb) && !off.Contains(mb.gameObject)) off.Add(mb.gameObject);
            foreach (var go in off) go.SetActive(false);
            return off;
        }

        /// Apaga los TrackedPoseDriver de la cámara (Input System o legacy) para que nada pelee con la vista de PC.
        public static List<Behaviour> DisableCameraTracking(Camera cam)
        {
            var off = new List<Behaviour>();
            if (!cam) return off;
            foreach (var b in cam.GetComponents<Behaviour>())
                if (b && b.enabled && b.GetType().Name.Contains("TrackedPoseDriver")) { b.enabled = false; off.Add(b); }
            return off;
        }

        /// Todos los XR Origin (Unity.XR.CoreUtils.XROrigin) de las escenas cargadas, activos o no.
        public static List<Transform> AllOrigins()
        {
            var list = new List<Transform>();
            var t = DrumixUtil.FindType("Unity.XR.CoreUtils.XROrigin");
            if (t == null) return list;
            foreach (var o in Resources.FindObjectsOfTypeAll(t))
            {
                var c = o as Component;
                if (c && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded) list.Add(c.transform);
            }
            return list;
        }

        /// "Camera Offset" del XR Origin (propiedad CameraFloorOffsetObject), o null.
        public static Transform CameraOffset(Transform origin)
        {
            if (!origin) return null;
            var t = DrumixUtil.FindType("Unity.XR.CoreUtils.XROrigin");
            var c = t != null ? origin.GetComponent(t) : null;
            if (!c) return null;
            var p = t.GetProperty("CameraFloorOffsetObject");
            var go = p != null ? p.GetValue(c, null) as GameObject : null;
            return go ? go.transform : null;
        }
    }
}
