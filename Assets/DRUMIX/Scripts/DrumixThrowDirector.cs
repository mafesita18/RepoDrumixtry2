using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// UN director global para todos los lanzamientos (no 30 temporizadores compitiendo).
    /// Momentos específicos: racha de fallos (3, 6, 9...) y al perder la canción.
    /// Más un random ocasional (cada 15-30 s), siempre con límites para Quest.
    public class DrumixThrowDirector : MonoBehaviour
    {
        public static DrumixThrowDirector Instance { get; private set; }

        [Tooltip("Modelos a lanzar (inactivos en la escena o prefabs). El builder los llena solo.")]
        public GameObject[] templates;
        [Header("Límites")]
        [Min(1)] public int maxActive = 6;
        [Min(1)] public int poolSize = 12;
        [Min(0)] public int maxPerSong = 10;
        [Min(0.1f)] public float cooldown = 3f;
        [Header("Momentos")]
        [Min(1)] public int missStreakForThrow = 3;
        [Range(0f, 1f)] public float missMomentChance = 0.85f;
        public bool throwOnFail = true;
        public bool occasionalRandom = true;
        public Vector2 randomInterval = new Vector2(15f, 30f);
        [Header("Trayectoria")]
        public Vector2 flightTime = new Vector2(0.9f, 1.25f);
        [Tooltip("Cae a este lado del jugador (m), nunca encima de la batería.")]
        public float sideOffset = 0.75f;

        public int ActiveCount { get { return brain == null ? 0 : brain.Active; } }
        public int ThrownThisSong { get { return brain == null ? 0 : brain.ThrownThisSong; } }
        readonly List<DrumixProjectile> pool = new List<DrumixProjectile>();
        readonly List<Collider> ignore = new List<Collider>();
        DrumixThrowBrain brain;
        bool duplicate;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { Instance = null; }

        void Awake()
        {
            if (Instance && Instance != this) { duplicate = true; enabled = false; Debug.LogWarning("[DRUMIX] Director de lanzamientos duplicado desactivado: " + name); return; }
            Instance = this;
            brain = new DrumixThrowBrain(System.Environment.TickCount);
            SyncBrain();
        }
        void SyncBrain()
        {
            if (brain == null) return;
            brain.MaxActive = Mathf.Min(maxActive, poolSize); brain.MaxPerSong = maxPerSong; brain.Cooldown = cooldown;
            brain.MissStreakForThrow = missStreakForThrow; brain.MissMomentChance = missMomentChance;
            brain.ThrowOnFail = throwOnFail; brain.OccasionalRandom = occasionalRandom;
            brain.RandomMin = randomInterval.x; brain.RandomMax = randomInterval.y;
        }
        void OnEnable()
        {
            if (duplicate) return;
            DrumixEvents.OnMiss += Miss; DrumixEvents.OnHit += Hit;
            DrumixEvents.OnStateChanged += State; DrumixEvents.OnSongFinished += Finished;
        }
        void OnDisable()
        {
            DrumixEvents.OnMiss -= Miss; DrumixEvents.OnHit -= Hit;
            DrumixEvents.OnStateChanged -= State; DrumixEvents.OnSongFinished -= Finished;
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start()
        {
            if (templates != null)
                foreach (var t in templates) if (t && t.scene.IsValid()) t.SetActive(false); // las plantillas de escena no se ven
            int prewarm = Mathf.Min(poolSize, Mathf.Max(1, templates == null ? 1 : templates.Length));
            for (int i = 0; i < prewarm; i++) Create(i);
        }

        void State(DrumixState state)
        {
            SyncBrain();
            brain.SetPlaying(state == DrumixState.Playing);
            if (state == DrumixState.Countdown) { ClearAll(); brain.SongStarted(); }
            if (state == DrumixState.Menu) ClearAll();
        }
        void Hit(DrumixHitInfo info) { brain.Hit(); }
        void Miss(DrumixHitInfo info) { if (brain.Miss()) Launch(); }
        void Finished(float percent, bool passed) { if (brain.Finished(passed)) Launch(); }

        void Update()
        {
            var g = DrumixGame.Instance;
            if (g && g.Paused) return; // en pausa no lanzan
            if (brain != null && brain.Tick(Time.deltaTime)) Launch();
        }

        /// Lanza UN objeto ahora si las reglas lo permiten (útil para probar desde otro script).
        public bool TryThrow() { return brain != null && brain.Can(true) && Launch(); }

        bool Launch()
        {
            if (!DrumixSettings.Current.crowdProps) return false; // Ajustes > Objetos del público apagado
            var npc = DrumixCrowdNPC.PickThrower();
            var cam = Camera.main;
            if (!npc || !cam) return false;
            var item = Get();
            if (!item) return false;
            Vector3 from = npc.transform.position + Vector3.up * 1.5f + npc.transform.forward * 0.3f;
            Vector3 side = cam.transform.right * (Random.value < 0.5f ? -1f : 1f) * sideOffset;
            Vector3 to = new Vector3(cam.transform.position.x, Mathf.Max(0.2f, cam.transform.position.y - 0.6f), cam.transform.position.z) + side;
            float time = Random.Range(Mathf.Min(flightTime.x, flightTime.y), Mathf.Max(flightTime.x, flightTime.y));
            float vx, vy, vz;
            DrumixThrowRules.Ballistic(from.x, from.y, from.z, to.x, to.y, to.z, time, Physics.gravity.y, out vx, out vy, out vz);
            IgnoreKit(item);
            item.Launch(from, new Vector3(vx, vy, vz));
            npc.PlayThrow();
            brain.Thrown();
            return true;
        }

        DrumixProjectile Get()
        {
            foreach (var p in pool) if (p && !p.gameObject.activeSelf) return p;
            if (pool.Count >= poolSize) return null;
            return Create(Random.Range(0, 1000));
        }

        DrumixProjectile Create(int index)
        {
            GameObject go;
            if (templates != null && templates.Length > 0 && templates[index % templates.Length])
            {
                var template = templates[index % templates.Length];
                go = Instantiate(template, transform);
                go.name = template.name + " (lanzable)";
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.12f;
                go.name = "Lanzable (esfera)";
            }
            go.SetActive(false);
            var p = go.GetComponent<DrumixProjectile>();
            if (!p) p = go.AddComponent<DrumixProjectile>();
            p.owner = this;
            var b = p.Body; b.isKinematic = true;
            pool.Add(p);
            return p;
        }

        void IgnoreKit(DrumixProjectile item)
        {
            ignore.Clear();
            foreach (var pad in DrumixUtil.FindAll<DrumixPad>()) ignore.AddRange(pad.GetComponentsInChildren<Collider>(true));
            foreach (var stick in DrumixUtil.FindAll<DrumixStick>()) ignore.AddRange(stick.GetComponentsInChildren<Collider>(true));
            foreach (var kit in DrumixUtil.FindAll<DrumixKitLock>()) ignore.AddRange(kit.GetComponentsInChildren<Collider>(true));
            foreach (var mine in item.GetComponentsInChildren<Collider>(true))
                foreach (var other in ignore) if (other && other != mine) Physics.IgnoreCollision(mine, other, true);
        }

        public void Return(DrumixProjectile item)
        {
            if (item && item.gameObject.activeSelf) { item.gameObject.SetActive(false); if (brain != null) brain.Returned(); }
        }

        void ClearAll() { foreach (var p in pool) if (p && p.gameObject.activeSelf) p.gameObject.SetActive(false); if (brain != null) brain.ClearActive(); }
    }
}
