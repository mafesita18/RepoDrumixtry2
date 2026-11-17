using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Sonidos generados en memoria: respaldo si no hay WAV del proyecto ni los incluidos.
    public static class DrumixProceduralAudio
    {
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearCache() { clips.Clear(); }

        public static AudioClip Drum(DrumixLane lane)
        {
            string key = "drum_" + lane;
            AudioClip clip;
            if (clips.TryGetValue(key, out clip) && clip) return clip;
            const int rate = 22050;
            float duration = lane == DrumixLane.Crash || lane == DrumixLane.Ride ? 0.8f : 0.35f;
            int length = (int)(duration * rate);
            var data = new float[length];
            var noise = new System.Random(100 + (int)lane);
            double phase = 0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float n = (float)(noise.NextDouble() * 2 - 1);
                float f = 80f, decay = 15f, mix = 0.03f;
                switch (lane)
                {
                    case DrumixLane.Kick: f = 45f + 100f * Mathf.Exp(-t * 35f); decay = 16f; break;
                    case DrumixLane.Snare: f = 170f; decay = 24f; mix = 0.75f; break;
                    case DrumixLane.HiHat: f = 6000f; decay = 65f; mix = 0.96f; break;
                    case DrumixLane.Tom1: f = 190f; decay = 12f; break;
                    case DrumixLane.Tom2: f = 145f; decay = 11f; break;
                    case DrumixLane.FloorTom: f = 95f; decay = 9f; break;
                    case DrumixLane.Crash: f = 3800f; decay = 6f; mix = 0.94f; break;
                    case DrumixLane.Ride: f = 2300f; decay = 8f; mix = 0.6f; break;
                }
                phase += 2 * Math.PI * f / rate;
                data[i] = ((float)Math.Sin(phase) * (1f - mix) + n * mix) * Mathf.Exp(-t * decay) * Mathf.Min(1f, t * 1500f) * 0.6f;
            }
            return Store(key, data, rate);
        }

        /// "Blip" corto para botones (hover = agudo y suave, click = más fuerte).
        public static AudioClip Blip(bool click)
        {
            string key = click ? "ui_click" : "ui_hover";
            AudioClip clip;
            if (clips.TryGetValue(key, out clip) && clip) return clip;
            const int rate = 22050;
            int length = (int)((click ? 0.08f : 0.05f) * rate);
            var data = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float f = click ? 900f - 3000f * t : 1500f;
                data[i] = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * (click ? 40f : 70f)) * (click ? 0.35f : 0.15f);
            }
            return Store(key, data, rate);
        }

        /// Ruido de multitud (cheer = brillante, boo = grave).
        public static AudioClip Crowd(bool cheer)
        {
            string key = cheer ? "crowd_cheer" : "crowd_boo";
            AudioClip clip;
            if (clips.TryGetValue(key, out clip) && clip) return clip;
            const int rate = 22050;
            int length = (int)(1.4f * rate);
            var data = new float[length];
            var rng = new System.Random(cheer ? 7 : 9);
            float low = 0;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate;
                float n = (float)(rng.NextDouble() * 2 - 1);
                low = Mathf.Lerp(low, n, cheer ? 0.35f : 0.06f);
                float env = Mathf.Min(1f, t * 6f) * Mathf.Clamp01((1.4f - t) * 2f);
                float voice = cheer ? 0 : Mathf.Sin(2 * Mathf.PI * (140f - 30f * t) * t) * 0.25f;
                data[i] = (low * 0.6f + voice) * env * 0.5f;
            }
            return Store(key, data, rate);
        }

        static AudioClip Store(string key, float[] data, int rate)
        {
            var clip = AudioClip.Create("DRUMIX_" + key, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            clips[key] = clip;
            return clip;
        }
    }
}
