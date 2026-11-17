using System;
using System.Collections.Generic;

// C# PURO (sin UnityEngine): secuencia, juicio de golpes, combo y %. DrumixGame solo lo envuelve.
namespace Drumix
{
    public sealed class DrumixScoring
    {
        public sealed class Note { public DrumixLane lane; public float time; public bool judged; }

        public float PerfectWindow = 0.10f;
        public float GoodWindow = 0.22f;
        public float PassPercent = 50f;
        public int ComboStep = 10;

        readonly List<Note> notes = new List<Note>();
        public IList<Note> Notes { get { return notes; } }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Perfects { get; private set; }
        public int Goods { get; private set; }
        public int Misses { get; private set; }
        public int StrayHits { get; private set; }
        public int JudgedNotes { get; private set; }
        public float Points { get; private set; }
        public int TotalNotes { get { return notes.Count; } }
        public float LastNoteTime { get { return notes.Count == 0 ? 0f : notes[notes.Count - 1].time; } }

        /// % en vivo (sobre notas ya juzgadas). Empieza en 100.
        public float Percent { get { return JudgedNotes == 0 ? 100f : Clamp(100f * Points / JudgedNotes, 0f, 100f); } }
        /// % final (sobre TODAS las notas). Es el que decide si pasas.
        public float FinalPercent { get { return notes.Count == 0 ? 0f : Clamp(100f * Points / notes.Count, 0f, 100f); } }
        public bool Passed { get { return FinalPercent >= PassPercent; } }

        static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }

        public void Load(IEnumerable<Note> chart)
        {
            notes.Clear();
            foreach (var n in chart) notes.Add(new Note { lane = n.lane, time = n.time });
            notes.Sort((a, b) => a.time.CompareTo(b.time));
            Combo = MaxCombo = Perfects = Goods = Misses = StrayHits = JudgedNotes = 0; Points = 0;
        }

        /// Patrón de rock: crash al inicio de cada 4 compases, bombo/caja, fill de toms en el compás 4.
        /// 'available' filtra piezas que no existen en la escena (se reemplazan por caja o la primera disponible).
        public static List<Note> BuildChart(float bpm, int bars, int seed, ICollection<DrumixLane> lanes, Func<DrumixLane, bool> available)
        {
            return BuildChart(bpm, bars, seed, lanes, available, DrumixPattern.Rock);
        }

        /// Patrón por nivel: Básico (negras bombo/caja), Rock (el de siempre) o Concierto (corcheas + redobles).
        public static List<Note> BuildChart(float bpm, int bars, int seed, ICollection<DrumixLane> lanes, Func<DrumixLane, bool> available, DrumixPattern pattern)
        {
            if (pattern == DrumixPattern.Rock) return BuildRock(bpm, bars, seed, lanes, available);
            var list = new List<Note>();
            if (bpm <= 0 || bars <= 0) return list;
            var used = new HashSet<DrumixLane>(lanes ?? (ICollection<DrumixLane>)new DrumixLane[0]);
            if (used.Count == 0) used.Add(DrumixLane.Snare);
            Func<DrumixLane, bool> ok = l => used.Contains(l) && (available == null || available(l));
            DrumixLane fallback = DrumixLane.Snare;
            if (!ok(fallback)) foreach (DrumixLane l in Enum.GetValues(typeof(DrumixLane))) if (ok(l)) { fallback = l; break; }
            Func<DrumixLane, DrumixLane> pick = l => ok(l) ? l : fallback;
            var rng = new Random(seed);
            float beat = 60f / bpm;
            DrumixLane[] toms = { DrumixLane.Tom1, DrumixLane.Tom2, DrumixLane.FloorTom };
            for (int bar = 0; bar < bars; bar++)
            {
                if (pattern == DrumixPattern.Basico)
                {
                    for (int b = 0; b < 4; b++)
                    {
                        DrumixLane lane = bar % 4 == 0 && b == 0 ? DrumixLane.Crash : (b % 2 == 0 ? DrumixLane.Kick : DrumixLane.Snare);
                        if (bar % 2 == 1 && b == 3 && rng.NextDouble() < 0.5) lane = DrumixLane.HiHat;
                        list.Add(new Note { lane = pick(lane), time = (bar * 4 + b) * beat });
                    }
                    continue;
                }
                // Concierto: corcheas
                bool fill = bar % 2 == 1;
                for (int e = 0; e < 8; e++)
                {
                    DrumixLane lane;
                    int b = e / 2; bool off = e % 2 == 1;
                    if (bar % 4 == 0 && e == 0) lane = DrumixLane.Crash;
                    else if (fill && b == 3) lane = toms[(e + bar) % toms.Length];
                    else if (!off) lane = b % 2 == 0 ? DrumixLane.Kick : DrumixLane.Snare;
                    else lane = bar % 4 >= 2 ? DrumixLane.Ride : DrumixLane.HiHat;
                    if (off && !fill && rng.NextDouble() < 0.25) continue; // respiro: no todas las corcheas
                    list.Add(new Note { lane = pick(lane), time = (bar * 8 + e) * beat * 0.5f });
                }
            }
            return list;
        }

