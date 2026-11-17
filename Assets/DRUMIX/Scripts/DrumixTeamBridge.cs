using System;
using System.Reflection;
using UnityEngine;

namespace Drumix
{
    /// Puente OPCIONAL con los scripts del equipo (AudioManager, LevelManager, DifficultyManager...).
    /// Cero dependencias: todo por reflexión, y si no existen no pasa nada.
    /// Formato de llamadas: "Clase.Metodo" (método público sin parámetros), p. ej. "LevelManager.NextLevel".
    public class DrumixTeamBridge : MonoBehaviour
    {
        public bool logDetected = true;
        [Tooltip("Usa el DifficultyManager del equipo (MargenError y VelocidadElementos del nivel actual) para las ventanas de acierto y el tempo.")]
        public bool useTeamDifficulty = true;
        [Tooltip("Al terminar la canción suena el SFX de ganar/perder del AudioManager del equipo (si está en la escena).")]
        public bool useTeamAudioManager = true;
        [Tooltip("Si en la escena hay un SequenceManager del equipo, cada fallo de DRUMIX llama su ValidarError(). DRUMIX sigue siendo quien juzga (no hay dos secuencias).")]
        public bool forwardMissesToTeamSequence = true;
        [Header("Sonidos del AudioManager del equipo (Tugui): se usan los suyos, no se duplican")]
        [Tooltip("El clic de los botones DRUMIX suena con SFX_ClicBoton del AudioManager (si tiene clip). Si no hay AudioManager, suena el clic de DRUMIX.")]
        public bool teamUiClick = true;
        [Tooltip("SFX_Exito en cada racha (combo) y SFX_Error al fallar una nota (con pausa mínima para no saturar).")]
        public bool teamHitSfx = true;
        [Min(0f)] public float errorSfxCooldown = 0.8f;
        [Tooltip("Si un nivel DRUMIX trae su propia música, el ambiente del AudioManager se pausa mientras suena la canción y vuelve en el menú/resultados.")]
        public bool pauseTeamAmbientForMusic = true;
        public string[] onSongStart = new string[0];
        public string[] onPass = new string[0];
        public string[] onFail = new string[0];

        void Start()
        {
            if (!logDetected) return;
            string found = "";
            foreach (var n in new[] { "AudioManager", "LevelManager", "DifficultyManager", "DrumPad", "BeatManager" })
                if (DrumixUtil.FindType(n) != null) found += n + " ";
            Debug.Log("[DRUMIX] Scripts del equipo detectados (se respetan, no se reemplazan): " + (found.Length > 0 ? found : "ninguno"));
        }
        static DrumixTeamBridge active;
        float lastErrorSfx = -10f; bool ambientPaused;
        void OnEnable() { active = this; DrumixEvents.OnStateChanged += State; DrumixEvents.OnSongFinished += Finished; DrumixEvents.OnMiss += Miss; DrumixEvents.OnCombo += Combo; }
        void OnDisable()
        {
            if (active == this) active = null;
            DrumixEvents.OnStateChanged -= State; DrumixEvents.OnSongFinished -= Finished; DrumixEvents.OnMiss -= Miss; DrumixEvents.OnCombo -= Combo;
            if (ambientPaused) { ambientPaused = false; CallIfPresent("AudioManager", "ReproducirAmbiente"); }
        }

        /// Lo llama DrumixUI al hacer clic: true = ya sonó el clic del equipo (DRUMIX no suena el suyo, nada doble).
        public static bool RouteClick()
        {
            if (!active || !active.isActiveAndEnabled || !active.teamUiClick || !TeamHasClip("sfxClicBoton")) return false;
            return CallIfPresent("AudioManager", "ReproducirSFXClicBoton");
        }
        void Combo(int combo) { if (teamHitSfx && TeamHasClip("sfxExito")) CallIfPresent("AudioManager", "ReproducirSFXExito"); }

        /// ¿El AudioManager del equipo existe y tiene ese clip asignado? (campo privado serializado, por reflexión)
        static bool TeamHasClip(string field)
        {
            object am = FindInstance("AudioManager");
            if (am == null) return false;
            try
            {
                var f = am.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                return f != null && f.GetValue(am) as AudioClip;
            }
            catch (Exception) { return false; }
        }

        void State(DrumixState s)
        {
            if (pauseTeamAmbientForMusic)
            {
                var g = DrumixGame.Instance; var lvl = g ? g.CurrentLevel : null;
                if (s == DrumixState.Countdown && lvl && lvl.music && !ambientPaused) ambientPaused = CallIfPresent("AudioManager", "DetenerAmbiente");
                else if ((s == DrumixState.Menu || s == DrumixState.Results) && ambientPaused) { ambientPaused = false; CallIfPresent("AudioManager", "ReproducirAmbiente"); }
            }
            if (s != DrumixState.Countdown) return;
            teamSequence = forwardMissesToTeamSequence ? FindInstance("SequenceManager") as UnityEngine.Object : null;
            if (teamSequence && logDetected) Debug.Log("[DRUMIX] SequenceManager del equipo conectado: sus ValidarError() reciben los fallos de DRUMIX.");
            CallAll(onSongStart);
        }

