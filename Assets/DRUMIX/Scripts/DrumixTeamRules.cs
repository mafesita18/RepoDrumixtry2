namespace Drumix
{
    /// Traducción de la dificultad del equipo (DifficultyManager) a reglas DRUMIX. C# puro (con tests).
    public static class DrumixTeamRules
    {
        /// margen: margenErrorSegundos del equipo (0.30 / 0.20 / 0.12). velocidad: velocidadElementos (1 / 1.25 / 1.5).
        /// Valores negativos = no disponible. good = margen (0.08-0.40 s), perfect = mitad (máx. 0.10 s), bpm = base * velocidad (60-180).
        public static void FromTeamDifficulty(float margen, float velocidad, float baseBpm, out float good, out float perfect, out float bpm)
        {
            good = 0.22f; perfect = 0.10f; bpm = baseBpm;
            if (margen > 0f)
            {
                good = Clamp(margen, 0.08f, 0.40f);
                perfect = Clamp(good * 0.5f, 0.04f, 0.10f);
            }
            if (velocidad > 0f) bpm = Clamp(baseBpm * velocidad, 60f, 180f);
        }
        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
    }
}
