using UnityEngine;

namespace Drumix
{
    /// Sonido del público: murmullo en loop + aplauso/abucheo en momentos (con cooldown).
    public class DrumixCrowdAudio : MonoBehaviour
    {
        public AudioClip murmur, cheer, boo;
        [Range(0f, 1f)] public float murmurVolume = 0.25f;
        [Range(0f, 1f)] public float reactionVolume = 0.7f;
        public float cooldown = 2.5f;
        AudioSource loop, shots;
        float last = -10f; int missStreak;

        void Awake()
        {
            loop = gameObject.AddComponent<AudioSource>();
            loop.loop = true; loop.playOnAwake = false; loop.spatialBlend = 0.3f; loop.volume = murmurVolume;
            shots = gameObject.AddComponent<AudioSource>();
            shots.playOnAwake = false; shots.spatialBlend = 0.3f;
            if (!cheer) cheer = DrumixProceduralAudio.Crowd(true);
            if (!boo) boo = DrumixProceduralAudio.Crowd(false);
        }
        void Start() { if (murmur) { loop.clip = murmur; loop.Play(); } }
        void OnEnable()
        {
            DrumixEvents.OnCombo += Combo; DrumixEvents.OnMiss += Miss; DrumixEvents.OnHit += Hit; DrumixEvents.OnSongFinished += Finished;
        }
        void OnDisable()
        {
            DrumixEvents.OnCombo -= Combo; DrumixEvents.OnMiss -= Miss; DrumixEvents.OnHit -= Hit; DrumixEvents.OnSongFinished -= Finished;
        }
        void Hit(DrumixHitInfo i) { missStreak = 0; }
        void Combo(int c) { Play(cheer, 1f); }
        void Miss(DrumixHitInfo i) { missStreak++; if (missStreak == 3 || missStreak == 6) Play(boo, 0.8f); }
        void Finished(float p, bool passed) { last = -10f; Play(passed ? cheer : boo, 1f); }
        void Play(AudioClip clip, float scale)
        {
            if (!clip || Time.time - last < cooldown) return;
            last = Time.time; shots.PlayOneShot(clip, reactionVolume * scale);
        }
    }
}
