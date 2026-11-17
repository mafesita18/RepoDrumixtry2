using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Público plug-and-play: ponlo en CUALQUIER modelo (o cápsula) y Ctrl+D las veces que quieras.
    /// Movimiento procedural (no depende del Animator), aplaude con aciertos/combos, abuchea con fallos.
    /// Nunca se oculta: fuerza renderers visibles y una capa que la cámara dibuja.
    [DisallowMultipleComponent]
    public class DrumixCrowdNPC : MonoBehaviour
    {
        public enum Reaction { None, Cheer, BigCheer, Boo, Throw }

        [Tooltip("Lo que se mueve (hijo 'Visual'). Vacío = este objeto. La raíz solo gira hacia el baterista.")]
        public Transform visual;
        [Tooltip("Objeto con la escala del modelo (hijo 'Escala (...)'). Al dar Play se mide la malla real y, si la persona no mide 1.60-1.85 m, se corrige aquí.")]
        public Transform scaleRoot;
        [Range(1.5f, 2f)] public float targetHeight = 1.72f;
        public bool normalizeHeight = true;
        [Tooltip("Baja los brazos si el modelo viene en T-pose.")]
        public bool lowerArms = true;
        [Tooltip("0 = aleatorio.")] public int seed;
        [Range(0f, 0.2f)] public float bob = 0.025f;
        [Range(0f, 15f)] public float swayDegrees = 4f;
        public bool lookAtPlayer = true;
        [Range(0f, 60f)] public float maxLookDegrees = 30f;
        [Range(0f, 1f)] public float cheerChance = 0.35f;
        [Range(0f, 1f)] public float booChance = 0.5f;
        [Tooltip("Puede ser elegido por el director para lanzar algo.")]
        public bool canThrow = true;
        [Tooltip("Si duplicas con Ctrl+D sin mover, al dar Play se separa solo.")]
        public bool autoSpaceDuplicates = true;

        public static readonly List<DrumixCrowdNPC> All = new List<DrumixCrowdNPC>();
        public Reaction Current { get { return current; } }

        Vector3 restPos; Quaternion restRot; Vector3 restScale;
        float phase, speed, amp, age = 10f, delay, visibilityTimer;
        Reaction current, queued;
        [Tooltip("Público grande (Quest): en reposo se anima cada N cuadros (1 = siempre). Las reacciones siempre van a tiempo completo.")]
        [Range(1, 4)] public int updateEvery = 1;
        int slot; float skipped;
        Camera cam; float camTimer;
        System.Random rng;
        bool started;
        static int ordinal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { All.Clear(); ordinal = 0; }

        float R(float a, float b) { return Mathf.Lerp(a, b, (float)rng.NextDouble()); }

        void Awake()
        {
            if (!visual) visual = transform;
            rng = new System.Random(unchecked((seed == 0 ? System.Environment.TickCount : seed) ^ (++ordinal * 7919)));
            phase = R(0, Mathf.PI * 2); speed = R(0.8f, 1.35f); amp = R(0.7f, 1.3f);
            slot = ordinal;
            foreach (var animator in GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                // Animator sin controller (o con clip de otro rig) deja el modelo en T-pose / gigante: se quita, el movimiento es procedural.
                if (!animator.runtimeAnimatorController || !animator.isHuman) animator.enabled = false;
            }
            if (lowerArms) DrumixUtil.LowerArms(visual, transform.up);
            DrumixUtil.ForceVisible(gameObject);
            foreach (var lod in GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
        }

        void OnEnable()
        {
            All.Add(this);
            DrumixEvents.OnHit += OnHit; DrumixEvents.OnMiss += OnMiss;
            DrumixEvents.OnCombo += OnCombo; DrumixEvents.OnSongFinished += OnFinished;
        }
        void OnDisable()
        {
            All.Remove(this);
            DrumixEvents.OnHit -= OnHit; DrumixEvents.OnMiss -= OnMiss;
            DrumixEvents.OnCombo -= OnCombo; DrumixEvents.OnSongFinished -= OnFinished;
            if (started && visual) { visual.localPosition = restPos; visual.localRotation = restRot; visual.localScale = restScale; } // solo si ya guardó su pose
        }

        void Start()
        {
            if (autoSpaceDuplicates) SpaceFromOthers();
            if (normalizeHeight) NormalizeHeight();
            rootRest = transform.localRotation;
            restPos = visual.localPosition; restRot = visual.localRotation; restScale = visual.localScale;
            started = true;
            EnsureCameraSeesMe();
        }

        void SpaceFromOthers()
        {
            for (int tries = 0; tries < 10; tries++)
            {
                bool overlap = false;
                foreach (var other in All)
                    if (other && other != this && other.RegistryIndex() < RegistryIndex() &&
                        (other.transform.position - transform.position).sqrMagnitude < 0.3f * 0.3f) { overlap = true; break; }
                if (!overlap) return;
                transform.position += transform.right * 0.65f;
            }
        }
        int RegistryIndex() { return All.IndexOf(this); }
        Quaternion rootRest;
        public float MeasuredHeight { get; private set; }

        /// Red de seguridad: mide la malla REAL (BakeMesh) y deja a la persona entre 1.60 y 1.85 m, con los pies en el piso.
        public void NormalizeHeight()
        {
            Transform s = scaleRoot ? scaleRoot : visual;
            if (!s) return;
            Bounds b = DrumixUtil.MeasureBounds(s.gameObject, transform);
            MeasuredHeight = b.size.y;
            if (!(b.size.y > 0.01f) || float.IsNaN(b.size.y) || float.IsInfinity(b.size.y)) return; // sin malla medible: no tocar
            if (DrumixScaleRules.PersonNeedsFix(b.size.y))
            {
                float k = DrumixScaleRules.Fit(b.size.y, targetHeight);
                if (k == 1f || k < 0.005f || k > 200f || float.IsNaN(k)) { Debug.LogWarning("[DRUMIX] " + name + ": altura medida rara (" + b.size.y + " m), no se reescala."); return; }
                s.localScale *= k;
                b = DrumixUtil.MeasureBounds(s.gameObject, transform);
                MeasuredHeight = b.size.y;
            }
            // pies en el piso de la raíz y centrado (x/z) sobre ella
            Vector3 shift = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            if (shift.sqrMagnitude > 1e-6f) s.position += transform.TransformVector(shift);
        }

        void EnsureCameraSeesMe()
        {
            var c = Camera.main;
            if (!c) return;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if ((c.cullingMask & (1 << t.gameObject.layer)) == 0) t.gameObject.layer = 0;
        }

        void OnHit(DrumixHitInfo info)
        {
            if (rng.NextDouble() < cheerChance * (info.judgement == DrumixJudgement.Perfect ? 1f : 0.5f)) Queue(Reaction.Cheer);
        }
        void OnMiss(DrumixHitInfo info) { if (rng.NextDouble() < booChance * (info.stray ? 0.5f : 1f)) Queue(Reaction.Boo); }
        void OnCombo(int combo) { Queue(Reaction.BigCheer); }
        void OnFinished(float percent, bool passed) { Queue(passed ? Reaction.BigCheer : Reaction.Boo); }

        public void Queue(Reaction r)
        {
            if (current == Reaction.Throw && age < 0.7f) return;
            if (current == Reaction.BigCheer && age < 1.2f && r == Reaction.Cheer) return;
            queued = r; delay = R(0f, 0.25f); // ola natural, no todos a la vez
        }
        public void PlayThrow() { queued = Reaction.None; current = Reaction.Throw; age = 0; }

        public static DrumixCrowdNPC PickThrower()
        {
            int count = 0;
            foreach (var n in All) if (n && n.canThrow && n.isActiveAndEnabled) count++;
            if (count == 0) return null;
            int pick = Random.Range(0, count);
            foreach (var n in All)
                if (n && n.canThrow && n.isActiveAndEnabled) { if (pick == 0) return n; pick--; }
            return null;
        }

        void Update()
        {
            if (!started || !visual) return;
            float dt = Time.deltaTime;
            if (updateEvery > 1 && current == Reaction.None && queued == Reaction.None && (Time.frameCount + slot) % updateEvery != 0)
            { skipped += dt; return; } // escalonado: cada persona en un cuadro distinto
            dt += skipped; skipped = 0f;
            if (queued != Reaction.None) { delay -= dt; if (delay <= 0) { current = queued; queued = Reaction.None; age = 0; } }
            age += dt;
            float t = Time.time * speed + phase;

            // Idle: respirar + balanceo suave.
            Vector3 pos = restPos + Vector3.up * (Mathf.Sin(t * 2f) * 0.5f + 0.5f) * bob * amp;
            Quaternion rot = restRot * Quaternion.Euler(0, Mathf.Sin(t * 0.7f) * swayDegrees * 0.5f, Mathf.Sin(t) * swayDegrees * amp);
            Vector3 scale = restScale;

            switch (current)
            {
                case Reaction.Cheer:
                    if (age < 0.6f) pos += Vector3.up * Mathf.Sin(age / 0.6f * Mathf.PI) * 0.18f; else current = Reaction.None;
                    break;
                case Reaction.BigCheer:
                    if (age < 1.6f)
                    {
                        pos += Vector3.up * Mathf.Abs(Mathf.Sin(age / 0.8f * Mathf.PI * 2f)) * 0.3f;
                        rot *= Quaternion.Euler(0, Mathf.Sin(age * 12f) * 12f, 0);
                    }
                    else current = Reaction.None;
                    break;
                case Reaction.Boo:
                    if (age < 1.1f)
                    {
                        float k = Mathf.Sin(age / 1.1f * Mathf.PI);
                        rot *= Quaternion.Euler(-12f * k, Mathf.Sin(age * 25f) * 10f * k, 0);
                        scale = Vector3.Scale(restScale, new Vector3(1f + 0.04f * k, 1f - 0.05f * k, 1f + 0.04f * k));
                    }
                    else current = Reaction.None;
                    break;
                case Reaction.Throw:
                    if (age < 0.7f)
                    {
                        float k = age < 0.25f ? age / 0.25f : 1f - (age - 0.25f) / 0.45f;
                        rot *= Quaternion.Euler(-18f * k, 0, 0);
                        pos += Vector3.up * 0.06f * k;
                    }
                    else current = Reaction.None;
                    break;
            }

            visual.localPosition = pos; visual.localRotation = rot; visual.localScale = scale;

            if (lookAtPlayer)
            {
                camTimer -= dt;
                if (camTimer <= 0 || !cam) { cam = Camera.main; camTimer = 1f; }
                if (cam)
                {
                    // Gira un poco la mirada hacia el baterista sin salirse de su orientación de reposo.
                    Vector3 to = cam.transform.position - transform.position; to.y = 0;
                    Quaternion baseRest = visual == transform ? restRot : rootRest;
                    if (to.sqrMagnitude > 0.01f)
                    {
                        Quaternion restWorld = (transform.parent ? transform.parent.rotation : Quaternion.identity) * baseRest;
                        Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                        float angle = Quaternion.Angle(restWorld, look);
                        Quaternion target = Quaternion.RotateTowards(restWorld, look, Mathf.Min(angle, maxLookDegrees));
                        if (visual == transform) transform.rotation = target * (Quaternion.Inverse(restRot) * rot);
                        else transform.rotation = Quaternion.Slerp(transform.rotation, target, dt * 3f);
                    }
                }
            }

            visibilityTimer -= dt;
            if (visibilityTimer <= 0) { visibilityTimer = 2f; DrumixUtil.ForceVisible(gameObject); }
        }
    }
}
