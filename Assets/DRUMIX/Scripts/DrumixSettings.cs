using UnityEngine;

namespace Drumix
{
    /// Ajustes y progreso guardados en PlayerPrefs (por equipo). Los valores y límites viven en DrumixSettingsData (C# puro).
    /// Quién los usa: volumen general (AudioListener), música (DrumixGame), efectos (DrumixPad), latencia (DrumixGame),
    /// vibración (DrumixStick), luces suaves (DrumixStageFX), objetos del público (DrumixThrowDirector), calibración (DrumixKitLock).
    public static class DrumixSettings
    {
        const string P = "DRUMIX.Settings.", Best = "DRUMIX.Best.", Seen = "DRUMIX.DanteSeen";
        static DrumixSettingsData current;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { current = null; }

        public static DrumixSettingsData Current { get { if (current == null) Load(); return current; } }

        public static void Load()
        {
            var d = new DrumixSettingsData();
            d.master = PlayerPrefs.GetFloat(P + "Master", d.master);
            d.music = PlayerPrefs.GetFloat(P + "Music", d.music);
            d.sfx = PlayerPrefs.GetFloat(P + "Sfx", d.sfx);
            d.latencyMs = PlayerPrefs.GetFloat(P + "Latency", d.latencyMs);
            d.haptics = PlayerPrefs.GetInt(P + "Haptics", d.haptics ? 1 : 0) == 1;
            d.softLights = PlayerPrefs.GetInt(P + "SoftLights", d.softLights ? 1 : 0) == 1;
            d.crowdProps = PlayerPrefs.GetInt(P + "CrowdProps", d.crowdProps ? 1 : 0) == 1;
            d.kitX = PlayerPrefs.GetFloat(P + "KitX", 0); d.kitY = PlayerPrefs.GetFloat(P + "KitY", 0);
            d.kitZ = PlayerPrefs.GetFloat(P + "KitZ", 0); d.kitYaw = PlayerPrefs.GetFloat(P + "KitYaw", 0);
            d.Sanitize();
            current = d;
            Apply();
        }

        public static void Save()
        {
            var d = Current; d.Sanitize();
            PlayerPrefs.SetFloat(P + "Master", d.master); PlayerPrefs.SetFloat(P + "Music", d.music); PlayerPrefs.SetFloat(P + "Sfx", d.sfx);
            PlayerPrefs.SetFloat(P + "Latency", d.latencyMs);
            PlayerPrefs.SetInt(P + "Haptics", d.haptics ? 1 : 0); PlayerPrefs.SetInt(P + "SoftLights", d.softLights ? 1 : 0);
            PlayerPrefs.SetInt(P + "CrowdProps", d.crowdProps ? 1 : 0);
            PlayerPrefs.SetFloat(P + "KitX", d.kitX); PlayerPrefs.SetFloat(P + "KitY", d.kitY);
            PlayerPrefs.SetFloat(P + "KitZ", d.kitZ); PlayerPrefs.SetFloat(P + "KitYaw", d.kitYaw);
            PlayerPrefs.Save();
            Apply();
        }

        /// Cambia un valor en vivo (sin guardar todavía).
        public static void Apply()
        {
            var d = current; if (d == null) return;
            d.Sanitize();
            AudioListener.volume = d.master;
            var g = DrumixGame.Instance; if (g) g.ApplyMusicVolume();
            foreach (var kit in DrumixUtil.FindAll<DrumixKitLock>()) if (kit) kit.SetCalibration(new Vector3(d.kitX, d.kitY, d.kitZ), d.kitYaw);
        }

        public static float BestPercent(string levelId) { return PlayerPrefs.GetFloat(Best + Key(levelId), 0f); }
        public static void ReportResult(string levelId, float percent)
        {
            float b = DrumixProgressRules.Better(BestPercent(levelId), percent);
            PlayerPrefs.SetFloat(Best + Key(levelId), b); PlayerPrefs.Save();
        }
        public static bool DanteSeen { get { return PlayerPrefs.GetInt(Seen, 0) == 1; } set { PlayerPrefs.SetInt(Seen, value ? 1 : 0); PlayerPrefs.Save(); } }
        /// Borra mejores % y vuelve a mostrar a Dante (para probar desde cero).
        public static void ResetProgress(System.Collections.Generic.IEnumerable<string> levelIds)
        {
            if (levelIds != null) foreach (var id in levelIds) PlayerPrefs.DeleteKey(Best + Key(id));
            PlayerPrefs.DeleteKey(Seen); PlayerPrefs.Save();
        }
        static string Key(string id) { return string.IsNullOrEmpty(id) ? "_" : id.Replace(' ', '_'); }
    }
}
