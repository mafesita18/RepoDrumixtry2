using UnityEngine;

namespace Drumix
{
    /// Haz de luz falso (malla aditiva, sin luz real: barato en Quest) que barre suave y se aviva con los aciertos.
    public class DrumixBeamSweep : MonoBehaviour
    {
        public float amplitude = 14f;
        public float speed = 0.35f;
        public float phase;
        Quaternion baseRot; float boost;
        Renderer rend; MaterialPropertyBlock block; Color baseColor = Color.white;

        void Awake()
        {
            baseRot = transform.localRotation;
            rend = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }
        void OnEnable() { DrumixEvents.OnHit += Hit; DrumixEvents.OnCombo += Combo; }
        void OnDisable() { DrumixEvents.OnHit -= Hit; DrumixEvents.OnCombo -= Combo; }
        void Hit(DrumixHitInfo i) { boost = Mathf.Max(boost, i.judgement == DrumixJudgement.Perfect ? 0.8f : 0.4f); }
        void Combo(int c) { boost = 1.2f; }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t) * amplitude, 0f, Mathf.Cos(t * 0.7f) * amplitude * 0.5f);
            boost = Mathf.MoveTowards(boost, 0f, Time.deltaTime * 1.5f);
            if (rend)
            {
                rend.GetPropertyBlock(block);
                block.SetColor("_BaseColor", baseColor * (1f + boost));
                rend.SetPropertyBlock(block);
            }
        }
    }
}
