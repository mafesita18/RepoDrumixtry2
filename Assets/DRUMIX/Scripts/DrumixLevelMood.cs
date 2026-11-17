using UnityEngine;
using UnityEngine.Rendering;

namespace Drumix
{
    /// Ambiente del nivel (niebla, luz ambiente, fondo) SOLO al dar Play: la configuración de iluminación de la escena
    /// del equipo no se toca en el archivo y se restaura al salir de Play.
    public class DrumixLevelMood : MonoBehaviour
    {
        [Header("Niebla")]
        public bool fog = true;
        public Color fogColor = new Color(0.07f, 0.04f, 0.11f);
        [Range(0f, 0.1f)] public float fogDensity = 0.02f;
        [Header("Luz ambiente (degradado)")]
        public bool overrideAmbient = true;
        public Color ambientSky = new Color(0.30f, 0.20f, 0.45f);
        public Color ambientEquator = new Color(0.18f, 0.10f, 0.26f);
        public Color ambientGround = new Color(0.06f, 0.04f, 0.08f);
        [Header("Fondo (de noche: color sólido en vez del cielo)")]
        public bool solidBackground = false;
        public Color background = new Color(0.035f, 0.02f, 0.06f);
        [Tooltip("Cámara a la que se le cambia el fondo (vacío = Camera.main).")]
        public Camera targetCamera;

        bool applied, oldFog; Color oldFogColor, oldSky, oldEq, oldGround; float oldDensity; FogMode oldMode; AmbientMode oldAmbient;
        Material oldSkybox; Camera cam; CameraClearFlags oldClear; Color oldBg;

        void OnEnable()
        {
            if (!Application.isPlaying || applied) return;
            applied = true;
            oldFog = RenderSettings.fog; oldFogColor = RenderSettings.fogColor; oldDensity = RenderSettings.fogDensity; oldMode = RenderSettings.fogMode;
            oldAmbient = RenderSettings.ambientMode; oldSky = RenderSettings.ambientSkyColor; oldEq = RenderSettings.ambientEquatorColor; oldGround = RenderSettings.ambientGroundColor;
            oldSkybox = RenderSettings.skybox;
            RenderSettings.fog = fog;
            if (fog) { RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity; }
            if (overrideAmbient)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator; RenderSettings.ambientGroundColor = ambientGround;
            }
            if (solidBackground)
            {
                cam = targetCamera ? targetCamera : Camera.main;
                RenderSettings.skybox = null;
                if (cam) { oldClear = cam.clearFlags; oldBg = cam.backgroundColor; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = background; }
            }
        }

        void OnDestroy()
        {
            if (!applied) return;
            RenderSettings.fog = oldFog; RenderSettings.fogColor = oldFogColor; RenderSettings.fogDensity = oldDensity; RenderSettings.fogMode = oldMode;
            RenderSettings.ambientMode = oldAmbient; RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEq; RenderSettings.ambientGroundColor = oldGround;
            RenderSettings.skybox = oldSkybox;
            if (cam) { cam.clearFlags = oldClear; cam.backgroundColor = oldBg; }
        }
    }
}
