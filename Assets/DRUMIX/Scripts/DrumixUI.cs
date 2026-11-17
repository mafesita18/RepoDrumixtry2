using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;
using K = Drumix.DrumixUIKit;
using L = Drumix.DrumixUILayout;

namespace Drumix
{
    /// UI completa de DRUMIX en UN canvas (estilo "grabado" de BateriaVR, adaptado al proyecto nuevo):
    /// menú principal → Dante (bienvenida) → niveles (Assets/DRUMIX_Config/Niveles, con desbloqueo) → HUD (barra de puntaje,
    /// carril de notas de 8 teclas A..K, feedback chico arriba, cuenta 3-2-1 al centro) → pausa / ajustes / calibración → resultados.
    /// PC sin visor: pantalla completa. Visor/Quest: panel 3D delante (la guía es el aro que se cierra sobre cada pad).
    /// Se construye sola por código; las posiciones del HUD están en DrumixUILayout (probadas: nada se encima).
    public class DrumixUI : MonoBehaviour
    {
        public static DrumixUI Instance { get; private set; }
        public AudioClip hoverClip, clickClip;
        public Vector2 canvasSize = new Vector2(L.W, L.H);
        public float metersPerPixel = 0.0014f;
        [Tooltip("Raycaster extra para VR (lo pone el builder si hay XRI): TrackedDeviceGraphicRaycaster.")]
        public string xrRaycasterType = "";
        public enum Modo { Auto, PantallaCompleta, PanelVR }
        [Tooltip("Auto: pantalla completa en PC sin visor, panel 3D con visor/Quest.")]
        public Modo modo = Modo.Auto;
        public float waitForHeadset = 0.8f;

        [Header("Historia")]
        public string playerName = "Roxy";
        [Tooltip("Dante da la bienvenida la primera vez (se guarda). Marca 'alwaysShowDante' para verlo siempre.")]
        public bool showDanteOnStart = true;
        public bool alwaysShowDante = false;
        [TextArea(2, 4)] public string[] danteWelcome = new string[0];
        [Tooltip("Para probar: todos los niveles abiertos aunque no se haya pasado el anterior.")]
        public bool unlockAllLevels = false;

        public bool IsFullScreen { get; private set; }
        public Canvas Canvas { get; private set; }
        public DrumixScreen Screen { get { return flow.Screen; } }
        float modeTimer, recheckTimer; bool modeDecided;
        const string CanvasName = "DRUMIX_Canvas";

        readonly DrumixUIFlow flow = new DrumixUIFlow();
        readonly DrumixDialogue dialogue = new DrumixDialogue();
        readonly DrumixArcade arcade = new DrumixArcade();
        AudioSource uiSource;
        bool duplicate;

        // pantallas
        RectTransform menuP, levelsP, settingsP, calibP, hudP, countP, pauseP, resultsP;
        RectTransform menuButtons, danteBox;
        readonly List<RectTransform> screens = new List<RectTransform>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { Instance = null; }

