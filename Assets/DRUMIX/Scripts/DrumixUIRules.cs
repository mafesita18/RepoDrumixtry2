using System;
using System.Collections.Generic;

// C# PURO (sin UnityEngine): reglas de la UI (flujo de pantallas, progreso, puntos, ajustes, diálogo de Dante
// y posiciones del HUD). Las usa DrumixUI y las prueban los tests headless (Tests/LogicTests).
namespace Drumix
{
    public enum DrumixScreen { Menu, Dante, Levels, Settings, Calibration, Countdown, Hud, Pause, Results }

    /// Qué pantalla se ve. El estado del juego (Menu/Countdown/Playing/Results + pausa) manda; encima de eso
    /// el menú abre Dante / Niveles / Ajustes / Calibración y "Volver" regresa a donde estabas (Ajustes desde Pausa vuelve a Pausa).
    public sealed class DrumixUIFlow
    {
        public DrumixScreen Screen { get; private set; } = DrumixScreen.Menu;
        DrumixScreen back = DrumixScreen.Menu;

        public static bool IsGameplay(DrumixScreen s) { return s == DrumixScreen.Countdown || s == DrumixScreen.Hud; }
        /// El HUD (puntaje arriba + carril) se ve también durante la cuenta 3-2-1 y detrás de la pausa.
        public static bool ShowsHud(DrumixScreen s) { return s == DrumixScreen.Countdown || s == DrumixScreen.Hud || s == DrumixScreen.Pause; }

        public void GameState(DrumixState state, bool paused)
        {
            switch (state)
            {
                case DrumixState.Menu:
                    if (Screen == DrumixScreen.Countdown || Screen == DrumixScreen.Hud || Screen == DrumixScreen.Pause || Screen == DrumixScreen.Results) Screen = DrumixScreen.Menu;
                    break;
                case DrumixState.Countdown: Screen = paused ? DrumixScreen.Pause : DrumixScreen.Countdown; break;
                case DrumixState.Playing: Screen = paused ? DrumixScreen.Pause : DrumixScreen.Hud; break;
                case DrumixState.Results: Screen = DrumixScreen.Results; break;
            }
        }
        /// Abre una sub-pantalla de menú (Dante, Niveles, Ajustes, Calibración).
        public bool Open(DrumixScreen s)
        {
            if (IsGameplay(s) || s == DrumixScreen.Results) return false;
            if (s == DrumixScreen.Calibration && Screen != DrumixScreen.Menu) return false; // calibrar solo desde el menú (sin canción)
            if (s == Screen) return true;
            back = Screen == DrumixScreen.Pause ? DrumixScreen.Pause : DrumixScreen.Menu;
            Screen = s; return true;
        }
        /// Volver/Esc en una sub-pantalla. Devuelve false si no hay a dónde volver (menú/juego).
        public bool Back()
        {
            switch (Screen)
            {
                case DrumixScreen.Dante: case DrumixScreen.Levels: case DrumixScreen.Settings: case DrumixScreen.Calibration:
                    Screen = back; back = DrumixScreen.Menu; return true;
                default: return false;
            }
        }
    }

    public static class DrumixProgressRules
    {
        /// Nivel 0 siempre abierto; el resto se abre cuando el anterior se pasó (mejor % >= % para pasar de ESE nivel).
        public static bool IsUnlocked(int index, IList<float> best, IList<float> pass)
        {
            if (index <= 0) return true;
            if (best == null || index - 1 >= best.Count) return false;
            float need = pass != null && index - 1 < pass.Count ? pass[index - 1] : 50f;
            return best[index - 1] >= need;
        }
        public static float Better(float oldBest, float percent) { return Math.Max(oldBest, Math.Max(0f, Math.Min(100f, percent))); }
        public static string Rank(float percent)
        {
            if (percent >= 95f) return "S";
            if (percent >= 85f) return "A";
            if (percent >= 70f) return "B";
            if (percent >= 50f) return "C";
            return "D";
        }
        /// 0-3 estrellas para la tarjeta del nivel.
        public static int Stars(float percent) { return percent >= 90f ? 3 : percent >= 70f ? 2 : percent >= 50f ? 1 : 0; }
    }

    /// Puntos "arcade" del HUD (PUNTOS / MULTI). El % para pasar sigue siendo el de DrumixScoring.
    public sealed class DrumixArcade
    {
        public int Score { get; private set; }
        public static int Multiplier(int combo) { return 1 + Math.Min(Math.Max(combo, 0) / 10, 3); }
        public void Reset() { Score = 0; }
        public int Add(DrumixJudgement j, int combo)
        {
            int baseP = j == DrumixJudgement.Perfect ? 100 : j == DrumixJudgement.Good ? 50 : 0;
            int p = baseP * Multiplier(combo);
            Score += p; return p;
        }
    }

    /// Ajustes del jugador (sin UnityEngine; DrumixSettings los guarda en PlayerPrefs y los aplica).
    [Serializable]
    public sealed class DrumixSettingsData
    {
        public float master = 0.85f, music = 0.7f, sfx = 0.9f;
        public float latencyMs = 0f;
        public bool haptics = true, softLights = false, crowdProps = true;
        public float kitX, kitY, kitZ, kitYaw;
        public const float MaxLatency = 200f, MaxKitMove = 0.6f, MaxKitHeight = 0.3f, MaxKitYaw = 45f;

