namespace Drumix
{
    /// Reglas puras (sin Unity) del director de lanzamientos. Las usa el runtime y la autoprueba.
    public static class DrumixThrowRules
    {
        /// ¿Se puede lanzar algo ahora? Solo jugando, respetando tope de activos, tope por canción y cooldown global.
        public static bool CanThrow(bool playing, int active, int maxActive, int thrownThisSong, int maxPerSong, float sinceLastThrow, float cooldown)
        {
            if (!playing) return false;
            if (maxActive <= 0 || active >= maxActive) return false;
            if (maxPerSong <= 0 || thrownThisSong >= maxPerSong) return false;
            return sinceLastThrow >= cooldown;
        }

        /// Momento específico: racha de fallos. Dispara al llegar a 'threshold' y luego cada 'threshold' fallos más.
        public static bool IsMissMoment(int missStreak, int threshold)
        {
            if (threshold <= 0 || missStreak <= 0) return false;
            return missStreak % threshold == 0;
        }

        /// Intervalo del lanzamiento random ocasional (min..max segundos), r en [0,1].
        public static float Interval(float min, float max, float r)
        {
            if (max < min) { float t = min; min = max; max = t; }
            if (min < 1f) min = 1f;
            if (max < min) max = min;
            if (r < 0) r = 0; if (r > 1) r = 1;
            return min + (max - min) * r;
        }

        /// Velocidad inicial balística para ir de 'from' a 'to' en 'time' segundos con gravedad 'g' (negativa).
        public static void Ballistic(float fx, float fy, float fz, float tx, float ty, float tz, float time, float g,
            out float vx, out float vy, out float vz)
        {
            if (time < 0.05f) time = 0.05f;
            vx = (tx - fx) / time;
            vz = (tz - fz) / time;
            vy = (ty - fy - 0.5f * g * time * time) / time;
        }
    }
}

namespace Drumix
{
    /// Cerebro del director (C# PURO, con tests): decide CUÁNDO lanzar. El MonoBehaviour solo ejecuta.
    /// Lanza en momentos específicos (racha de fallos, perder la canción) + un random ocasional, siempre con límites.
    public sealed class DrumixThrowBrain
    {
        public int MaxActive = 6;
        public int MaxPerSong = 10;
        public float Cooldown = 3f;
        public int MissStreakForThrow = 3;
        public float MissMomentChance = 0.85f;
        public bool ThrowOnFail = true;
        public bool OccasionalRandom = true;
        public float RandomMin = 15f, RandomMax = 30f;

        public bool Playing { get; private set; }
        public int Active { get; private set; }
        public int ThrownThisSong { get; private set; }
        public int MissStreak { get; private set; }
        public float SinceLast { get; private set; } = 999f;
        public float RandomTimer { get; private set; }
        readonly System.Random rng;

        public DrumixThrowBrain(int seed) { rng = new System.Random(seed); ScheduleRandom(); }

        void ScheduleRandom() { RandomTimer = DrumixThrowRules.Interval(RandomMin, RandomMax, (float)rng.NextDouble()); }

        public void SongStarted() { ThrownThisSong = 0; MissStreak = 0; SinceLast = 999f; Active = 0; ScheduleRandom(); }
        public void SetPlaying(bool playing) { Playing = playing; }
        public void Hit() { MissStreak = 0; }

        /// Fallo: devuelve true si es un "momento" y hay que lanzar ya.
        public bool Miss()
        {
            MissStreak++;
            return DrumixThrowRules.IsMissMoment(MissStreak, MissStreakForThrow) && rng.NextDouble() < MissMomentChance && Can(Playing);
        }

        /// Fin de canción: un lanzamiento si perdiste (se permite aunque ya no esté "jugando").
        public bool Finished(bool passed) { return !passed && ThrowOnFail && Can(true); }

        /// Avanza el reloj; devuelve true si toca el random ocasional.
        public bool Tick(float dt)
        {
            SinceLast += dt;
            if (!Playing || !OccasionalRandom) return false;
            RandomTimer -= dt;
            if (RandomTimer > 0) return false;
            ScheduleRandom();
            return Can(true);
        }

        public bool Can(bool playing) { return DrumixThrowRules.CanThrow(playing, Active, MaxActive, ThrownThisSong, MaxPerSong, SinceLast, Cooldown); }
        public void Thrown() { Active++; ThrownThisSong++; SinceLast = 0f; }
        public void Returned() { if (Active > 0) Active--; }
        public void ClearActive() { Active = 0; }
    }
}
