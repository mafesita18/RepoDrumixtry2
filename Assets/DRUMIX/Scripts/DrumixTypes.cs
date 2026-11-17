using System;

// C# PURO (sin UnityEngine): lo usan el runtime y las pruebas fuera de Unity.
namespace Drumix
{
    public enum DrumixLane { Kick = 0, Snare = 1, HiHat = 2, Tom1 = 3, Tom2 = 4, FloorTom = 5, Crash = 6, Ride = 7 }
    public enum DrumixJudgement { Perfect, Good, Miss }
    public enum DrumixState { Menu, Countdown, Playing, Results }

    [Serializable]
    public struct DrumixHitInfo
    {
        public DrumixLane lane;
        public DrumixJudgement judgement;
        public float offset;      // segundos respecto a la nota (negativo = antes)
        public int combo;
        public bool stray;        // golpe sin nota cerca (cuenta como fallo de ritmo, no baja el %)
    }
}
