using System;
using System.Collections.Generic;

namespace Drumix.EditorTools
{
    /// Reglas puras de acomodo (C# sin Unity, con tests): cajas alineadas a ejes (AABB), penetración,
    /// búsqueda de un lugar libre empujando el objeto, separación mínima entre NPCs y riesgo de z-fighting.
    public struct Box3
    {
        public float x0, y0, z0, x1, y1, z1;
        public Box3(float x0, float y0, float z0, float x1, float y1, float z1)
        { this.x0 = Math.Min(x0, x1); this.y0 = Math.Min(y0, y1); this.z0 = Math.Min(z0, z1); this.x1 = Math.Max(x0, x1); this.y1 = Math.Max(y0, y1); this.z1 = Math.Max(z0, z1); }
        public static Box3 FromCenter(float cx, float cy, float cz, float sx, float sy, float sz)
        { return new Box3(cx - sx / 2, cy - sy / 2, cz - sz / 2, cx + sx / 2, cy + sy / 2, cz + sz / 2); }
        public float CenterX { get { return (x0 + x1) / 2; } }
        public float CenterZ { get { return (z0 + z1) / 2; } }
        public float SizeX { get { return x1 - x0; } }
        public float SizeY { get { return y1 - y0; } }
        public float SizeZ { get { return z1 - z0; } }
        public Box3 Moved(float dx, float dy, float dz) { return new Box3(x0 + dx, y0 + dy, z0 + dz, x1 + dx, y1 + dy, z1 + dz); }
        public override string ToString() { return string.Format("[{0:0.00},{1:0.00},{2:0.00} .. {3:0.00},{4:0.00},{5:0.00}]", x0, y0, z0, x1, y1, z1); }
    }

    public static class DrumixLayout
    {
        /// Tolerancia: tocarse (o meterse menos de 1 cm) no cuenta como superposición.
        public const float Tolerance = 0.01f;
        /// Distancia mínima entre centros de NPCs (personas de ~0.5 m de ancho).
        public const float NpcMinSpacing = 0.9f;
        /// Dos superficies horizontales a menos de esto parpadean (z-fighting) a distancia en Quest.
        public const float CoplanarEpsilon = 0.004f;

        /// Margen alrededor de un NPC ya ubicado para que el siguiente quede a >= NpcMinSpacing (centro a centro).
        public static float NpcPadding(float npcWidth) { return Math.Max(0f, NpcMinSpacing - npcWidth); }

        /// Centro del semicírculo del público: con el escenario del equipo se adelanta para que todas las filas
        /// queden delante del borde de su tarima (z = 2.82) sin tener que correr a nadie.
        public static float CrowdCenterZ(bool teamStage) { return teamStage ? 1.5f : 0.5f; }

        /// Lugares candidatos para el público en una escena del equipo (C# puro, con tests): puntos (x, z) delante de la
        /// batería (centro cx,cz; frente fx,fz normalizado), de rMin a rMax cada 'step', dentro de ±maxAngle grados.
        /// Ordenados: primero las filas cercanas y centradas (como un público real), luego más lejos / más de lado.
        public static List<float[]> CrowdSlots(float cx, float cz, float fx, float fz, float rMin, float rMax, float step, float maxAngle)
        {
            var list = new List<float[]>();
            float len = (float)Math.Sqrt(fx * fx + fz * fz);
            if (len < 1e-4f || step <= 0.01f || rMax < rMin) return list;
            fx /= len; fz /= len;
            float rx = fz, rz = -fx; // derecha (giro de -90° en Y)
            for (float r = rMin; r <= rMax + 1e-4f; r += step)
            {
                // ~1 lugar por cada 'step' de arco
                float maxRad = maxAngle * (float)Math.PI / 180f;
                int n = Math.Max(1, (int)Math.Floor(2 * maxRad * r / step));
                for (int i = 0; i <= n; i++)
                {
                    float a = -maxRad + 2 * maxRad * i / n;
                    float x = cx + (float)(Math.Cos(a) * fx + Math.Sin(a) * rx) * r;
                    float z = cz + (float)(Math.Cos(a) * fz + Math.Sin(a) * rz) * r;
                    float deg = Math.Abs(a) * 180f / (float)Math.PI;
                    list.Add(new[] { x, z, r + deg * 0.03f, r, deg });
                }
            }
            list.Sort((a, b) => a[2].CompareTo(b[2]));
            return list;
        }

        /// Cuánto se meten dos cajas (el menor solape entre los 3 ejes). <= 0: no se tocan.
        public static float Penetration(Box3 a, Box3 b)
        {
            float ox = Math.Min(a.x1, b.x1) - Math.Max(a.x0, b.x0);
            float oy = Math.Min(a.y1, b.y1) - Math.Max(a.y0, b.y0);
            float oz = Math.Min(a.z1, b.z1) - Math.Max(a.z0, b.z0);
            return Math.Min(ox, Math.Min(oy, oz));
        }
        public static bool Overlaps(Box3 a, Box3 b, float tolerance = Tolerance) { return Penetration(a, b) > tolerance; }

