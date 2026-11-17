using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drumix
{
    /// Secuencia + combo + puntaje (0-100 %, 50 % para pasar). La lógica vive en DrumixScoring (C# puro, con tests).
    /// Nota que se pasa = MISS (baja el %). Golpe sin nota cerca = MISS de ritmo (rompe combo, no baja el %).
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public class DrumixGame : MonoBehaviour
    {
        public static DrumixGame Instance { get; private set; }

        [Header("Canción de prueba (El Bar Local)")]
        public string songName = "El Bar Local";
        [Range(50, 180)] public float bpm = 88f;
        [Range(4, 64)] public int bars = 12;
        public int seed = 1234;
        [Tooltip("Piezas que usa la secuencia. Si una no existe en la escena se reemplaza sola.")]
        public DrumixLane[] lanes = { DrumixLane.Kick, DrumixLane.Snare, DrumixLane.HiHat, DrumixLane.Tom1, DrumixLane.Tom2, DrumixLane.FloorTom, DrumixLane.Crash, DrumixLane.Ride };

        [Header("Reglas")]
        public float leadTime = 1.4f;
        public float perfectWindow = 0.10f;
        public float goodWindow = 0.22f;
        [Range(0, 100)] public float passPercent = 50f;
        public int comboStep = 10;
        public float countdownSeconds = 3f;

        [Header("Niveles (Assets/DRUMIX/Config/Niveles) · si hay, mandan sobre los valores de arriba")]
        public DrumixLevel[] levels = new DrumixLevel[0];
        public int levelIndex = 0;
        public DrumixPattern pattern = DrumixPattern.Rock;
        [Header("Escenas del equipo (las llena ARMAR TODO)")]
        [Tooltip("Escena de cada nivel (mismo orden que 'levels'). Vacío = todos los niveles se juegan en esta escena.")]
        public string[] levelScenes = new string[0];
        [Tooltip("Nivel que representa ESTA escena (-1 = ninguno, p. ej. DRUMIX_Bar).")]
        public int sceneLevel = -1;
        // Lo que se elige en una escena y se juega en la siguiente (sobrevive al LoadScene).
        static int pendingLevel = -1; static bool pendingStart;
        bool loadingScene;

        public DrumixState State { get; private set; } = DrumixState.Menu;
        /// Pausa (Esc / botón PAUSA / menú del control): congela canción, cuenta y notas. No toca Time.timeScale (no afecta scripts del equipo).
        public bool Paused { get; private set; }
        /// Último resultado: si pasó y había otro nivel, ya quedó seleccionado el siguiente.
        public bool AdvancedLevel { get; private set; }
        public int LevelCount { get { return ValidLevels(); } }
        AudioSource music;
        public float SongTime { get; private set; }
        public float CountdownLeft { get; private set; }
        readonly DrumixScoring score = new DrumixScoring();
        public DrumixScoring Scoring { get { return score; } }
        public int Combo { get { return score.Combo; } }
        public int MaxCombo { get { return score.MaxCombo; } }
        public int Perfects { get { return score.Perfects; } }
        public int Goods { get { return score.Goods; } }
        public int Misses { get { return score.Misses; } }
        public int TotalNotes { get { return score.TotalNotes; } }
        public float Percent { get { return score.Percent; } }
        public float FinalPercent { get { return score.FinalPercent; } }
        public bool Passed { get { return score.Passed; } }
        public float Progress { get { return score.TotalNotes == 0 ? 0 : Mathf.Clamp01(SongTime / (score.LastNoteTime + 1f)); } }

        readonly Dictionary<DrumixLane, DrumixPad> pads = new Dictionary<DrumixLane, DrumixPad>();
        readonly List<DrumixHitInfo> missBuffer = new List<DrumixHitInfo>();
        float padRefresh;
        bool duplicate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { Instance = null; pendingLevel = -1; pendingStart = false; }

        void Awake()
        {
            if (Instance && Instance != this)
            {
                duplicate = true;
                Debug.LogWarning("[DRUMIX] Hay dos DrumixGame; se desactiva el duplicado: " + name);
                enabled = false; return;
            }
            Instance = this;
            if (pendingLevel >= 0) { levelIndex = pendingLevel; pendingLevel = -1; }
            else if (sceneLevel >= 0) levelIndex = sceneLevel;
        }
        void OnEnable() { if (!duplicate) DrumixEvents.OnPadStruck += OnPadStruck; }
        void OnDisable() { DrumixEvents.OnPadStruck -= OnPadStruck; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start()
        {
            RefreshPads(); SetState(DrumixState.Menu);
            if (pendingStart) { pendingStart = false; StartCoroutine(StartNextFrame()); } // venía de "JUGAR"/"SIGUIENTE" en otra escena
        }
        System.Collections.IEnumerator StartNextFrame() { yield return null; StartSong(); }

        /// Si el nivel 'index' vive en otra escena del equipo, la carga (y opcionalmente arranca la canción allá).
        bool GoToLevelScene(int index, bool start)
        {
            if (loadingScene) return true;
            string target = DrumixLevelData.SceneToLoad(levelScenes, index, SceneManager.GetActiveScene().name);
            if (target == null) return false;
            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogWarning("[DRUMIX] La escena '" + target + "' del nivel " + (index + 1) + " no está en Build Settings: se juega aquí. (DRUMIX > ARMAR TODO la agrega)");
                return false;
            }
            loadingScene = true; pendingLevel = index; pendingStart = start;
            SetPaused(false); ClearCues();
            SceneManager.LoadScene(target);
            return true;
        }

        public void RefreshPads()
        {
            pads.Clear();
            foreach (var pad in DrumixUtil.FindAll<DrumixPad>())
                if (pad && pad.isActiveAndEnabled && !pads.ContainsKey(pad.lane)) pads[pad.lane] = pad;
        }
        public DrumixPad GetPad(DrumixLane lane) { DrumixPad pad; return pads.TryGetValue(lane, out pad) && pad ? pad : null; }

        int ValidLevels()
        {
            int n = 0;
            if (levels != null) foreach (var l in levels) if (l && l.spec != null) n++;
            return n;
        }
        public DrumixLevel CurrentLevel
        {
            get
            {
                if (levels == null) return null;
                int i = 0;
                foreach (var l in levels)
                {
                    if (!l || l.spec == null) continue;
                    if (i == Mathf.Clamp(levelIndex, 0, ValidLevels() - 1)) return l;
                    i++;
                }
                return null;
            }
        }
        /// Nivel válido número i (0..LevelCount-1), sin contar huecos vacíos de la lista.
        public DrumixLevel LevelAt(int index)
        {
            if (levels == null) return null;
            int i = 0;
            foreach (var l in levels) { if (!l || l.spec == null) continue; if (i == index) return l; i++; }
            return null;
        }
        public int CurrentIndex { get { int n = ValidLevels(); return n == 0 ? 0 : Mathf.Clamp(levelIndex, 0, n - 1); } }
        /// Id estable para guardar el mejor % (nombre del asset del nivel).
        public string LevelId(int index) { var l = LevelAt(index); return l ? l.name : songName; }
        /// Elegir nivel desde la pantalla de niveles.
        public void SelectLevel(int index)
        {
            int n = ValidLevels();
            if (n == 0 || (State != DrumixState.Menu && State != DrumixState.Results)) return;
            levelIndex = Mathf.Clamp(index, 0, n - 1); AdvancedLevel = false;
            if (GoToLevelScene(levelIndex, false)) return; // el nivel tiene su propia escena del equipo
            DrumixEvents.ReportState(State);
        }
        public string LevelTitle { get { var l = CurrentLevel; return l ? l.spec.name : songName; } }
        public string LevelSubtitle { get { var l = CurrentLevel; return l ? l.spec.subtitle : ""; } }

        /// Botón "NIVEL": pasa al siguiente nivel (vuelve al primero después del último).
        public void CycleLevel()
        {
            int n = ValidLevels();
            if (n == 0 || (State != DrumixState.Menu && State != DrumixState.Results)) return;
            levelIndex = (Mathf.Clamp(levelIndex, 0, n - 1) + 1) % n;
            AdvancedLevel = false;
            if (GoToLevelScene(levelIndex, false)) return;
            DrumixEvents.ReportState(State); // la UI refresca el nombre
        }

        void ApplyLevel(DrumixLevelSpec src)
        {
            var spec = src.Clone(); spec.Sanitize();
            songName = spec.name; pattern = spec.pattern; bpm = spec.bpm; bars = spec.bars; seed = spec.seed;
            perfectWindow = spec.perfectWindow; goodWindow = spec.goodWindow; passPercent = spec.passPercent;
            leadTime = spec.leadTime; comboStep = spec.comboStep; lanes = spec.lanes;
        }

        public void StartSong()
        {
            if (loadingScene || GoToLevelScene(CurrentIndex, true)) return; // el siguiente nivel se toca en su escena
            RefreshPads();
            var level = CurrentLevel;
            if (level) ApplyLevel(level.spec);
            else
            {
                // Sin niveles: la dificultad del equipo (DifficultyManager) puede ajustar ventanas/tempo.
                var bridge = GetComponent<DrumixTeamBridge>();
                if (bridge) bridge.BeforeSong(this);
            }
            AdvancedLevel = false;
            if (Paused) { Paused = false; DrumixEvents.ReportPause(false); }
            score.PerfectWindow = perfectWindow; score.GoodWindow = Mathf.Max(goodWindow, perfectWindow);
            score.PassPercent = passPercent; score.ComboStep = comboStep;
            score.Load(DrumixScoring.BuildChart(bpm, bars, seed, lanes, l => pads.Count == 0 || pads.ContainsKey(l), pattern));
            SongTime = -leadTime;
            CountdownLeft = countdownSeconds;
            ClearCues();
            SetState(DrumixState.Countdown);
            DrumixEvents.ReportScore(score.Percent, score.Combo);
        }
        public void BackToMenu() { SetPaused(false); ClearCues(); SetState(DrumixState.Menu); }
        /// Reiniciar la misma canción (desde pausa o resultados: si ya pasó al siguiente nivel, repite el que acaba de tocar).
        public void Restart()
        {
            SetPaused(false);
            if (State == DrumixState.Results && AdvancedLevel) levelIndex = Mathf.Max(0, levelIndex - 1);
            StartSong();
        }
        public void Pause() { if (State == DrumixState.Countdown || State == DrumixState.Playing) SetPaused(true); }
        public void Resume() { SetPaused(false); }
        public void TogglePause() { if (Paused) Resume(); else Pause(); }
        void SetPaused(bool on)
        {
            if (Paused == on) return;
            Paused = on;
            if (music) { if (on) music.Pause(); else if (State == DrumixState.Playing) music.UnPause(); }
            DrumixEvents.ReportPause(on);
            DrumixEvents.ReportState(State); // la UI cambia entre HUD y PAUSA
        }
        public void ApplyMusicVolume()
        {
            var level = CurrentLevel;
            if (music && level) music.volume = level.musicVolume * DrumixSettings.Current.music;
        }
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetState(DrumixState state)
        {
            State = state;
            UpdateMusic();
            DrumixEvents.ReportState(state);
        }

        void UpdateMusic()
        {
            var level = CurrentLevel;
            bool play = State == DrumixState.Playing && level && level.music;
            if (play)
            {
                if (!music) { music = gameObject.AddComponent<AudioSource>(); music.playOnAwake = false; music.loop = true; music.spatialBlend = 0f; }
                music.clip = level.music; music.volume = level.musicVolume * DrumixSettings.Current.music; music.Play();
            }
            else if (music && music.isPlaying) music.Stop();
        }

        void Update()
        {
            padRefresh -= Time.deltaTime;
            if (padRefresh <= 0) { padRefresh = 2f; if (pads.Count == 0) RefreshPads(); }
            if (Paused) return;
            if (State == DrumixState.Countdown)
            {
                CountdownLeft -= Time.deltaTime;
                if (CountdownLeft <= 0) { SongTime = -0.5f; SetState(DrumixState.Playing); }
                return;
            }
            if (State != DrumixState.Playing) return;
            SongTime += Time.deltaTime;

            DrumixEvents.CollectAndReport(score, SongTime, missBuffer);
            foreach (var kv in pads) if (kv.Value) kv.Value.SetCue(score.Cue(kv.Key, SongTime, leadTime));
            if (score.IsFinished(SongTime)) Finish();
        }

        void ClearCues() { foreach (var kv in pads) if (kv.Value) kv.Value.SetCue(0); }

        /// Siguiente nota pendiente (para el HUD "Siguiente: CAJA (S)").
        public DrumixScoring.Note NextNote() { return score.Next(); }

        void OnPadStruck(DrumixLane lane, float strength)
        {
            if (State != DrumixState.Playing || Paused) return;
            DrumixEvents.JudgeAndReport(score, lane, DrumixSettings.Current.JudgeTime(SongTime)); // latencia calibrada en Ajustes
        }

        void Finish()
        {
            ClearCues();
            int n = ValidLevels();
            DrumixSettings.ReportResult(LevelId(CurrentIndex), score.FinalPercent); // mejor % (abre el siguiente nivel en la pantalla de niveles)
            if (n > 0)
            {
                int before = Mathf.Clamp(levelIndex, 0, n - 1);
                levelIndex = DrumixLevelData.NextIndex(before, n, score.Passed);
                AdvancedLevel = levelIndex != before;
            }
            SetState(DrumixState.Results);
            DrumixEvents.ReportFinished(score.FinalPercent, score.Passed);
        }
    }
}
