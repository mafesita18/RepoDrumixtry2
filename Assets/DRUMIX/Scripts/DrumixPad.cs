using UnityEngine;

namespace Drumix
{
    /// Pieza tocable. NO reemplaza el DrumPad del equipo: si hay uno en el mismo objeto,
    /// DrumixPad no suena (evita doble sonido) y solo reporta el golpe al bus DrumixEvents.
    [DisallowMultipleComponent]
    public class DrumixPad : MonoBehaviour
    {
        public DrumixLane lane = DrumixLane.Snare;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.9f;
        [Tooltip("Tiempo mínimo entre golpes (evita dobles por rebote).")]
        public float minInterval = 0.07f;
        [Tooltip("Velocidad mínima de la baqueta (m/s) para contar golpe.")]
        public float minStickSpeed = 0.35f;
        [Tooltip("Si el objeto ya tiene el DrumPad del equipo, no sonar dos veces.")]
        public bool muteIfTeamDrumPad = true;
        [Tooltip("Aceptar cualquier collider (no solo DrumixStick / objetos llamados stick/baqueta).")]
        public bool acceptAnyCollider = false;
        [Tooltip("Anillo/aro que brilla cuando viene una nota (lo crea el builder).")]
        public Renderer cueRenderer;
        public Color cueColor = new Color(0.75f, 0.35f, 1f);
        [Tooltip("PERFECT (dorado).")]
        public Color hitColor = new Color(1f, 0.78f, 0.15f);
        [Tooltip("Color del destello según el juicio (acierto bueno / fallo).")]
        public Color goodColor = new Color(0.25f, 0.6f, 1f);
        public Color missColor = new Color(1f, 0.12f, 0.15f);
        [Tooltip("Segundos que el pad queda con el color del juicio (luego se apaga en 0.2 s).")]
        public float judgeHold = 0.15f;
        float judgeTimer = -1f;
        Color flashColor = new Color(1f, 0.85f, 0.3f);
        void OnEnable() { DrumixEvents.OnHit += Judged; DrumixEvents.OnMiss += Judged; }
        void OnDisable() { DrumixEvents.OnHit -= Judged; DrumixEvents.OnMiss -= Judged; }
        /// El destello del pad toma el color del juicio: dorado PERFECT, celeste GOOD, rojo MISS.
        void Judged(DrumixHitInfo info)
        {
            if (info.lane != lane || info.stray) return;
            flashColor = info.judgement == DrumixJudgement.Perfect ? hitColor : info.judgement == DrumixJudgement.Good ? goodColor : missColor;
            flash = 1f; judgeTimer = judgeHold; // color puro (sin mezclar con el morado del aro) durante judgeHold
        }

        public bool HasTeamPad { get; private set; }
        public float LastStrikeTime { get; private set; } = -10f;
        AudioSource source;
        float cue, flash;
        Vector3 cueBaseScale = Vector3.one;
        MaterialPropertyBlock block;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            HasTeamPad = DrumixUtil.HasTeamDrumPad(gameObject);
            var body = GetComponent<Rigidbody>();
            if (!body) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            bool hasCollider = false;
            foreach (var c in GetComponents<Collider>())
            {
                var mesh = c as MeshCollider;
                if (mesh && !mesh.convex) { mesh.enabled = false; continue; } // trigger cóncavo = error de física
                if (!c.enabled) continue;
                c.isTrigger = true; hasCollider = true;
            }
            if (!hasCollider) { var s = gameObject.AddComponent<SphereCollider>(); s.isTrigger = true; s.radius = 0.2f / Mathf.Max(0.01f, transform.lossyScale.x); }
            source = GetComponent<AudioSource>();
            if (!source) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0.6f; source.dopplerLevel = 0f;
            if (!clip) clip = DrumixProceduralAudio.Drum(lane);
            block = new MaterialPropertyBlock();
            if (cueRenderer) { cueBaseScale = cueRenderer.transform.localScale; cueRenderer.enabled = false; }
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other) return;
            bool projectile = other.GetComponentInParent<DrumixProjectile>() != null; // lo que tira el público NUNCA cuenta
            var otherPad = other.GetComponentInParent<DrumixPad>();
            var stick = other.GetComponentInParent<DrumixStick>();
            bool named = !stick && LooksLikeStick(other.transform);
            float speed = stick ? stick.Speed : 0f;
            if (!DrumixHitRules.ShouldCount(projectile, otherPad != null && otherPad != this, stick != null, acceptAnyCollider, named, speed, minStickSpeed)) return;
            float strength = stick ? DrumixHitRules.Strength(speed) : 1f;
            if (Strike(strength, true) && stick) stick.Haptic(strength);
        }

        static bool LooksLikeStick(Transform t)
        {
            for (int i = 0; i < 3 && t; i++, t = t.parent)
                if (DrumixHitRules.LooksLikeStickName(t.name)) return true;
            return false;
        }

        /// Golpe. physical=true cuando viene de la baqueta (el DrumPad del equipo ya habrá sonado). Devuelve true si contó.
        public bool Strike(float strength = 1f, bool physical = false)
        {
            if (!DrumixHitRules.IntervalOk(Time.time, LastStrikeTime, minInterval)) return false;
            LastStrikeTime = Time.time;
            if (DrumixHitRules.ShouldPlaySound(physical, HasTeamPad, muteIfTeamDrumPad) && source && clip)
                source.PlayOneShot(clip, volume * DrumixSettings.Current.sfx * Mathf.Lerp(0.6f, 1f, strength));
            flash = 1f; flashColor = Color.white; judgeTimer = -1f; // blanco hasta que llegue el juicio (mismo frame)
            DrumixEvents.PadStruck(lane, strength);
            return true;
        }

        /// 0 = sin nota cerca, 1 = ¡ahora!
        public void SetCue(float value) { cue = Mathf.Clamp01(value); }

        void Update()
        {
            if (judgeTimer > 0f) { judgeTimer -= Time.deltaTime; flash = 1f; }
            else flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
            if (!cueRenderer) return;
            bool show = cue > 0.01f || flash > 0.01f;
            if (cueRenderer.enabled != show) cueRenderer.enabled = show;
            if (!show) return;
            Color c = judgeTimer > 0f ? flashColor : Color.Lerp(cueColor * (0.3f + 0.7f * cue), flashColor, flash);
            cueRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, c); block.SetColor(ColorId, c); block.SetColor(EmissionId, c * 2f);
            cueRenderer.SetPropertyBlock(block);
            // Aro que se cierra: grande (1.8x) cuando la nota está lejos, justo del tamaño del pad cuando hay que golpear.
            float s = (cue > 0.01f ? 1f + 0.8f * (1f - cue) : 1f) + 0.12f * flash;
            cueRenderer.transform.localScale = cueBaseScale * s;
        }
    }
}