        UnityEngine.Object teamSequence;
        bool forwarding;
        /// Solo fallos de NOTA (no los 'stray', que es lo que reporta el propio SequenceManager): así nunca hay bucle.
        void Miss(DrumixHitInfo info)
        {
            if (teamHitSfx && !info.stray && Time.unscaledTime - lastErrorSfx >= errorSfxCooldown && TeamHasClip("sfxError"))
            { lastErrorSfx = Time.unscaledTime; CallIfPresent("AudioManager", "ReproducirSFXError"); }
            if (!forwardMissesToTeamSequence || info.stray || forwarding || !teamSequence) return;
            forwarding = true;
            try
            {
                var m = teamSequence.GetType().GetMethod("ValidarError", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m != null) m.Invoke(teamSequence, null);
            }
            catch (Exception e) { Debug.LogWarning("[DRUMIX] SequenceManager.ValidarError falló: " + e.Message); }
            finally { forwarding = false; }
        }
        void Finished(float p, bool passed)
        {
            if (useTeamAudioManager) CallIfPresent("AudioManager", passed ? "ReproducirSFXGanarNivel" : "ReproducirSFXPerderNivel");
            CallAll(passed ? onPass : onFail);
        }

        /// Lo llama DrumixGame.StartSong antes de armar la canción.
        public void BeforeSong(DrumixGame game)
        {
            if (!useTeamDifficulty || !game) return;
            object dm = FindInstance("DifficultyManager");
            if (dm == null) return;
            float margen, velocidad;
            bool hasMargen = TryGetFloat(dm, "MargenError", out margen);
            bool hasVel = TryGetFloat(dm, "VelocidadElementos", out velocidad);
            if (!hasMargen && !hasVel) return;
            if (baseBpm <= 0f) baseBpm = game.bpm;
            float good, perfect, bpm;
            DrumixTeamRules.FromTeamDifficulty(hasMargen ? margen : -1f, hasVel ? velocidad : -1f, baseBpm, out good, out perfect, out bpm);
            if (hasMargen) { game.goodWindow = good; game.perfectWindow = perfect; }
            if (hasVel) game.bpm = bpm;
            if (logDetected) Debug.Log("[DRUMIX] Dificultad del equipo: margen " + (hasMargen ? margen.ToString("0.00") : "-") + " s, velocidad x" + (hasVel ? velocidad.ToString("0.00") : "-") + " -> ventanas " + perfect.ToString("0.00") + "/" + good.ToString("0.00") + " s, " + game.bpm.ToString("0") + " bpm");
        }
        float baseBpm = -1f;

        static object FindInstance(string typeName)
        {
            Type type = DrumixUtil.FindType(typeName);
            if (type == null) return null;
            try
            {
                var inst = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                object target = inst != null ? inst.GetValue(null, null) : null;
                if (target == null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                {
                    var all = DrumixUtil.FindAll(type);
                    if (all.Length > 0) target = all[0];
                }
                return target;
            }
            catch (Exception) { return null; }
        }

        static bool TryGetFloat(object target, string property, out float value)
        {
            value = 0f;
            try
            {
                var p = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
                if (p == null) return false;
                object v = p.GetValue(target, null);
                if (v is float) { value = (float)v; return true; }
                if (v is int) { value = (int)v; return true; }
            }
            catch (Exception) { }
            return false;
        }

        /// Llama Clase.Metodo() solo si la clase y una instancia existen (sin warnings si no están).
        public static bool CallIfPresent(string typeName, string method)
        {
            object target = FindInstance(typeName);
            if (target == null) return false;
            try
            {
                MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (m == null) return false;
                m.Invoke(target, null);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[DRUMIX] Falló " + typeName + "." + method + ": " + e.Message); return false; }
        }

        void CallAll(string[] calls) { if (calls != null) foreach (var c in calls) Call(c); }

        public static bool Call(string call)
        {
            if (string.IsNullOrEmpty(call) || call.IndexOf('.') < 0) return false;
            int dot = call.LastIndexOf('.');
            string typeName = call.Substring(0, dot), method = call.Substring(dot + 1);
            try
            {
                Type type = DrumixUtil.FindType(typeName);
                if (type == null) { Debug.LogWarning("[DRUMIX] No existe la clase " + typeName); return false; }
                MethodInfo m = type.GetMethod(method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m == null) { Debug.LogWarning("[DRUMIX] " + typeName + " no tiene " + method + "()"); return false; }
                object target = null;
                if (!m.IsStatic)
                {
                    var inst = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    target = inst != null ? inst.GetValue(null, null) : null;
                    if (target == null)
                    {
                        var field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                        target = field != null ? field.GetValue(null) : null;
                    }
                    if (target == null && typeof(UnityEngine.Object).IsAssignableFrom(type))
                    {
#if UNITY_2023_1_OR_NEWER
                        target = UnityEngine.Object.FindAnyObjectByType(type);
#else
                        target = UnityEngine.Object.FindObjectOfType(type);
#endif
                    }
                    if (target == null) { Debug.LogWarning("[DRUMIX] No hay instancia de " + typeName + " en la escena"); return false; }
                }
                m.Invoke(target, null);
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[DRUMIX] Falló " + call + ": " + e.Message); return false; }
        }
    }
}
