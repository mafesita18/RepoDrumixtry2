using System;

namespace Drumix
{
    /// Reglas de tamaño (C# puro, con tests). Todo se mide por bounds reales (no por la escala del FBX),
    /// así da igual si el modelo se importó en cm, m o con la escala de Blender.
    public static class DrumixScaleRules
    {
        public const float PersonMin = 1.60f, PersonMax = 1.85f;
        public const float KitSnareHeight = 0.65f;   // altura del parche de la caja sobre el piso
        public const float KitTargetWidth = 1.40f;   // ancho de la batería (cascos)
        public const float KitMaxWidth = 1.80f;      // con platillos nunca más ancha que esto
        public const float SeatedEyeHeight = 1.15f, SeatedBack = 0.45f, SeatedPitch = 15f;

        /// Altura objetivo de una persona del público (variación estable por índice).
        public static float PersonHeight(int index)
        {
            float t = ((index * 7919) % 100) / 99f;
            return PersonMin + (PersonMax - PersonMin) * t;
        }

        /// Factor para que algo que mide 'measured' pase a medir 'target' (1 si la medida es inválida).
        public static float Fit(float measured, float target)
        {
            if (!(measured > 1e-4f) || float.IsInfinity(measured) || !(target > 0f)) return 1f;
            return target / measured;
        }

        /// ¿Hace falta corregir? (fuera del rango de una persona normal, con 2 % de margen).
        public static bool PersonNeedsFix(float measuredHeight)
        {
            return !(measuredHeight >= PersonMin * 0.98f && measuredHeight <= PersonMax * 1.02f);
        }

        /// ¿El NPC armado es una persona creíble? (altura 1.2–2.4 m, pies cerca del piso, centrado, escala finita).
        /// Si no, el builder pone una persona de primitivas en vez de dejar un gigante/enano invisible.
        public static bool PersonPlausible(float height, float minY, float centerX, float centerZ, float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f || scale > 500f) return false;
            if (!(height >= 1.2f && height <= 2.4f)) return false;
            return Math.Abs(minY) <= 0.2f && Math.Abs(centerX) <= 1f && Math.Abs(centerZ) <= 1f;
        }

        /// Escala de la batería: la caja a 0.65 m del piso (lo que importa para tocar) sin pasar de 1.8 m de ancho.
        public static float KitScale(float snareHeight, float width)
        {
            float s = Fit(snareHeight, KitSnareHeight);
            if (width > 1e-4f && width * s > KitMaxWidth) s = KitMaxWidth / width;
            return s < 0.05f ? 0.05f : s > 20f ? 20f : s;
        }
    }
}