        public static int FirstOverlap(Box3 item, IList<Box3> obstacles, float tolerance = Tolerance)
        {
            for (int i = 0; i < obstacles.Count; i++) if (Overlaps(item, obstacles[i], tolerance)) return i;
            return -1;
        }

        /// Busca dónde poner 'item' sin chocar: primero donde está, luego empujándolo en el plano XZ
        /// (hacia afuera desde el centro (cx, cz), luego de costado, luego en diagonal) en pasos de 'step'
        /// hasta 'maxDistance'. Devuelve false si no hay lugar (el builder lo omite y lo reporta).
        public static bool FindFreeOffset(Box3 item, IList<Box3> obstacles, float cx, float cz, float step, float maxDistance,
                                          out float dx, out float dz, float tolerance = Tolerance)
        {
            dx = dz = 0;
            if (FirstOverlap(item, obstacles, tolerance) < 0) return true;
            float rx = item.CenterX - cx, rz = item.CenterZ - cz;
            float len = (float)Math.Sqrt(rx * rx + rz * rz);
            if (len < 1e-4f) { rx = 0; rz = 1; } else { rx /= len; rz /= len; }
            float tx = -rz, tz = rx; // tangente
            var dirs = new[]
            {
                new[] { rx, rz }, new[] { tx, tz }, new[] { -tx, -tz },
                new[] { (rx + tx) * 0.7071f, (rz + tz) * 0.7071f }, new[] { (rx - tx) * 0.7071f, (rz - tz) * 0.7071f },
                new[] { -rx, -rz },
            };
            if (step <= 0) step = 0.1f;
            for (float d = step; d <= maxDistance + 1e-4f; d += step)
                foreach (var dir in dirs)
                {
                    float ox = dir[0] * d, oz = dir[1] * d;
                    if (FirstOverlap(item.Moved(ox, 0, oz), obstacles, tolerance) < 0) { dx = ox; dz = oz; return true; }
                }
            return false;
        }

        /// Pares (i, j) de puntos XZ más cerca que 'minDistance'.
        public static List<int[]> TooClose(IList<float[]> pointsXZ, float minDistance)
        {
            var list = new List<int[]>();
            for (int i = 0; i < pointsXZ.Count; i++)
                for (int j = i + 1; j < pointsXZ.Count; j++)
                {
                    float ddx = pointsXZ[i][0] - pointsXZ[j][0], ddz = pointsXZ[i][1] - pointsXZ[j][1];
                    if (ddx * ddx + ddz * ddz < minDistance * minDistance) list.Add(new[] { i, j });
                }
            return list;
        }

        /// Dos superficies horizontales que se ven a la vez (sus huellas XZ se cruzan) a casi la misma altura -> z-fighting.
        public static bool CoplanarRisk(Box3 surfaceA, float topA, Box3 surfaceB, float topB, float epsilon = CoplanarEpsilon)
        {
            float ox = Math.Min(surfaceA.x1, surfaceB.x1) - Math.Max(surfaceA.x0, surfaceB.x0);
            float oz = Math.Min(surfaceA.z1, surfaceB.z1) - Math.Max(surfaceA.z0, surfaceB.z0);
            return ox > 0 && oz > 0 && Math.Abs(topA - topB) < epsilon;
        }

        /// Materiales que sobran: más materiales que submeshes hacen que Unity dibuje la última submesh
        /// otra vez encima (material superpuesto). Devuelve cuántos sobran (0 = bien).
        public static int ExtraMaterialSlots(int materialCount, int subMeshCount)
        {
            return subMeshCount > 0 && materialCount > subMeshCount ? materialCount - subMeshCount : 0;
        }

        /// Firma estable para deduplicar materiales generados con los mismos parámetros.
        public static string MaterialSignature(string shader, float r, float g, float b, float a, float emission, string texture, float smooth, float metal)
        {
            Func<float, string> f = v => Math.Round(v, 3).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return shader + "|" + f(r) + "," + f(g) + "," + f(b) + "," + f(a) + "|e" + f(emission) + "|t" + (texture ?? "") + "|s" + f(smooth) + "|m" + f(metal);
        }

        /// Sufijo corto y estable (FNV-1a) para un material con mismo nombre pero otros parámetros.
        public static string ShortHash(string s)
        {
            uint h = 2166136261;
            foreach (char c in s ?? "") { h ^= c; h *= 16777619; }
            return h.ToString("x8").Substring(0, 6);
        }
    }
}
