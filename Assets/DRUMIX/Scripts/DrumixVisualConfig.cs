using UnityEngine;

namespace Drumix
{
    /// Pulido visual editable desde el Inspector (Assets/DRUMIX_Config/Visual.asset). ARMAR TODO lo lee.
    [CreateAssetMenu(menuName = "DRUMIX/Config visual", fileName = "Visual")]
    public class DrumixVisualConfig : ScriptableObject
    {
        public const int CurrentVersion = 2;
        [HideInInspector] public int version = CurrentVersion;
        [Header("Luz ambiente (degradado, sin costo en Quest)")]
        public Color ambientSky = new Color(0.24f, 0.18f, 0.34f);
        public Color ambientEquator = new Color(0.13f, 0.09f, 0.19f);
        public Color ambientGround = new Color(0.04f, 0.03f, 0.05f);
        [Header("Luz principal (direccional, sin sombras)")]
        [Range(0f, 3f)] public float directionalIntensity = 1.2f;
        public Color directionalColor = new Color(1f, 0.93f, 0.85f);
        [Header("Fondo y niebla")]
        public Color background = new Color(0.035f, 0.02f, 0.06f);
        public bool fog = true;
        public Color fogColor = new Color(0.07f, 0.04f, 0.11f);
        [Range(0f, 0.08f)] public float fogDensity = 0.015f;
        [Header("Luces de escenario (pocas, sin sombras)")]
        [Range(0f, 3f)] public float stageLightMultiplier = 1f;
        [Header("Post-procesado (bloom suave + color)")]
        public bool postProcessingPC = true;
        [Tooltip("En Quest el post-procesado cuesta un pase extra de pantalla completa. Apagado por defecto.")]
        public bool postProcessingQuest = false;
        [Range(0f, 2f)] public float bloomIntensity = 0.45f;
        [Range(0.5f, 2f)] public float bloomThreshold = 1.0f;
        [Range(-50f, 50f)] public float saturation = 12f;
        [Header("Partículas")]
        public bool hitParticles = true;
        public bool missParticles = true;
        public bool comboParticles = true;
        public bool throwParticles = true;
        public bool winConfetti = true;
        public Color perfectColor = new Color(1f, 0.82f, 0.35f);
        public Color goodColor = new Color(0.25f, 0.6f, 1f);
        /// Sube un Visual.asset viejo a los valores nuevos de luz/colores (solo una vez; luego manda lo que pongas tú).
        public bool Upgrade()
        {
            if (version >= CurrentVersion) return false;
            var d = CreateInstance<DrumixVisualConfig>();
            ambientSky = d.ambientSky; ambientEquator = d.ambientEquator; ambientGround = d.ambientGround;
            directionalIntensity = d.directionalIntensity; directionalColor = d.directionalColor;
            fogDensity = d.fogDensity; goodColor = d.goodColor; perfectColor = d.perfectColor; missColor = d.missColor;
            if (Application.isPlaying) Destroy(d); else DestroyImmediate(d);
            version = CurrentVersion;
            return true;
        }
        public Color missColor = new Color(1f, 0.2f, 0.25f);
        public Color comboColor = new Color(0.8f, 0.45f, 1f);
        [Range(8, 128)] public int maxParticlesPerEffect = 48;
    }
}
