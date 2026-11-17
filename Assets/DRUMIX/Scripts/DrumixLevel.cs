using UnityEngine;

namespace Drumix
{
    /// Nivel editable desde el Inspector (Assets/DRUMIX/Config/Niveles/*.asset).
    /// Aquí se pulen patrón, tempo, compases, ventanas de golpe y % para pasar SIN tocar código.
    [CreateAssetMenu(menuName = "DRUMIX/Nivel", fileName = "Nivel")]
    public class DrumixLevel : ScriptableObject
    {
        public DrumixLevelSpec spec = new DrumixLevelSpec();
        [Tooltip("Canción/loop opcional que suena durante el nivel (vacío = solo la batería).")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.5f;

        void OnValidate() { if (spec != null) spec.Sanitize(); }
    }
}
