using System;
using UnityEngine;

namespace Drumix
{
    /// <summary>
    /// Bus de eventos de DRUMIX. Estático: cualquier script (del equipo o nuestro) puede escuchar
    /// sin referencias. Nombre único para NO chocar con DrumGameEvents viejo.
    /// </summary>
    public static class DrumixEvents
    {
        /// Golpe físico/teclado a un pad (antes de juzgar). Lo escucha DrumixGame.
        public static event Action<DrumixLane, float> OnPadStruck;
        public static event Action<DrumixHitInfo> OnHit;
        public static event Action<DrumixHitInfo> OnMiss;
        /// Hitos de combo (10, 20, 30...).
        public static event Action<int> OnCombo;
        public static event Action<DrumixState> OnStateChanged;
        /// porcentaje 0-100, combo actual
        public static event Action<float, int> OnScoreChanged;
        /// porcentaje final, pasó (>= 50%)
        public static event Action<float, bool> OnSongFinished;
        /// true = pausa, false = sigue.
        public static event Action<bool> OnPauseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OnPadStruck = null; OnHit = null; OnMiss = null; OnCombo = null;
            OnStateChanged = null; OnScoreChanged = null; OnSongFinished = null; OnPauseChanged = null;
        }

        public static void PadStruck(DrumixLane lane, float strength) { if (OnPadStruck != null) OnPadStruck(lane, strength); }
        public static void ReportHit(DrumixHitInfo info) { if (OnHit != null) OnHit(info); }
        public static void ReportMiss(DrumixHitInfo info) { if (OnMiss != null) OnMiss(info); }
        public static void ReportCombo(int combo) { if (OnCombo != null) OnCombo(combo); }
        public static void ReportState(DrumixState state) { if (OnStateChanged != null) OnStateChanged(state); }
        public static void ReportScore(float percent, int combo) { if (OnScoreChanged != null) OnScoreChanged(percent, combo); }
        public static void ReportFinished(float percent, bool passed) { if (OnSongFinished != null) OnSongFinished(percent, passed); }
        public static void ReportPause(bool paused) { if (OnPauseChanged != null) OnPauseChanged(paused); }

        public static string LaneName(DrumixLane lane)
        {
            switch (lane)
            {
                case DrumixLane.Kick: return "BOMBO";
                case DrumixLane.Snare: return "CAJA";
                case DrumixLane.HiHat: return "HI-HAT";
                case DrumixLane.Tom1: return "TOM 1";
                case DrumixLane.Tom2: return "TOM 2";
                case DrumixLane.FloorTom: return "TOM PISO";
                case DrumixLane.Crash: return "CRASH";
                default: return "RIDE";
            }
        }
        /// Juzga un golpe y avisa a todos (público, lanzamientos, luces, UI, equipo). Lo usa DrumixGame (y los tests).
        public static DrumixHitInfo JudgeAndReport(DrumixScoring score, DrumixLane lane, float songTime)
        {
            bool milestone;
            var info = score.Strike(lane, songTime, out milestone);
            if (info.judgement == DrumixJudgement.Miss) ReportMiss(info); else ReportHit(info);
            if (milestone) ReportCombo(info.combo);
            ReportScore(score.Percent, score.Combo);
            return info;
        }

        /// Notas que pasaron sin tocarse: un MISS por cada una (+ puntaje). Devuelve cuántas.
        public static int CollectAndReport(DrumixScoring score, float songTime, System.Collections.Generic.List<DrumixHitInfo> buffer)
        {
            buffer.Clear();
            int n = score.CollectMisses(songTime, buffer);
            if (n <= 0) return 0;
            foreach (var miss in buffer) ReportMiss(miss);
            ReportScore(score.Percent, score.Combo);
            return n;
        }

        public static string LaneKey(DrumixLane lane) { return "ASDFGHJK".Substring((int)lane, 1); }
    }
}
