using System;
using System.Collections.Generic;

namespace Drumix
{
    /// Estilo de patrón de la canción (C# puro, con tests).
    public enum DrumixPattern
    {
        /// Bombo y caja en negras, crash al empezar cada 4 compases. Sin redobles.
        Basico = 0,
        /// Rock de bar: como Básico + redoble de toms cada 4 compases + hi-hat/ride a veces.
        Rock = 1,
        /// Concierto: corcheas (hi-hat en el contratiempo), redobles cada 2 compases, ride y crash.
        Concierto = 2,
    }

    /// Un nivel jugable. Lo usan el ScriptableObject DrumixLevel (editable en el Inspector) y los tests.
    [Serializable]
    public class DrumixLevelSpec
    {
        public string name = "El Bar Local";
        public string subtitle = "";
        public DrumixPattern pattern = DrumixPattern.Rock;
        public float bpm = 88f;
        public int bars = 12;
        public int seed = 1234;
        public float perfectWindow = 0.10f;
        public float goodWindow = 0.22f;
        public float passPercent = 50f;
        public float leadTime = 1.4f;
        public int comboStep = 10;
        public DrumixLane[] lanes = (DrumixLane[])Enum.GetValues(typeof(DrumixLane));

        public DrumixLevelSpec Clone() { var c = (DrumixLevelSpec)MemberwiseClone(); c.lanes = lanes == null ? null : (DrumixLane[])lanes.Clone(); return c; }

        /// Valores sanos aunque alguien ponga cualquier cosa en el Inspector.
        public void Sanitize()
        {
            if (string.IsNullOrEmpty(name)) name = "Nivel";
            bpm = Clamp(bpm, 50f, 180f);
            bars = (int)Clamp(bars, 2, 64);
            perfectWindow = Clamp(perfectWindow, 0.03f, 0.25f);
            goodWindow = Clamp(goodWindow, perfectWindow, 0.40f);
            // con corcheas las notas están a media negra: la ventana no puede tapar a la siguiente nota
            float spacing = (pattern == DrumixPattern.Concierto ? 0.5f : 1f) * 60f / bpm;
            goodWindow = Math.Min(goodWindow, spacing * 0.45f);
            perfectWindow = Math.Min(perfectWindow, goodWindow);
            passPercent = Clamp(passPercent, 1f, 100f);
            leadTime = Clamp(leadTime, 0.5f, 4f);
            if (comboStep < 2) comboStep = 10;
            if (lanes == null || lanes.Length == 0) lanes = (DrumixLane[])Enum.GetValues(typeof(DrumixLane));
        }
        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
    }

    public static class DrumixLevelData
    {
        /// Los 3 niveles de DRUMIX (mismos nombres que en el diseño del equipo). Se crean como assets editables.
        public static DrumixLevelSpec[] Defaults()
        {
            return new[]
            {
                new DrumixLevelSpec { name = "El Garaje", subtitle = "Nivel 1 · calentando", pattern = DrumixPattern.Basico, bpm = 76, bars = 8, seed = 11,
                    perfectWindow = 0.12f, goodWindow = 0.28f, passPercent = 50, leadTime = 1.8f,
                    lanes = new[] { DrumixLane.Kick, DrumixLane.Snare, DrumixLane.HiHat, DrumixLane.Crash } },
                new DrumixLevelSpec { name = "El Bar Local", subtitle = "Nivel 2 · el público ya te mira", pattern = DrumixPattern.Rock, bpm = 88, bars = 12, seed = 1234,
                    perfectWindow = 0.10f, goodWindow = 0.22f, passPercent = 50, leadTime = 1.4f },
                new DrumixLevelSpec { name = "El Gran Concierto", subtitle = "Nivel 3 · estadio lleno", pattern = DrumixPattern.Concierto, bpm = 104, bars = 16, seed = 777,
                    perfectWindow = 0.07f, goodWindow = 0.13f, passPercent = 50, leadTime = 1.2f },
            };
        }

        /// Siguiente nivel tras terminar: si pasó avanza (sin pasarse del último); si no, repite.
        /// Escena del equipo de un nivel: "1.NivelUno" = 0, "2.NivelDos" = 1, "3.NivelTres" = 2 (también "Nivel1", "Level 2"...).
        /// -1 si el nombre no es de un nivel (SampleScene, BackUpNiveles, BasicScene, DRUMIX_Bar...).
        public static int LevelFromSceneName(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return -1;
            string n = sceneName.Trim();
            int slash = n.LastIndexOfAny(new[] { '/', '\\' }); if (slash >= 0) n = n.Substring(slash + 1);
            if (n.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 6);
            string low = n.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
            if (low.Contains("backup") || low.Contains("sample") || low.Contains("basic") || low.Contains("drumix")) return -1;
            int at = low.IndexOf("nivel", StringComparison.Ordinal); int len = 5;
            if (at < 0) { at = low.IndexOf("level", StringComparison.Ordinal); }
            if (at < 0) return -1;
            string rest = low.Substring(at + len);
            string[] words = { "uno", "dos", "tres", "cuatro", "cinco" };
            for (int i = 0; i < words.Length; i++) if (rest.StartsWith(words[i], StringComparison.Ordinal)) return i;
            int d = 0, k = 0;
            while (k < rest.Length && char.IsDigit(rest[k])) { d = d * 10 + (rest[k] - '0'); k++; }
            if (k > 0 && d >= 1) return d - 1;
            // "1.NivelX": número delante
            k = 0; d = 0;
            while (k < low.Length && char.IsDigit(low[k])) { d = d * 10 + (low[k] - '0'); k++; }
            return k > 0 && d >= 1 ? d - 1 : -1;
        }

        /// Escena que hay que cargar para jugar el nivel 'index' (null = se juega aquí mismo).
        public static string SceneToLoad(string[] levelScenes, int index, string currentScene)
        {
            if (levelScenes == null || index < 0 || index >= levelScenes.Length) return null;
            string s = levelScenes[index];
            if (string.IsNullOrEmpty(s) || string.Equals(s, currentScene, StringComparison.OrdinalIgnoreCase)) return null;
            return s;
        }

        /// Versión de DRUMIX guardada dentro de un .unity (texto YAML). -1 si la escena no tiene el objeto raíz.
        public static int BuiltVersionInSceneText(string sceneYaml, string rootName)
        {
            if (string.IsNullOrEmpty(sceneYaml) || string.IsNullOrEmpty(rootName)) return -1;
            if (sceneYaml.IndexOf("m_Name: " + rootName + "\n", StringComparison.Ordinal) < 0
                && sceneYaml.IndexOf("m_Name: " + rootName + "\r", StringComparison.Ordinal) < 0) return -1;
            int best = 0; const string key = "builtVersion: ";
            for (int at = sceneYaml.IndexOf(key, StringComparison.Ordinal); at >= 0; at = sceneYaml.IndexOf(key, at + 1, StringComparison.Ordinal))
            {
                int k = at + key.Length, v = 0, digits = 0;
                while (k < sceneYaml.Length && char.IsDigit(sceneYaml[k]) && digits < 6) { v = v * 10 + (sceneYaml[k] - '0'); k++; digits++; }
                if (digits > 0 && v > best) best = v;
            }
            return best;
        }

        public static int NextIndex(int current, int count, bool passed)
        {
            if (count <= 0) return 0;
            if (current < 0) current = 0;
            if (current >= count) current = count - 1;
            return passed && current < count - 1 ? current + 1 : current;
        }
    }
}
