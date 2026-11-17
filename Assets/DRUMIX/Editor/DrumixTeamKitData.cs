namespace Drumix.EditorTools
{
    /// Calibración de la batería DEL EQUIPO ("drum-kit/source/Drum Set_sketchfab.fbx").
    /// Sus piezas no tienen nombre (Cylinder.003, Circle.005...), así que DRUMIX usa las zonas que el equipo ya
    /// calibró en 1.NivelUno (sensores SensorBowl / Sensorplatillo1 con DrumPad), medidas en metros relativas
    /// a la raíz del FBX (BATERIA). El baterista mira hacia +Z. C# puro (con tests).
    public static class DrumixTeamKitData
    {
        public const string ModelName = "Drum Set_sketchfab";

        public struct Zone
        {
            public DrumixLane lane; public string teamName; public string clip;
            public float x, y, z, qx, qy, qz, qw, sx, sy, sz;
        }

        public static readonly Zone[] Zones =
        {
            // Bombo: el equipo no tiene sensor (se toca con A/X o tecla A). Zona frente al parche del bombo.
            new Zone { lane = DrumixLane.Kick,     teamName = "(bombo, sin sensor)", clip = "",           x =  0.000f, y = -0.830f, z = -0.360f, qw = 1f, sx = 0.62f, sy = 0.62f, sz = 0.20f },
            new Zone { lane = DrumixLane.Snare,    teamName = "Tambor2",   clip = "tambor2",    x = -0.607f, y = -0.334f, z = -0.372f, qw = 1f, sx = 0.518f, sy = 0.331f, sz = 0.437f },
            // Hi-hat: el modelo lo tiene (Circle.006/007) pero el equipo no le puso sensor ni sonido.
            new Zone { lane = DrumixLane.HiHat,    teamName = "(hi-hat, sin sensor)", clip = "",          x = -1.180f, y =  0.090f, z = -0.180f, qw = 1f, sx = 0.56f, sy = 0.14f, sz = 0.56f },
            new Zone { lane = DrumixLane.Tom1,     teamName = "Tambor3",   clip = "tambor3",    x = -0.256f, y = -0.081f, z = -0.025f, qx = -0.158f, qy = -0.004f, qz = -0.026f, qw = 0.987f, sx = 0.394f, sy = 0.311f, sz = 0.340f },
            new Zone { lane = DrumixLane.Tom2,     teamName = "Tambor4",   clip = "tambor4",    x =  0.272f, y = -0.056f, z = -0.039f, qx = -0.158f, qy =  0.002f, qz =  0.015f, qw = 0.987f, sx = 0.394f, sy = 0.323f, sz = 0.356f },
            new Zone { lane = DrumixLane.FloorTom, teamName = "Tambor1",   clip = "tambor1",    x =  0.781f, y = -0.288f, z = -0.387f, qw = 1f, sx = 0.440f, sy = 0.176f, sz = 0.408f },
            new Zone { lane = DrumixLane.Crash,    teamName = "Platillo2", clip = "platillo2",  x = -0.765f, y =  0.425f, z =  0.043f, qx = -0.210f, qy =  0.055f, qz = -0.194f, qw = 0.957f, sx = 0.721f, sy = 0.212f, sz = 0.696f },
            new Zone { lane = DrumixLane.Ride,     teamName = "Platillo1", clip = "Platillo-1", x =  1.102f, y =  0.293f, z =  0.066f, qz = 0.078f, qw = 0.997f, sx = 0.782f, sy = 0.212f, sz = 0.760f },
        };

        /// Piezas del FBX que identifican ESTE modelo (posición relativa a la raíz, m). Si no están, no se usa la calibración.
        public struct Probe { public string node; public float x, y, z; }
        public static readonly Probe[] Signature =
        {
            new Probe { node = "Cylinder.002", x =  0.00f, y = -0.83f, z =  0.07f }, // bombo
            new Probe { node = "Cylinder.003", x = -0.62f, y = -0.33f, z = -0.43f }, // caja
            new Probe { node = "Cylinder.067", x = -0.26f, y = -0.08f, z = -0.03f }, // tom
            new Probe { node = "Circle.005",   x = -0.78f, y =  0.37f, z =  0.11f }, // crash
        };
        public const float SignatureTolerance = 0.12f;

        /// Altura de la raíz sobre la tarima para que la alfombra (Plane.001, y = -1.315) quede apoyada.
        public const float RootHeight = 1.315f;
        /// La caja (z = -0.372) queda ~0.42 m delante del jugador.
        public const float RootForward = 0.79f;
        /// Distancia de la caja delante del jugador (se mantiene al escalar la batería).
        public const float SnareAhead = 0.42f;

        // ---- Escenario del equipo: "ESCENARIO 3D/lv7bnckv1reo-stage/stage.obj" (19 x 13 m). Su tarima es plana a y = 1.463
        // (malla) y mide x -8.34..11.05, z -6.43..6.91 (coordenadas Unity del OBJ, x ya invertida). Se gira 180° como en sus
        // niveles y se baja 1.463 m: la tarima queda EXACTAMENTE en y = 0 (ahí pisan jugador y batería; sin tarima DRUMIX
        // encima = sin z-fighting). El borde delantero cae en z = 2.82: el público queda abajo, delante del borde.
        public const string StageName = "stage";
        public const float StagePlatformTop = 1.463f;
        public static readonly float[] StagePlatformMesh = { -8.34f, 11.05f, -6.43f, 6.91f }; // xMin, xMax, zMin, zMax
        public static readonly float[] StageRootPosition = { 1.35f, -StagePlatformTop, -3.61f };

        /// Huella de la tarima del equipo en el mundo DRUMIX: { xMin, xMax, zMin, zMax } (giro 180°: x = Rx - mx, z = Rz - mz).
        public static float[] StagePlatformWorld()
        {
            float rx = StageRootPosition[0], rz = StageRootPosition[2];
            var m = StagePlatformMesh;
            return new[] { rx - m[1], rx - m[0], rz - m[3], rz - m[2] };
        }
        public const float StageYaw = 180f;

        public static bool IsTeamStage(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string p = assetPath.Replace('\\', '/');
            int slash = p.LastIndexOf('/'); string file = slash >= 0 ? p.Substring(slash + 1) : p;
            int dot = file.LastIndexOf('.'); if (dot > 0) file = file.Substring(0, dot);
            return string.Equals(file, StageName, System.StringComparison.OrdinalIgnoreCase)
                && p.IndexOf("escenario", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsTeamKit(string fileName)
        {
            return string.Equals(fileName, ModelName, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