        public DrumixSettingsData Clone() { return (DrumixSettingsData)MemberwiseClone(); }
        static float C(float v, float a, float b) { return float.IsNaN(v) ? a : v < a ? a : v > b ? b : v; }
        public void Sanitize()
        {
            master = C(master, 0f, 1f); music = C(music, 0f, 1f); sfx = C(sfx, 0f, 1f);
            latencyMs = (float)Math.Round(C(latencyMs, -MaxLatency, MaxLatency));
            kitX = C(kitX, -MaxKitMove, MaxKitMove); kitZ = C(kitZ, -MaxKitMove, MaxKitMove);
            kitY = C(kitY, -MaxKitHeight, MaxKitHeight); kitYaw = C(kitYaw, -MaxKitYaw, MaxKitYaw);
        }
        /// Latencia positiva = el sonido/visor llega tarde: el golpe se juzga como si fuera latencyMs antes.
        public float JudgeTime(float songTime) { return songTime - latencyMs / 1000f; }
        public void ResetKit() { kitX = kitY = kitZ = kitYaw = 0f; }
    }

    /// Diálogo de Dante con efecto máquina de escribir: Next() primero completa la línea, luego pasa a la siguiente.
    public sealed class DrumixDialogue
    {
        readonly List<string> lines = new List<string>();
        public float SecondsPerChar = 0.025f;
        public int Index { get; private set; }
        public float Age { get; private set; }
        public bool Finished { get; private set; } = true;
        public int Count { get { return lines.Count; } }

        public void Start(IEnumerable<string> source)
        {
            lines.Clear();
            if (source != null) foreach (var l in source) if (!string.IsNullOrEmpty(l)) lines.Add(l);
            Index = 0; Age = 0f; Finished = lines.Count == 0;
        }
        public string Line { get { return Finished || Index >= lines.Count ? "" : lines[Index]; } }
        public int VisibleChars { get { var l = Line; return SecondsPerChar <= 0f ? l.Length : Math.Min(l.Length, (int)(Age / SecondsPerChar)); } }
        public bool LineComplete { get { return VisibleChars >= Line.Length; } }
        public string Visible { get { var l = Line; return l.Substring(0, VisibleChars); } }
        public void Tick(float dt) { if (!Finished) Age += Math.Max(0f, dt); }
        /// Devuelve true si el diálogo terminó con este paso.
        public bool Next()
        {
            if (Finished) return true;
            if (!LineComplete) { Age = Line.Length * SecondsPerChar + 0.001f; return false; }
            Index++; Age = 0f;
            if (Index >= lines.Count) Finished = true;
            return Finished;
        }
        public void Skip() { Index = lines.Count; Finished = true; }

        public static string[] Welcome(string player)
        {
            return new[] {
                "Bienvenida, " + player + ". Soy Dante, líder de la banda. Hoy empieza tu camino como nuestra baterista.",
                "Golpea la pieza que se ilumina justo cuando el aro se cierra. En PC: teclas A S D F G H J K.",
                "Con 50% pasas al siguiente escenario. Empezamos en El Garaje. ¡A romperla!" };
        }
        public static string Result(string player, bool passed, bool advanced, float percent)
        {
            if (passed && percent >= 90f) return "¡Brutal, " + player + "! El público está de pie.";
            if (passed) return advanced ? "¡Así se hace, " + player + "! La banda te espera en el siguiente escenario." : "¡Bien tocado, " + player + "!";
            return player + ", respira. Necesitas 50%. Volvamos a seguir las luces.";
        }
    }

    /// Rectángulo en píxeles del canvas (centro + tamaño), para comprobar que nada del HUD se encima.
    public struct DrumixUIRect
    {
        public float x, y, w, h;
        public DrumixUIRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public bool Overlaps(DrumixUIRect o) { return Math.Abs(x - o.x) * 2f < w + o.w && Math.Abs(y - o.y) * 2f < h + o.h; }
        public bool Inside(float cw, float ch) { return Math.Abs(x) + w / 2f <= cw / 2f && Math.Abs(y) + h / 2f <= ch / 2f; }
    }

    /// Posiciones del HUD en un canvas de 1600x900 (PC: escalado a la pantalla; VR: panel 3D).
    /// Arriba la barra de puntaje, debajo el texto PERFECT/MISS chico, al centro la cuenta 3-2-1 (zona libre: ahí se ve la batería),
    /// abajo el carril de notas. Ninguno se toca (lo prueban los tests).
    public static class DrumixUILayout
    {
        public const float W = 1600f, H = 900f;
        public static readonly DrumixUIRect TopBar = new DrumixUIRect(0, 372, 1240, 120);
        public static readonly DrumixUIRect Pause = new DrumixUIRect(700, 372, 150, 56);
        public static readonly DrumixUIRect Next = new DrumixUIRect(0, 290, 700, 34);
        public static readonly DrumixUIRect Feedback = new DrumixUIRect(0, 235, 620, 70);
        public static readonly DrumixUIRect Countdown = new DrumixUIRect(0, 60, 420, 220);
        public static readonly DrumixUIRect Highway = new DrumixUIRect(0, -275, 960, 330);
        public static DrumixUIRect[] Hud() { return new[] { TopBar, Pause, Next, Feedback, Countdown, Highway }; }
        public static string[] HudNames() { return new[] { "barra", "pausa", "siguiente", "feedback", "cuenta", "carril" }; }

        /// y (px, dentro del carril) de una nota: llega a la línea de golpe cuando dt = 0.
        public static float NoteY(float dt, float window, float hitY, float topY)
        {
            if (window <= 0f) return hitY;
            return hitY + (topY - hitY) * (dt / window);
        }
        /// 0..1 del aro que se cierra sobre el pad en VR (1 = recién aparece, 0 = golpear ya).
        public static float RingScale(float dt, float lead) { return lead <= 0f ? 1f : 1f + 0.8f * Math.Max(0f, Math.Min(1f, dt / lead)); }
    }
}