        void Awake()
        {
            if (Instance && Instance != this) { duplicate = true; Debug.LogWarning("[DRUMIX] UI duplicada desactivada: " + name); gameObject.SetActive(false); return; }
            Instance = this;
            Build();
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false; uiSource.spatialBlend = 0f;
            if (!hoverClip) hoverClip = DrumixProceduralAudio.Blip(false);
            if (!clickClip) clickClip = DrumixProceduralAudio.Blip(true);
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void OnEnable()
        {
            if (duplicate) return;
            DrumixEvents.OnStateChanged += State; DrumixEvents.OnHit += Hit; DrumixEvents.OnMiss += Miss;
            DrumixEvents.OnScoreChanged += Score; DrumixEvents.OnCombo += Combo; DrumixEvents.OnPadStruck += Struck;
        }
        void OnDisable()
        {
            DrumixEvents.OnStateChanged -= State; DrumixEvents.OnHit -= Hit; DrumixEvents.OnMiss -= Miss;
            DrumixEvents.OnScoreChanged -= Score; DrumixEvents.OnCombo -= Combo; DrumixEvents.OnPadStruck -= Struck;
        }
        void Start()
        {
            if (Canvas && !Canvas.worldCamera) Canvas.worldCamera = Camera.main;
            EnsureEventSystem();
            DrumixSettings.Load();
            var g = DrumixGame.Instance;
            State(g ? g.State : DrumixState.Menu);
            if (showDanteOnStart && (alwaysShowDante || !DrumixSettings.DanteSeen) && (!g || g.State == DrumixState.Menu)) OpenDante();
        }

        public static void PlayUISound(bool click)
        {
            var ui = Instance;
            if (!ui || !ui.uiSource) return;
            var clip = click ? ui.clickClip : ui.hoverClip;
            if (clip) ui.uiSource.PlayOneShot(clip, click ? 0.8f : 0.4f);
        }

        // =====================================================================================  construcción
        public void Build()
        {
            var old = transform.Find(CanvasName);
            if (old) DestroyImmediate(old.gameObject);
            screens.Clear();

            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            go.transform.SetParent(transform, false);
            Canvas = go.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.WorldSpace;
            Canvas.sortingOrder = 10;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = canvasSize; rt.localScale = Vector3.one * metersPerPixel;
            go.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            if (!string.IsNullOrEmpty(xrRaycasterType))
            {
                Type raycaster = DrumixUtil.FindType(xrRaycasterType);
                if (raycaster != null && typeof(Component).IsAssignableFrom(raycaster)) go.AddComponent(raycaster);
            }
            BuildMenu(rt); BuildLevels(rt); BuildSettings(rt); BuildCalibration(rt);
            BuildHud(rt); BuildCountdown(rt); BuildPause(rt); BuildResults(rt);
            Show(DrumixScreen.Menu, false);
        }

        RectTransform Screenp(Transform canvas, string name)
        {
            var s = K.Stretch(canvas, name);
            s.gameObject.AddComponent<CanvasGroup>();
            screens.Add(s);
            return s;
        }

        // ---------- MENÚ PRINCIPAL + DANTE ----------
        Text menuLevel, menuRules;
        Text danteText; Button danteNext;
        void BuildMenu(Transform canvas)
        {
            menuP = Screenp(canvas, "Menu");
            var f = K.Frame(menuP, "Marco", Vector2.zero, new Vector2(1200, 760));
            var t = K.Label(f, "Titulo", "DRUMIX", 104, new Vector2(0, 268), new Vector2(1000, 130), K.White, FontStyle.Bold); K.Shadow(t);
            K.Label(f, "Banda", "EL RITMO DE LA BANDA", 28, new Vector2(0, 192), new Vector2(1000, 40), K.Accent);
            menuLevel = K.Label(f, "Nivel", "", 26, new Vector2(0, 146), new Vector2(1000, 40), K.Muted);

            menuButtons = K.Rect(f, "Botones", new Vector2(0, -40), new Vector2(1000, 380));
            K.Button(menuButtons, "Jugar", "JUGAR", new Vector2(-235, 130), new Vector2(430, 74), PlayCurrent, 30);
            K.Button(menuButtons, "Niveles", "NIVELES", new Vector2(235, 130), new Vector2(430, 74), () => Open(DrumixScreen.Levels), 30);
            K.Button(menuButtons, "Calibrar", "CALIBRAR BATERÍA", new Vector2(-235, 38), new Vector2(430, 74), () => Open(DrumixScreen.Calibration), 30);
            K.Button(menuButtons, "Ajustes", "AJUSTES", new Vector2(235, 38), new Vector2(430, 74), () => Open(DrumixScreen.Settings), 30);
            K.Button(menuButtons, "Salir", "SALIR", new Vector2(0, -54), new Vector2(430, 66), () => { var g = DrumixGame.Instance; if (g) g.QuitGame(); }, 26);
            menuRules = K.Label(menuButtons, "Reglas", "", 21, new Vector2(0, -126), new Vector2(1000, 34), K.Muted);
            K.Label(menuButtons, "Controles", "PC: A S D F G H J K  ·  Enter = aceptar  ·  Esc/P = pausa      Quest: baquetas  ·  A/X = bombo  ·  Menú = pausa",
                19, new Vector2(0, -164), new Vector2(1100, 30), new Color(K.Muted.r, K.Muted.g, K.Muted.b, 0.75f));

            // Dante: reemplaza los botones mientras habla (no se encima con nada).
            danteBox = K.Frame(f, "Dante", new Vector2(0, -90), new Vector2(1040, 300), false);
            var portrait = K.Frame(danteBox, "Retrato", new Vector2(-405, 10), new Vector2(150, 170), false);
            K.Label(portrait, "Inicial", "D", 92, Vector2.zero, new Vector2(140, 150), K.Accent, FontStyle.Bold);
            K.Label(danteBox, "Nombre", "DANTE  ·  LÍDER DE LA BANDA", 22, new Vector2(80, 108), new Vector2(760, 30), K.Accent, FontStyle.Bold, TextAnchor.MiddleLeft);
            danteText = K.Label(danteBox, "Texto", "", 27, new Vector2(80, 20), new Vector2(760, 130), K.White, FontStyle.Normal, TextAnchor.UpperLeft);
            danteNext = K.Button(danteBox, "Siguiente", "SIGUIENTE  >", new Vector2(250, -100), new Vector2(230, 50), DanteNext, 22);
            K.Button(danteBox, "Saltar", "SALTAR", new Vector2(420, -100), new Vector2(130, 50), DanteFinish, 22);
            danteBox.gameObject.SetActive(false);
        }

        // ---------- NIVELES ----------
        Text levelsTitle, levelsPage;
        readonly List<Button> cardButtons = new List<Button>();
        readonly List<Text> cardNum = new List<Text>(), cardInfo = new List<Text>(), cardStatus = new List<Text>();
        readonly List<Button> cardChoose = new List<Button>();
        readonly List<Image[]> cardStars = new List<Image[]>();
        Button prevPage, nextPage;
        int page;
        const int PerPage = 3;
        void BuildLevels(Transform canvas)
        {
            levelsP = Screenp(canvas, "Niveles");
            var f = K.Frame(levelsP, "Marco", Vector2.zero, new Vector2(1240, 780));
            levelsTitle = K.Label(f, "Titulo", "TU CAMINO", 50, new Vector2(0, 312), new Vector2(1000, 70), K.Accent, FontStyle.Bold);
            for (int i = 0; i < PerPage; i++)
            {
                int slot = i;
                var x = (i - 1) * 370f;
                var card = K.Box(f, "Tarjeta_" + i, new Vector2(x, 20), new Vector2(340, 480), K.Panel);
                card.raycastTarget = true;
                var b = card.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = card;
                var fx = card.gameObject.AddComponent<DrumixButtonFX>(); fx.normal = K.Panel; fx.hover = K.PanelHover; fx.pressed = K.Accent; fx.hoverScale = 1.03f;
                if (Application.isPlaying) b.onClick.AddListener(() => ChooseSlot(slot));
                K.Border(card.rectTransform, 6, K.Line);
                cardButtons.Add(b);
                cardNum.Add(K.Label(card.rectTransform, "Numero", "01", 52, new Vector2(0, 182), new Vector2(300, 70), K.Accent, FontStyle.Bold));
                cardInfo.Add(K.Label(card.rectTransform, "Info", "", 23, new Vector2(0, 52), new Vector2(300, 190), K.White));
                var stars = new Image[3];
                for (int s = 0; s < 3; s++) stars[s] = K.Diamond(card.rectTransform, "Estrella_" + s, new Vector2((s - 1) * 34, -70), 18, K.Gold);
                cardStars.Add(stars);
                cardStatus.Add(K.Label(card.rectTransform, "Estado", "", 20, new Vector2(0, -124), new Vector2(300, 60), K.Muted));
                cardChoose.Add(K.Button(card.rectTransform, "Elegir", "ELEGIR", new Vector2(0, -196), new Vector2(280, 54), () => ChooseSlot(slot), 22));
            }
            prevPage = K.Button(f, "Anterior", "<", new Vector2(-470, -312), new Vector2(90, 54), () => { page--; RefreshLevels(); }, 28);
            nextPage = K.Button(f, "Siguientes", ">", new Vector2(470, -312), new Vector2(90, 54), () => { page++; RefreshLevels(); }, 28);
            levelsPage = K.Label(f, "Pagina", "", 20, new Vector2(-300, -312), new Vector2(200, 30), K.Muted);
            K.Button(f, "Volver", "VOLVER", new Vector2(0, -312), new Vector2(360, 58), () => Back(), 24);
        }

        // ---------- AJUSTES ----------
        Slider sMaster, sMusic, sSfx, sLatency; Text vMaster, vMusic, vSfx, vLatency;
        Toggle tHaptics, tSoft, tCrowd;
        bool refreshing;
        void BuildSettings(Transform canvas)
        {
            settingsP = Screenp(canvas, "Ajustes");
            var f = K.Frame(settingsP, "Marco", Vector2.zero, new Vector2(1100, 720));
            K.Label(f, "Titulo", "AJUSTES", 50, new Vector2(0, 288), new Vector2(900, 70), K.Accent, FontStyle.Bold);
            sMaster = SettingRow(f, 0, "GENERAL", 0, 1, false, v => { DrumixSettings.Current.master = v; DrumixSettings.Apply(); }, out vMaster);
            sMusic = SettingRow(f, 1, "MÚSICA", 0, 1, false, v => { DrumixSettings.Current.music = v; DrumixSettings.Apply(); }, out vMusic);
            sSfx = SettingRow(f, 2, "EFECTOS", 0, 1, false, v => { DrumixSettings.Current.sfx = v; DrumixSettings.Apply(); }, out vSfx);
            sLatency = SettingRow(f, 3, "LATENCIA (ms)", -DrumixSettingsData.MaxLatency, DrumixSettingsData.MaxLatency, true, v => { DrumixSettings.Current.latencyMs = v; }, out vLatency);
            K.Label(f, "AyudaLatencia", "Si sientes que tus golpes llegan tarde aunque vas a tiempo, sube la latencia.", 18, new Vector2(0, -30), new Vector2(900, 28), K.Muted);
            tHaptics = K.Toggle(f, "Vibracion", "Vibración", new Vector2(-235, -95), new Vector2(430, 54), v => { if (!refreshing) DrumixSettings.Current.haptics = v; });
            tSoft = K.Toggle(f, "Luces", "Luces suaves (VR)", new Vector2(235, -95), new Vector2(430, 54), v => { if (!refreshing) DrumixSettings.Current.softLights = v; });
            tCrowd = K.Toggle(f, "Publico", "Objetos del público", new Vector2(-235, -162), new Vector2(430, 54), v => { if (!refreshing) DrumixSettings.Current.crowdProps = v; });
            K.Button(f, "Reiniciar", "BORRAR PROGRESO", new Vector2(235, -162), new Vector2(430, 54), ResetProgress, 20);
            K.Button(f, "Guardar", "GUARDAR Y VOLVER", new Vector2(0, -268), new Vector2(520, 62), () => Back(), 24);
        }
        Slider SettingRow(RectTransform f, int i, string label, float min, float max, bool whole, Action<float> set, out Text value)
        {
            float y = 200 - i * 62;
            K.Label(f, "Nombre_" + i, label, 22, new Vector2(-330, y), new Vector2(260, 40), K.White, FontStyle.Normal, TextAnchor.MiddleRight);
            var v = K.Label(f, "Valor_" + i, "", 22, new Vector2(420, y), new Vector2(120, 40), K.Muted, FontStyle.Normal, TextAnchor.MiddleLeft);
            value = v;
            return K.Slider(f, "Slider_" + i, new Vector2(80, y), new Vector2(560, 44), min, max, whole, x => { if (refreshing) return; set(x); RefreshSettingValues(); });
        }

        // ---------- CALIBRACIÓN ----------
        Text calibPose; DrumixSettingsData calibBackup;
        void BuildCalibration(Transform canvas)
        {
            calibP = Screenp(canvas, "Calibracion");
            var f = K.Frame(calibP, "Marco", Vector2.zero, new Vector2(1100, 740));
            K.Label(f, "Titulo", "TU ESPACIO, TU BATERÍA", 44, new Vector2(0, 296), new Vector2(1000, 64), K.Accent, FontStyle.Bold);
            K.Label(f, "Ayuda", "Mueve la batería hasta que caiga cómoda en tus manos. Mantén libre tu espacio: el visor NO se mueve.", 22, new Vector2(0, 228), new Vector2(940, 60), K.Muted);
            const float s = 0.05f;
            CalButton(f, "IZQUIERDA", new Vector2(-300, 130), d => d.kitX -= s);
            CalButton(f, "ALEJAR", new Vector2(0, 130), d => d.kitZ += s);
            CalButton(f, "DERECHA", new Vector2(300, 130), d => d.kitX += s);
            CalButton(f, "SUBIR", new Vector2(-300, 50), d => d.kitY += s * 0.5f);
            CalButton(f, "ACERCAR", new Vector2(0, 50), d => d.kitZ -= s);
            CalButton(f, "BAJAR", new Vector2(300, 50), d => d.kitY -= s * 0.5f);
            CalButton(f, "GIRO -5°", new Vector2(-300, -30), d => d.kitYaw -= 5f);
            CalButton(f, "GIRO +5°", new Vector2(300, -30), d => d.kitYaw += 5f);
            calibPose = K.Label(f, "Pose", "", 22, new Vector2(0, -105), new Vector2(940, 34), K.Muted);
            K.Button(f, "Restablecer", "RESTABLECER", new Vector2(0, -170), new Vector2(340, 56), () => { DrumixSettings.Current.ResetKit(); DrumixSettings.Apply(); RefreshCalib(); }, 22);
            K.Button(f, "Guardar", "GUARDAR Y VOLVER", new Vector2(-250, -268), new Vector2(460, 62), () => { DrumixSettings.Save(); calibBackup = null; flow.Back(); ApplyScreen(); }, 24);
            K.Button(f, "Cancelar", "CANCELAR", new Vector2(250, -268), new Vector2(460, 62), () => Back(), 24);
        }
        void CalButton(RectTransform f, string text, Vector2 pos, Action<DrumixSettingsData> change)
        {
            K.Button(f, "Cal_" + text, text, pos, new Vector2(260, 60), () => { change(DrumixSettings.Current); DrumixSettings.Apply(); RefreshCalib(); }, 22);
        }

        // ---------- HUD ----------
        Text hudLevel, hudPercent, hudPoints, hudCombo, hudMulti, hudTime, hudNext, feedbackText;
        RectTransform hudProgress;
        float feedbackAge = 10f;
        void BuildHud(Transform canvas)
        {
            hudP = Screenp(canvas, "HUD");
            var bar = K.Box(hudP, "Barra", new Vector2(L.TopBar.x, L.TopBar.y), new Vector2(L.TopBar.w, L.TopBar.h), new Color(0.1f, 0.06f, 0.18f, 0.82f)).rectTransform;
            K.Border(bar, 4, K.Line);
            hudLevel = K.Label(bar, "Nivel", "", 18, new Vector2(0, 44), new Vector2(900, 24), K.Muted);
            hudPercent = K.Label(bar, "Precision", "100%", 52, new Vector2(-470, 0), new Vector2(260, 70), K.White, FontStyle.Bold); K.Shadow(hudPercent);
            hudPoints = Stat(bar, "PUNTOS", -150);
            hudCombo = Stat(bar, "COMBO", 120);
            hudMulti = Stat(bar, "MULTI", 380);
            hudProgress = K.Progress(bar, "Progreso", new Vector2(0, -42), new Vector2(1180, 10), K.Accent);
            hudTime = K.Label(bar, "Tiempo", "", 16, new Vector2(-470, -30), new Vector2(260, 20), K.Muted);
            K.Button(hudP, "Pausa", "PAUSA", new Vector2(L.Pause.x, L.Pause.y), new Vector2(L.Pause.w, L.Pause.h), () => { var g = DrumixGame.Instance; if (g) g.Pause(); }, 22);
            hudNext = K.Label(hudP, "Siguiente", "", 24, new Vector2(L.Next.x, L.Next.y), new Vector2(L.Next.w, L.Next.h), K.Miss);
            K.Shadow(hudNext);
            feedbackText = K.Label(hudP, "Feedback", "", 54, new Vector2(L.Feedback.x, L.Feedback.y), new Vector2(L.Feedback.w, L.Feedback.h), K.Gold, FontStyle.Bold);
            K.Shadow(feedbackText);
            BuildHighway(hudP);
        }
        Text Stat(RectTransform bar, string title, float x)
        {
            K.Label(bar, title, title, 17, new Vector2(x, 18), new Vector2(220, 22), K.Muted);
            var v = K.Label(bar, title + "_Valor", "0", 34, new Vector2(x, -8), new Vector2(220, 40), K.White, FontStyle.Bold);
            return v;
        }

        // carril de notas: 8 columnas, la nota baja hasta la línea dorada (= golpear ahí)
        RectTransform highway; Image[] notePool; Image[] laneFlash; float[] laneFlashAge;
        const int NotePool = 48;
        float HitY { get { return -L.Highway.h / 2f + 58f; } }
        float TopY { get { return L.Highway.h / 2f - 8f; } }
        void BuildHighway(RectTransform parent)
        {
            highway = K.Box(parent, "Carril de notas", new Vector2(L.Highway.x, L.Highway.y), new Vector2(L.Highway.w, L.Highway.h), new Color(0.05f, 0.03f, 0.1f, 0.6f)).rectTransform;
            K.Border(highway, 0, K.Line);
            float colW = L.Highway.w / 8f;
            laneFlash = new Image[8]; laneFlashAge = new float[8];
            for (int i = 0; i < 8; i++)
            {
                var lane = (DrumixLane)i;
                float x = -L.Highway.w / 2f + colW * (i + 0.5f);
                var c = K.LaneColor(lane);
                K.Box(highway, "Col_" + i, new Vector2(x, 0), new Vector2(colW - 4, L.Highway.h - 4), new Color(c.r, c.g, c.b, 0.07f));
                laneFlash[i] = K.Box(highway, "Destello_" + i, new Vector2(x, HitY), new Vector2(colW - 6, 26), new Color(c.r, c.g, c.b, 0f));
                K.Label(highway, "Pieza_" + i, DrumixEvents.LaneName(lane), 15, new Vector2(x, -L.Highway.h / 2f + 34), new Vector2(colW, 20), c);
                K.Label(highway, "Tecla_" + i, DrumixEvents.LaneKey(lane), 20, new Vector2(x, -L.Highway.h / 2f + 14), new Vector2(colW, 22), K.White, FontStyle.Bold);
            }
            K.Box(highway, "Linea de golpe", new Vector2(0, HitY), new Vector2(L.Highway.w - 8, 4), K.Gold);
            notePool = new Image[NotePool];
            for (int i = 0; i < NotePool; i++)
            {
                notePool[i] = K.Box(highway, "Nota_" + i, Vector2.zero, new Vector2(colW - 18, 22), Color.white);
                notePool[i].enabled = false;
            }
        }

        // ---------- CUENTA 3-2-1 ----------
        Text countText;
        void BuildCountdown(Transform canvas)
        {
            countP = Screenp(canvas, "Cuenta");
            countText = K.Label(countP, "Numero", "3", 170, new Vector2(L.Countdown.x, L.Countdown.y), new Vector2(L.Countdown.w, L.Countdown.h), K.Gold, FontStyle.Bold);
            K.Shadow(countText);
        }

        // ---------- PAUSA ----------
        void BuildPause(Transform canvas)
        {
            pauseP = Screenp(canvas, "Pausa");
            var f = K.Frame(pauseP, "Marco", new Vector2(0, -20), new Vector2(760, 600));
            K.Label(f, "Titulo", "PAUSA", 68, new Vector2(0, 218), new Vector2(700, 90), K.Accent, FontStyle.Bold);
            K.Button(f, "Continuar", "CONTINUAR", new Vector2(0, 110), new Vector2(460, 62), () => { var g = DrumixGame.Instance; if (g) g.Resume(); }, 26);
            K.Button(f, "Reiniciar", "REINICIAR", new Vector2(0, 32), new Vector2(460, 62), () => { var g = DrumixGame.Instance; if (g) g.Restart(); }, 26);
            K.Button(f, "Ajustes", "AJUSTES", new Vector2(0, -46), new Vector2(460, 62), () => Open(DrumixScreen.Settings), 26);
            K.Button(f, "Menu", "MENÚ", new Vector2(0, -124), new Vector2(460, 62), () => { var g = DrumixGame.Instance; if (g) g.BackToMenu(); }, 26);
            K.Label(f, "Nota", "La canción y las notas están detenidas.", 20, new Vector2(0, -214), new Vector2(700, 30), K.Muted);
        }

        // ---------- RESULTADOS ----------
        Text resTitle, resPercent, resRank, resStats, resDante;
        Image[] resStars;
        Button resPrimary, resSecondary;
        void BuildResults(Transform canvas)
        {
            resultsP = Screenp(canvas, "Resultados");
            var f = K.Frame(resultsP, "Marco", Vector2.zero, new Vector2(1100, 740));
            resTitle = K.Label(f, "Titulo", "APROBADO", 46, new Vector2(0, 286), new Vector2(900, 64), K.Accent, FontStyle.Bold);
            resPercent = K.Label(f, "Porcentaje", "0%", 120, new Vector2(0, 176), new Vector2(900, 140), K.White, FontStyle.Bold); K.Shadow(resPercent);
            resStars = new Image[3];
            for (int s = 0; s < 3; s++) resStars[s] = K.Diamond(f, "Estrella_" + s, new Vector2(-150 + (s - 1) * 40, 78), 22, K.Gold);
            resRank = K.Label(f, "Rango", "RANGO B", 30, new Vector2(60, 78), new Vector2(300, 44), K.Gold, FontStyle.Bold);
            resStats = K.Label(f, "Stats", "", 24, new Vector2(0, 0), new Vector2(940, 80), K.White);
            resDante = K.Label(f, "Dante", "", 23, new Vector2(0, -88), new Vector2(940, 70), K.Muted);
            resPrimary = K.Button(f, "Principal", "SIGUIENTE NIVEL", new Vector2(0, -180), new Vector2(520, 66), ResultsPrimary, 26);
            resSecondary = K.Button(f, "Secundario", "OTRA VEZ", new Vector2(-150, -268), new Vector2(270, 56), ResultsSecondary, 22);
            K.Button(f, "Menu", "MENÚ", new Vector2(150, -268), new Vector2(270, 56), () => { var g = DrumixGame.Instance; if (g) g.BackToMenu(); }, 22);
        }

        // =====================================================================================  flujo
        public bool Open(DrumixScreen s)
        {
            if (s == DrumixScreen.Calibration) calibBackup = DrumixSettings.Current.Clone();
            if (!flow.Open(s)) return false;
            if (s == DrumixScreen.Levels) { var g = DrumixGame.Instance; page = g ? g.CurrentIndex / PerPage : 0; }
            ApplyScreen();
            return true;
        }
        /// Volver/Esc. Ajustes guarda; Calibración cancela; Dante termina. false = no había a dónde volver.
        public bool Back()
        {
            switch (flow.Screen)
            {
                case DrumixScreen.Dante: DanteFinish(); return true;
                case DrumixScreen.Settings: DrumixSettings.Save(); break;
                case DrumixScreen.Calibration:
                    if (calibBackup != null) { CopyKit(calibBackup, DrumixSettings.Current); DrumixSettings.Apply(); calibBackup = null; }
                    break;
            }
            bool ok = flow.Back();
            if (ok) ApplyScreen();
            return ok;
        }
        static void CopyKit(DrumixSettingsData from, DrumixSettingsData to) { to.kitX = from.kitX; to.kitY = from.kitY; to.kitZ = from.kitZ; to.kitYaw = from.kitYaw; }

        /// Enter / A del control: el botón principal de la pantalla. true = lo usó la UI.
        public bool Submit()
        {
            var g = DrumixGame.Instance;
            switch (flow.Screen)
            {
                case DrumixScreen.Menu: PlayCurrent(); return true;
                case DrumixScreen.Dante: DanteNext(); return true;
                case DrumixScreen.Levels: ChooseIndex(g ? g.CurrentIndex : 0); return true;
                case DrumixScreen.Settings: Back(); return true;
                case DrumixScreen.Calibration: DrumixSettings.Save(); calibBackup = null; flow.Back(); ApplyScreen(); return true;
                case DrumixScreen.Pause: if (g) g.Resume(); return true;
                case DrumixScreen.Results: ResultsPrimary(); return true;
                default: return false;
            }
        }

        void OpenDante()
        {
            var lines = danteWelcome != null && danteWelcome.Length > 0 ? danteWelcome : DrumixDialogue.Welcome(playerName);
            dialogue.Start(lines);
            if (dialogue.Finished) return;
            Open(DrumixScreen.Dante);
        }
        void DanteNext() { if (dialogue.Next()) DanteFinish(); }
        void DanteFinish()
        {
            dialogue.Skip();
            DrumixSettings.DanteSeen = true;
            if (flow.Screen == DrumixScreen.Dante) { flow.Back(); ApplyScreen(); }
        }

        void PlayCurrent()
        {
            var g = DrumixGame.Instance; if (!g) return;
            if (!IsUnlocked(g.CurrentIndex)) g.SelectLevel(HighestUnlocked());
            g.StartSong();
        }
        bool IsUnlocked(int i)
        {
            var g = DrumixGame.Instance;
            if (unlockAllLevels || !g || g.LevelCount == 0) return true;
            var best = new List<float>(); var pass = new List<float>();
            for (int k = 0; k < g.LevelCount; k++)
            {
                best.Add(DrumixSettings.BestPercent(g.LevelId(k)));
                var l = g.LevelAt(k); pass.Add(l ? l.spec.passPercent : 50f);
            }
            return DrumixProgressRules.IsUnlocked(i, best, pass);
        }
        int HighestUnlocked()
        {
            var g = DrumixGame.Instance; int n = g ? g.LevelCount : 0, h = 0;
            for (int i = 0; i < n; i++) if (IsUnlocked(i)) h = i;
            return h;
        }
        void ChooseSlot(int slot) { ChooseIndex(page * PerPage + slot); }
        void ChooseIndex(int index)
        {
            var g = DrumixGame.Instance; if (!g) return;
            if (g.LevelCount == 0) { Back(); return; } // sin niveles: la canción de prueba
            if (index < 0 || index >= g.LevelCount || !IsUnlocked(index)) return;
            g.SelectLevel(index);
            Back(); // vuelve al menú con el nivel elegido (JUGAR lo empieza)
        }
        void ResetProgress()
        {
            var g = DrumixGame.Instance; var ids = new List<string>();
            if (g) for (int i = 0; i < g.LevelCount; i++) ids.Add(g.LevelId(i));
            DrumixSettings.ResetProgress(ids);
            if (g) g.SelectLevel(0);
            Feedback("PROGRESO BORRADO", K.Muted);
        }
        void ResultsPrimary()
        {
            var g = DrumixGame.Instance; if (!g) return;
            g.StartSong(); // si pasó y había otro nivel, ya quedó elegido el siguiente; si no, repite
        }
        void ResultsSecondary()
        {
            var g = DrumixGame.Instance; if (!g) return;
            if (g.AdvancedLevel) g.Restart(); // OTRA VEZ el nivel que acaba de tocar
            else { g.BackToMenu(); Open(DrumixScreen.Levels); }
        }

        // =====================================================================================  estado / pantallas
        void State(DrumixState s)
        {
            var g = DrumixGame.Instance;
            var before = flow.Screen;
            flow.GameState(s, g && g.Paused);
            if (s == DrumixState.Countdown && before != DrumixScreen.Countdown && before != DrumixScreen.Pause && before != DrumixScreen.Settings)
            {
                arcade.Reset(); feedbackText.text = ""; hudCombo.text = "0"; hudMulti.text = "x1"; hudPoints.text = "0"; hudPercent.text = "100%";
            }
            if (s == DrumixState.Results) FillResults();
            ApplyScreen();
        }

        void ApplyScreen() { Show(flow.Screen, Application.isPlaying); }

        void Show(DrumixScreen s, bool fade)
        {
            Set(menuP, s == DrumixScreen.Menu || s == DrumixScreen.Dante, fade);
            if (menuButtons) menuButtons.gameObject.SetActive(s != DrumixScreen.Dante);
            if (danteBox) danteBox.gameObject.SetActive(s == DrumixScreen.Dante);
            Set(levelsP, s == DrumixScreen.Levels, fade);
            Set(settingsP, s == DrumixScreen.Settings, fade);
            Set(calibP, s == DrumixScreen.Calibration, fade);
            Set(hudP, DrumixUIFlow.ShowsHud(s) || (s == DrumixScreen.Settings && FromPause()), fade);
            Set(countP, s == DrumixScreen.Countdown, fade);
            Set(pauseP, s == DrumixScreen.Pause, fade);
            Set(resultsP, s == DrumixScreen.Results, fade);
            if (s == DrumixScreen.Menu || s == DrumixScreen.Dante) RefreshMenu();
            if (s == DrumixScreen.Levels) RefreshLevels();
            if (s == DrumixScreen.Settings) RefreshSettings();
            if (s == DrumixScreen.Calibration) RefreshCalib();
        }
        bool FromPause() { var g = DrumixGame.Instance; return g && g.Paused; }

        void Set(RectTransform p, bool on, bool fade)
        {
            if (!p) return;
            if (on && !p.gameObject.activeSelf && fade)
            {
                var cg = p.GetComponent<CanvasGroup>(); if (cg) cg.alpha = 0f;
            }
            p.gameObject.SetActive(on);
        }
        void UpdateFades()
        {
            float step = Time.unscaledDeltaTime / 0.22f;
            foreach (var p in screens)
            {
                if (!p || !p.gameObject.activeSelf) continue;
                var cg = p.GetComponent<CanvasGroup>();
                if (cg && cg.alpha < 1f) cg.alpha = Mathf.Min(1f, cg.alpha + step);
            }
        }

        // =====================================================================================  refrescos
        void RefreshMenu()
        {
            var g = DrumixGame.Instance; if (!g || !menuLevel) return;
            var l = g.CurrentLevel;
            float bpm = l ? l.spec.bpm : g.bpm;
            menuLevel.text = g.LevelTitle + "  /  " + Mathf.RoundToInt(bpm) + " BPM" + (string.IsNullOrEmpty(g.LevelSubtitle) ? "" : "  ·  " + g.LevelSubtitle);
            float pw = l ? l.spec.perfectWindow : g.perfectWindow, gw = l ? l.spec.goodWindow : g.goodWindow, pp = l ? l.spec.passPercent : g.passPercent;
            menuRules.text = "PERFECT ±" + Mathf.RoundToInt(pw * 1000) + " ms  |  GOOD ±" + Mathf.RoundToInt(gw * 1000) + " ms  |  " + Mathf.RoundToInt(pp) + "% PARA AVANZAR";
        }
        void RefreshLevels()
        {
            var g = DrumixGame.Instance;
            int n = g ? g.LevelCount : 0;
            int pages = Mathf.Max(1, (n + PerPage - 1) / PerPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            levelsTitle.text = "TU CAMINO, " + playerName.ToUpperInvariant();
            prevPage.gameObject.SetActive(page > 0); nextPage.gameObject.SetActive(page < pages - 1);
            levelsPage.text = pages > 1 ? (page + 1) + " / " + pages : "";
            for (int i = 0; i < PerPage; i++)
            {
                int idx = page * PerPage + i;
                var l = g && idx < n ? g.LevelAt(idx) : null;
                bool show = l != null || (n == 0 && idx == 0 && g);
                cardButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                bool unlocked = IsUnlocked(idx);
                bool chosen = g && g.CurrentIndex == idx;
                float best = DrumixSettings.BestPercent(g.LevelId(idx));
                var spec = l ? l.spec : null;
                cardNum[i].text = (idx + 1).ToString("00");
                cardInfo[i].text = (spec != null ? spec.name : g.songName) + "\n\n" + Mathf.RoundToInt(spec != null ? spec.bpm : g.bpm) + " BPM\n" + (spec != null ? spec.subtitle : "");
                int stars = DrumixProgressRules.Stars(best);
                for (int s = 0; s < 3; s++) cardStars[i][s].color = s < stars ? K.Gold : new Color(1, 1, 1, 0.12f);
                cardStatus[i].text = unlocked ? (best > 0 ? "MEJOR: " + best.ToString("0.0") + "%" : "SIN JUGAR") : "BLOQUEADO\nSupera el anterior con " + Mathf.RoundToInt(PrevPass(idx)) + "%";
                K.SetText(cardChoose[i], !unlocked ? "BLOQUEADO" : chosen ? "ELEGIDO" : "ELEGIR");
                K.SetLocked(cardChoose[i], !unlocked); K.SetLocked(cardButtons[i], !unlocked);
                K.SetSelected(cardButtons[i], chosen && unlocked); K.SetSelected(cardChoose[i], chosen && unlocked);
            }
        }
        float PrevPass(int idx) { var g = DrumixGame.Instance; var l = g ? g.LevelAt(idx - 1) : null; return l ? l.spec.passPercent : 50f; }

        void RefreshSettings()
        {
            var d = DrumixSettings.Current;
            refreshing = true;
            sMaster.value = d.master; sMusic.value = d.music; sSfx.value = d.sfx; sLatency.value = d.latencyMs;
            tHaptics.isOn = d.haptics; tSoft.isOn = d.softLights; tCrowd.isOn = d.crowdProps;
            refreshing = false;
            RefreshSettingValues();
        }
        void RefreshSettingValues()
        {
            var d = DrumixSettings.Current;
            vMaster.text = Mathf.RoundToInt(d.master * 100) + "%"; vMusic.text = Mathf.RoundToInt(d.music * 100) + "%";
            vSfx.text = Mathf.RoundToInt(d.sfx * 100) + "%"; vLatency.text = d.latencyMs.ToString("+0;-0;0") + " ms";
        }
        void RefreshCalib()
        {
            var d = DrumixSettings.Current; d.Sanitize();
            if (calibPose) calibPose.text = "METROS  (" + d.kitX.ToString("0.00") + ", " + d.kitY.ToString("0.00") + ", " + d.kitZ.ToString("0.00") + ")   /   GIRO " + d.kitYaw.ToString("0") + "°";
        }

        void FillResults()
        {
            var g = DrumixGame.Instance; if (!g || !resTitle) return;
            float pct = g.FinalPercent;
            resTitle.text = g.Passed ? "APROBADO" : "REINTENTAR";
            resTitle.color = g.Passed ? K.Accent : K.Miss;
            resPercent.text = pct.ToString("0.0") + "%";
            int stars = DrumixProgressRules.Stars(pct);
            for (int s = 0; s < 3; s++) resStars[s].color = s < stars ? K.Gold : new Color(1, 1, 1, 0.15f);
            resRank.text = "RANGO " + DrumixProgressRules.Rank(pct);
            resStats.text = "PUNTOS " + arcade.Score.ToString("N0") + "    MEJOR COMBO " + g.MaxCombo +
                            "\nPERFECT " + g.Perfects + "    GOOD " + g.Goods + "    MISS " + g.Misses;
            resDante.text = "DANTE: " + DrumixDialogue.Result(playerName, g.Passed, g.AdvancedLevel, pct);
            K.SetText(resPrimary, g.Passed ? (g.AdvancedLevel ? "SIGUIENTE NIVEL" : "OTRA VEZ") : "REINTENTAR");
            K.SetText(resSecondary, g.AdvancedLevel ? "OTRA VEZ" : "NIVELES");
        }

        // =====================================================================================  eventos de juego
        void Score(float percent, int combo)
        {
            if (hudPercent) hudPercent.text = percent.ToString("0.0") + "%";
            if (hudCombo) hudCombo.text = combo.ToString();
            if (hudMulti) hudMulti.text = "x" + DrumixArcade.Multiplier(combo);
        }
        void Hit(DrumixHitInfo info)
        {
            arcade.Add(info.judgement, info.combo);
            if (hudPoints) hudPoints.text = arcade.Score.ToString("N0");
            bool perfect = info.judgement == DrumixJudgement.Perfect;
            Feedback(perfect ? "PERFECT!" : "GOOD", perfect ? K.Gold : K.Blue);
        }
        void Miss(DrumixHitInfo info) { Feedback("MISS", K.Miss); }
        void Combo(int c) { Feedback("COMBO x" + c + "!", K.Gold); }
        void Struck(DrumixLane lane, float strength) { int i = (int)lane; if (laneFlashAge != null && i >= 0 && i < 8) laneFlashAge[i] = 0f; }
        void Feedback(string text, Color color)
        {
            if (!feedbackText) return;
            feedbackText.text = text; feedbackText.color = color; feedbackAge = 0f;
        }

        // =====================================================================================  Update
        void Update()
        {
            UpdateMode();
            UpdateFades();
            if (Canvas && !IsFullScreen && !Canvas.worldCamera) Canvas.worldCamera = Camera.main;
            var g = DrumixGame.Instance;
            float dt = Time.unscaledDeltaTime;

            if (flow.Screen == DrumixScreen.Dante)
            {
                dialogue.Tick(dt);
                if (danteText) danteText.text = dialogue.Visible;
                K.SetText(danteNext, dialogue.LineComplete ? (dialogue.Index >= dialogue.Count - 1 ? "EMPEZAR  >" : "SIGUIENTE  >") : ">>");
            }
            if (g && flow.Screen == DrumixScreen.Countdown && countText)
            {
                float left = g.CountdownLeft;
                countText.text = left > 0 ? Mathf.CeilToInt(left).ToString() : "¡YA!";
                float frac = left - Mathf.Floor(left);
                countText.transform.localScale = Vector3.one * (1f + 0.25f * Mathf.Clamp01((frac - 0.75f) * 4f));
            }
            if (g && hudP && hudP.gameObject.activeSelf)
            {
                if (hudLevel) hudLevel.text = g.LevelTitle.ToUpperInvariant() + "  ·  DRUMIX";
                K.SetProgress(hudProgress, g.Progress);
                float total = g.Scoring.LastNoteTime + 1f;
                if (hudTime) hudTime.text = Mathf.Max(0f, g.SongTime).ToString("0.0") + " / " + total.ToString("0.0") + " s";
                var n = g.State == DrumixState.Playing ? g.NextNote() : null;
                if (hudNext) hudNext.text = n != null ? "SIGUIENTE: " + DrumixEvents.LaneName(n.lane) + "  (" + DrumixEvents.LaneKey(n.lane) + ")" : "";
            }
            UpdateHighway(g, dt);
            if (feedbackText)
            {
                feedbackAge += dt;
                float pop = feedbackAge < 0.08f ? Mathf.Lerp(1.2f, 1f, feedbackAge / 0.08f) : 1f;
                feedbackText.transform.localScale = Vector3.one * pop;
                var c = feedbackText.color; c.a = Mathf.Clamp01(1f - feedbackAge / 0.5f); feedbackText.color = c; // se desvanece en 0.5 s
            }
        }

        void UpdateHighway(DrumixGame g, float dt)
        {
            if (!highway) return;
            bool show = IsFullScreen && g && (flow.Screen == DrumixScreen.Hud || flow.Screen == DrumixScreen.Countdown || flow.Screen == DrumixScreen.Pause);
            if (highway.gameObject.activeSelf != show) highway.gameObject.SetActive(show);
            if (!show) return;
            for (int i = 0; i < 8; i++)
            {
                laneFlashAge[i] += dt;
                var c = laneFlash[i].color; c.a = Mathf.Clamp01(0.8f - laneFlashAge[i] / 0.2f); laneFlash[i].color = c;
            }
            float window = Mathf.Max(0.9f, g.leadTime * 1.6f), colW = L.Highway.w / 8f;
            int used = 0;
            var notes = g.Scoring.Notes;
            for (int i = 0; i < notes.Count && used < NotePool; i++)
            {
                var n = notes[i];
                float t = n.time - g.SongTime;
                if (t > window) break;
                if (n.judged || t < -0.15f) continue;
                var img = notePool[used++];
                img.enabled = true;
                img.color = K.LaneColor(n.lane);
                img.rectTransform.anchoredPosition = new Vector2(-L.Highway.w / 2f + colW * ((int)n.lane + 0.5f), L.NoteY(t, window, HitY, TopY));
            }
            for (int i = used; i < NotePool; i++) if (notePool[i].enabled) notePool[i].enabled = false;
        }

        // =====================================================================================  PC / VR
        static bool HeadsetActive()
        {
            return XRSettings.isDeviceActive && InputDevices.GetDeviceAtXRNode(XRNode.CenterEye).isValid;
        }
        void UpdateMode()
        {
            if (!Canvas) return;
            float dt = Time.unscaledDeltaTime;
            if (!modeDecided)
            {
                modeTimer += dt;
                if (modo == Modo.PantallaCompleta) { SetFullScreen(true); modeDecided = true; }
                else if (modo == Modo.PanelVR || Application.isMobilePlatform || HeadsetActive()) { SetFullScreen(false); modeDecided = true; }
                else if (modeTimer >= waitForHeadset) { SetFullScreen(true); modeDecided = true; Debug.Log("[DRUMIX] Sin visor: UI en pantalla completa."); }
                return;
            }
            if (modo != Modo.Auto || Application.isMobilePlatform) return;
            recheckTimer += dt;
            if (recheckTimer < 2f) return;
            recheckTimer = 0f;
            bool hmd = HeadsetActive();
            if (hmd && IsFullScreen) SetFullScreen(false);
            else if (!hmd && !IsFullScreen) SetFullScreen(true);
        }

        /// true = Screen Space Overlay a pantalla completa (PC); false = panel world-space (VR).
        public void SetFullScreen(bool on)
        {
            if (!Canvas) return;
            IsFullScreen = on;
            var rt = (RectTransform)Canvas.transform;
            var scaler = Canvas.GetComponent<CanvasScaler>();
            if (on)
            {
                Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                if (scaler)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = canvasSize; // mismas coordenadas que el panel VR (1600x900)
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0.5f;
                }
            }
            else
            {
                Canvas.renderMode = RenderMode.WorldSpace;
                if (scaler) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f; scaler.dynamicPixelsPerUnit = 3f; }
                rt.localPosition = Vector3.zero; rt.localRotation = Quaternion.identity;
                rt.sizeDelta = canvasSize; rt.localScale = Vector3.one * metersPerPixel;
                Canvas.worldCamera = Camera.main;
            }
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current || DrumixUtil.FindAny<EventSystem>()) return;
            var go = new GameObject("EventSystem (DRUMIX)", typeof(EventSystem));
            AddBestInputModule(go);
        }

        /// InputSystemUIInputModule si existe (Input System), si no StandaloneInputModule.
        public static void AddBestInputModule(GameObject go)
        {
            Type module = DrumixUtil.FindType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (module != null)
            {
                Component c = go.GetComponent(module);
                if (!c) c = go.AddComponent(module);
                var assign = module.GetMethod("AssignDefaultActions", Type.EmptyTypes);
                if (assign != null) { try { assign.Invoke(c, null); } catch (Exception) { } }
                return;
            }
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!go.GetComponent<StandaloneInputModule>()) go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
