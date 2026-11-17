using System;
using UnityEngine;

namespace Drumix
{
    /// En la cámara: activa/desactiva el post-procesado de URP según plataforma (PC sí, Quest según Config visual).
    /// Por reflexión: compila aunque no esté URP.
    public class DrumixVisualRuntime : MonoBehaviour
    {
        public DrumixVisualConfig config;

        void Start()
        {
            bool quest = Application.platform == RuntimePlatform.Android;
            bool want = config ? (quest ? config.postProcessingQuest : config.postProcessingPC) : !quest;
            SetPostProcessing(GetComponent<Camera>(), want);
        }

        public static bool SetPostProcessing(Camera cam, bool on)
        {
            if (!cam) return false;
            Type t = DrumixUtil.FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (t == null) return false;
            var data = cam.GetComponent(t);
            if (!data) data = cam.gameObject.AddComponent(t);
            var p = t.GetProperty("renderPostProcessing");
            if (p == null || !p.CanWrite) return false;
            try { p.SetValue(data, on, null); return true; } catch (Exception) { return false; }
        }
    }
}
