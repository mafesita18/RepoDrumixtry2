using UnityEngine;

namespace Drumix
{
    /// Partículas de acierto / fallo / combo / lanzamiento / victoria. Pocas partículas, aditivas, sin colisiones ni luces:
    /// pensado para Quest. El material lo genera ARMAR TODO (nada se crea en runtime).
    public class DrumixFX : MonoBehaviour
    {
        public static DrumixFX Instance { get; private set; }
        public Material particleMaterial;
        public DrumixVisualConfig config;
        public Transform celebrationPoint;

        ParticleSystem hit, miss, combo, toss, confetti;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { Instance = null; }

        void Awake()
        {
            if (Instance && Instance != this) { enabled = false; return; }
            Instance = this;
            if (!particleMaterial) { Debug.LogWarning("[DRUMIX] DrumixFX sin material: sin partículas (vuelve a ARMAR TODO)."); enabled = false; return; }
            int max = config ? config.maxParticlesPerEffect : 48;
            hit = Make("FX Acierto", max, 0.45f, 0.09f, 2.2f, 0.25f);
            miss = Make("FX Fallo", max / 2, 0.5f, 0.12f, 0.9f, -0.3f);
            combo = Make("FX Combo", max, 0.9f, 0.10f, 3.2f, 0.15f);
            toss = Make("FX Lanzamiento", max / 2, 0.45f, 0.08f, 1.2f, 0f);
            confetti = Make("FX Confeti", max * 2, 2.2f, 0.08f, 4f, 0.6f);
        }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            DrumixEvents.OnHit += Hit; DrumixEvents.OnMiss += Miss; DrumixEvents.OnCombo += Combo; DrumixEvents.OnSongFinished += Finished;
        }
        void OnDisable()
        {
            DrumixEvents.OnHit -= Hit; DrumixEvents.OnMiss -= Miss; DrumixEvents.OnCombo -= Combo; DrumixEvents.OnSongFinished -= Finished;
        }

        ParticleSystem Make(string name, int max, float life, float size, float speed, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true; main.loop = true; main.maxParticles = Mathf.Max(8, max); // siempre "Playing": Emit() se ve siempre
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.enabled = false; // solo Emit()
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = particleMaterial;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.maxParticleSize = 0.25f; // limita overdraw si la cámara se acerca (fracción de pantalla)
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Color color, int count)
        {
            if (!ps || count <= 0) return;
            if (!ps.isPlaying) ps.Play();
            var p = new ParticleSystem.EmitParams { position = pos, startColor = color, applyShapeToPosition = true };
            ps.Emit(p, count);
        }

        Vector3 PadPosition(DrumixLane lane)
        {
            var g = DrumixGame.Instance;
            var pad = g ? g.GetPad(lane) : null;
            if (pad)
            {
                var r = pad.cueRenderer;
                return (r ? r.bounds.center : pad.transform.position) + Vector3.up * 0.05f;
            }
            return celebrationPoint ? celebrationPoint.position : transform.position;
        }

        bool On(bool flag) { return !config || flag; }

        void Hit(DrumixHitInfo i)
        {
            if (i.stray || !On(config ? config.hitParticles : true)) return;
            bool perfect = i.judgement == DrumixJudgement.Perfect;
            Color c = config ? (perfect ? config.perfectColor : config.goodColor) : (perfect ? new Color(1f, 0.82f, 0.35f) : new Color(0.25f, 0.6f, 1f));
            Emit(hit, PadPosition(i.lane), c, perfect ? 14 : 8);
        }
        void Miss(DrumixHitInfo i)
        {
            if (i.stray || !On(config ? config.missParticles : true)) return;
            Emit(miss, PadPosition(i.lane), config ? config.missColor : new Color(1f, 0.2f, 0.25f), 6);
        }
        void Combo(int c)
        {
            if (!On(config ? config.comboParticles : true)) return;
            Vector3 p = celebrationPoint ? celebrationPoint.position : transform.position;
            Emit(combo, p, config ? config.comboColor : new Color(0.8f, 0.45f, 1f), Mathf.Clamp(10 + c / 2, 10, 30));
        }
        void Finished(float percent, bool passed)
        {
            if (!passed || !On(config ? config.winConfetti : true)) return;
            Vector3 p = (celebrationPoint ? celebrationPoint.position : transform.position) + Vector3.up * 1.2f;
            Color[] colors = { new Color(1f, 0.82f, 0.35f), new Color(0.8f, 0.45f, 1f), new Color(0.25f, 0.6f, 1f) };
            for (int k = 0; k < 3; k++) Emit(confetti, p, colors[k], 25);
        }

        /// Lo llama DrumixProjectile al salir disparado.
        public static void Throw(Vector3 pos)
        {
            var fx = Instance;
            if (!fx || !fx.isActiveAndEnabled || !fx.On(fx.config ? fx.config.throwParticles : true)) return;
            Emit(fx.toss, pos, new Color(1f, 0.95f, 0.85f), 6);
        }
    }
}