        static List<Note> BuildRock(float bpm, int bars, int seed, ICollection<DrumixLane> lanes, Func<DrumixLane, bool> available)
        {
            var list = new List<Note>();
            if (bpm <= 0 || bars <= 0) return list;
            var used = new HashSet<DrumixLane>(lanes ?? (ICollection<DrumixLane>)new DrumixLane[0]);
            if (used.Count == 0) used.Add(DrumixLane.Snare);
            Func<DrumixLane, bool> ok = l => used.Contains(l) && (available == null || available(l));
            DrumixLane fallback = DrumixLane.Snare;
            if (!ok(fallback)) foreach (DrumixLane l in Enum.GetValues(typeof(DrumixLane))) if (ok(l)) { fallback = l; break; }
            var rng = new Random(seed);
            float beat = 60f / bpm;
            DrumixLane[] toms = { DrumixLane.Tom1, DrumixLane.Tom2, DrumixLane.FloorTom };
            for (int bar = 0; bar < bars; bar++)
            {
                bool fill = bar % 4 == 3;
                for (int b = 0; b < 4; b++)
                {
                    DrumixLane lane;
                    if (bar % 4 == 0 && b == 0) lane = DrumixLane.Crash;
                    else if (fill && b >= 2) lane = toms[rng.Next(toms.Length)];
                    else if (b == 0 || b == 2) lane = DrumixLane.Kick;
                    else lane = DrumixLane.Snare;
                    if (lane == DrumixLane.Kick && b == 2 && rng.NextDouble() < 0.35)
                        lane = rng.NextDouble() < 0.6 ? DrumixLane.HiHat : DrumixLane.Ride;
                    if (!ok(lane)) lane = fallback;
                    list.Add(new Note { lane = lane, time = (bar * 4 + b) * beat });
                }
            }
            return list;
        }

        /// Juzga un golpe en 'lane' en el instante 'songTime'. Devuelve el resultado (Miss + stray si no había nota).
        public DrumixHitInfo Strike(DrumixLane lane, float songTime, out bool comboMilestone)
        {
            comboMilestone = false;
            Note best = null; float bestAbs = float.MaxValue;
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (n.judged || n.lane != lane) continue;
                float abs = Math.Abs(n.time - songTime);
                if (abs < bestAbs) { bestAbs = abs; best = n; }
                if (n.time - songTime > GoodWindow) break;
            }
            if (best != null && bestAbs <= GoodWindow)
            {
                best.judged = true; JudgedNotes++;
                bool perfect = bestAbs <= PerfectWindow;
                if (perfect) { Perfects++; Points += 1f; } else { Goods++; Points += 0.6f; }
                Combo++; if (Combo > MaxCombo) MaxCombo = Combo;
                comboMilestone = ComboStep > 0 && Combo % ComboStep == 0;
                return new DrumixHitInfo { lane = lane, judgement = perfect ? DrumixJudgement.Perfect : DrumixJudgement.Good, offset = songTime - best.time, combo = Combo };
            }
            Combo = 0; StrayHits++;
            return new DrumixHitInfo { lane = lane, judgement = DrumixJudgement.Miss, combo = 0, stray = true };
        }

        /// Marca como MISS las notas que ya pasaron su ventana. Las agrega a 'output'. Devuelve cuántas.
        public int CollectMisses(float songTime, List<DrumixHitInfo> output)
        {
            int count = 0;
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (n.judged || songTime <= n.time + GoodWindow) continue;
                n.judged = true; JudgedNotes++; Misses++; Combo = 0; count++;
                if (output != null) output.Add(new DrumixHitInfo { lane = n.lane, judgement = DrumixJudgement.Miss, offset = GoodWindow, combo = 0 });
            }
            return count;
        }

        public bool IsFinished(float songTime) { return notes.Count == 0 || (JudgedNotes >= notes.Count && songTime > LastNoteTime + 1f); }

        /// 0..1: qué tan cerca viene la próxima nota de esa pieza (para que brille).
        public float Cue(DrumixLane lane, float songTime, float leadTime)
        {
            if (leadTime <= 0) return 0;
            float best = 0;
            for (int i = 0; i < notes.Count; i++)
            {
                var n = notes[i];
                if (n.judged || n.lane != lane) continue;
                float dt = n.time - songTime;
                if (dt > leadTime) break;
                float v = 1f - Clamp(dt / leadTime, 0f, 1f);
                if (v > best) best = v;
            }
            return best;
        }

        public Note Next()
        {
            for (int i = 0; i < notes.Count; i++) if (!notes[i].judged) return notes[i];
            return null;
        }
    }
}
