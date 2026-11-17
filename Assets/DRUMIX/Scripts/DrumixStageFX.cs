using UnityEngine;

namespace Drumix
{
    /// Luces del bar: destello en cada acierto (dorado/violeta), rojo en fallos, pulso en combos.
    public class DrumixStageFX : MonoBehaviour
    {
        public Light[] lights;
        public Color perfectColor = new Color(1f, 0.8f, 0.35f);
        public Color goodColor = new Color(0.25f, 0.6f, 1f);
        public Color missColor = new Color(1f, 0.15f, 0.2f);
        float[] baseIntensity; Color[] baseColor;
        float flash; Color flashColor;

        void Start()
        {
            if (lights == null) lights = new Light[0];
            baseIntensity = new float[lights.Length]; baseColor = new Color[lights.Length];
            for (int i = 0; i < lights.Length; i++) if (lights[i]) { baseIntensity[i] = lights[i].intensity; baseColor[i] = lights[i].color; }
        }
        void OnEnable() { DrumixEvents.OnHit += Hit; DrumixEvents.OnMiss += Miss; DrumixEvents.OnCombo += Combo; }
        void OnDisable() { DrumixEvents.OnHit -= Hit; DrumixEvents.OnMiss -= Miss; DrumixEvents.OnCombo -= Combo; }
        void Hit(DrumixHitInfo i) { Flash(i.judgement == DrumixJudgement.Perfect ? perfectColor : goodColor, 0.6f); }
        void Miss(DrumixHitInfo i) { if (!i.stray) Flash(missColor, 0.5f); }
        void Combo(int c) { Flash(perfectColor, 1.2f); }
        void Flash(Color c, float amount) { flashColor = c; flash = Mathf.Max(flash, amount); }
        void Update()
        {
            if (baseIntensity == null) return;
            flash = Mathf.MoveTowards(flash, 0, Time.deltaTime * 3f);
            for (int i = 0; i < lights.Length; i++)
            {
                if (!lights[i]) continue;
                lights[i].intensity = baseIntensity[i] * (DrumixSettings.Current.softLights ? 0.7f : 1f) * (1f + flash * (DrumixSettings.Current.softLights ? 0.5f : 1.5f)); // Ajustes > Luces suaves
                lights[i].color = Color.Lerp(baseColor[i], flashColor, Mathf.Clamp01(flash));
            }
        }
    }
}
