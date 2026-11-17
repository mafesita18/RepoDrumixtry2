// C# PURO (sin UnityEngine): reglas de qué cuenta como golpe. Las usa DrumixPad y las prueban los tests.
namespace Drumix
{
    public static class DrumixHitRules
    {
        /// ¿Este contacto cuenta como golpe? Un proyectil del público NUNCA cuenta, pase lo que pase.
        public static bool ShouldCount(bool isProjectile, bool isOtherPad, bool hasStick, bool acceptAnyCollider, bool looksLikeStick, float stickSpeed, float minStickSpeed)
        {
            if (isProjectile || isOtherPad) return false;
            if (hasStick) return stickSpeed >= minStickSpeed;
            return acceptAnyCollider || looksLikeStick;
        }

        /// Antirrebote: ignora golpes demasiado seguidos en la misma pieza.
        public static bool IntervalOk(float now, float lastStrike, float minInterval) { return now - lastStrike >= minInterval; }

        /// No duplicar sonido: si el DrumPad del equipo ya sonó por el contacto físico, nosotros callamos.
        public static bool ShouldPlaySound(bool physical, bool hasTeamPad, bool muteIfTeamPad) { return !(physical && hasTeamPad && muteIfTeamPad); }

        public static bool LooksLikeStickName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("joystick")) return false;
            return n.Contains("stick") || n.Contains("baqueta") || n.Contains("palillo");
        }

        /// Fuerza 0.3-1 según la velocidad de la baqueta.
        public static float Strength(float stickSpeed)
        {
            float k = stickSpeed / 3f; if (k < 0) k = 0; if (k > 1) k = 1;
            return k * 0.7f + 0.3f;
        }
    }
}
