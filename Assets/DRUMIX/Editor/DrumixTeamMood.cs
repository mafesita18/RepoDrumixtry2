using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Drumix.EditorTools
{
    /// Pulido por nivel dentro de las escenas del equipo (todo dentro de DRUMIX_ROOT, nada del equipo se edita):
    /// 0 = El Garaje (luz cálida de taller, polvo, portón abierto al dar Play), 1 = El Bar Local (bar completo con neones),
    /// 2 = El Gran Concierto (batería en la tarima, focos, haces de luz, público grande abajo, niebla nocturna).
    public static partial class DrumixBuilder
    {
        public const int MoodGarage = 0, MoodBar = 1, MoodConcert = 2;

        // ---------------------------------------------------------------- Bar Local
        /// ¿La escena del equipo ya ENCIERRA a la batería en un cuarto (paredes alrededor)? Si sí, no se arma el bar (no se duplica).
        /// Rayos horizontales a 1.6 m en 8 direcciones: si 5 o más pegan en paredes del equipo a menos de 7 m, ya hay cuarto.
        /// (Antes era una caja de 12 m: la pared de la sala del VR Template a 3 m detrás o el cubo del equipo a un lado
        /// bastaban para cancelar el bar entero en 2.NivelDos.)
        static bool TeamHasRoomAround(Transform root, HashSet<Collider> geometry)
        {
            Vector3 eye = root.TransformPoint(new Vector3(0, 1.6f, 0.4f));
            int walls = 0;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = root.rotation * (Quaternion.Euler(0, i * 45f, 0) * Vector3.forward);
                foreach (var h in Physics.RaycastAll(eye, dir, 7f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    if (geometry.Contains(h.collider) && h.collider.bounds.size.y > 1.2f) { walls++; break; }
            }
            return walls >= 5;
        }

        /// Bar completo (paredes, neones, barra, mesas, parlantes, tarima) en coordenadas locales de DRUMIX_ROOT.
        static GameObject BuildTeamBar(Transform root)
        {
            var env = Child("Bar Local (ambiente DRUMIX)", root, Vector3.zero).transform;
            var floor = Prim(PrimitiveType.Plane, "Piso bar", env, new Vector3(0, 0.002f, 2.5f), new Vector3(1.84f, 1, 1.42f),
                             Mat("Piso_Bar", new Color(0.17f, 0.1f, 0.07f), 0, null, false, 0.3f), true);
            floor.GetComponent<Collider>().enabled = true;
            Prim(PrimitiveType.Cube, "Tarima", env, new Vector3(0, (TarimaTop + 0.003f) / 2f, 0.35f), new Vector3(3.2f, TarimaTop - 0.003f, 2.4f), Mat("Tarima", new Color(0.22f, 0.08f, 0.32f)));
            preferBundledFurniture = true;
            try { BuildBarRoom(env); } finally { preferBundledFurniture = false; }
            foreach (Transform t in env)
                if (t.name.StartsWith("Pared", StringComparison.Ordinal) && !t.GetComponent<Collider>()) t.gameObject.AddComponent<BoxCollider>();
            report.AppendLine("  Bar Local: paredes, neones violeta/cian, barra con botellas, taburetes, mesas, parlantes, monitores y tarima (incluidos CC0).");
            return env.gameObject;
        }

        /// Las paredes del bar no pueden meterse en lo del equipo (p. ej. la sala del VR Template): se corren hacia adentro.
        static void FitBarWalls(GameObject env, HashSet<Collider> geometry)
        {
            Transform back = env.transform.Find("Pared atras"), front = env.transform.Find("Pared fondo"),
                      left = env.transform.Find("Pared izq"), right = env.transform.Find("Pared der"), floor = env.transform.Find("Piso bar");
            int moved = 0;
            moved += Push(back, Vector3.forward, 3.5f, geometry);   // de z -4.5 hasta -1 (detrás del jugador)
            moved += Push(front, Vector3.back, 0.25f, geometry);    // detrás de la barra no hay lugar
            // Las paredes de los lados se acortan PRIMERO al largo nuevo (atrás/fondo ya corridas): si no, su punta vieja
            // (z -4.5) chocaba con lo del equipo que ya quedó afuera del bar y se apagaba la pared entera (render 2.NivelDos).
            {
                float za = back && back.gameObject.activeSelf ? back.localPosition.z : -4.5f, zb = front && front.gameObject.activeSelf ? front.localPosition.z : 9.5f;
                foreach (var side in new[] { left, right })
                    if (side) { side.localPosition = new Vector3(side.localPosition.x, side.localPosition.y, (za + zb) / 2f); side.localScale = new Vector3(side.localScale.x, side.localScale.y, zb - za + 0.2f); }
                Physics.SyncTransforms();
            }
            moved += Push(left, Vector3.right, 2.5f, geometry);
            moved += Push(right, Vector3.left, 2.5f, geometry);
            float z0 = back && back.gameObject.activeSelf ? back.localPosition.z : -4.5f, z1 = front && front.gameObject.activeSelf ? front.localPosition.z : 9.5f;
            float x0 = left && left.gameObject.activeSelf ? left.localPosition.x : -9f, x1 = right && right.gameObject.activeSelf ? right.localPosition.x : 9f;
            foreach (var side in new[] { left, right })
                if (side) { side.localPosition = new Vector3(side.localPosition.x, side.localPosition.y, (z0 + z1) / 2f); side.localScale = new Vector3(side.localScale.x, side.localScale.y, z1 - z0 + 0.2f); }
            foreach (var end in new[] { back, front })
                if (end) { end.localPosition = new Vector3((x0 + x1) / 2f, end.localPosition.y, end.localPosition.z); end.localScale = new Vector3(x1 - x0 + 0.2f, end.localScale.y, end.localScale.z); }
            if (floor) { floor.localPosition = new Vector3((x0 + x1) / 2f, floor.localPosition.y, (z0 + z1) / 2f); floor.localScale = new Vector3((x1 - x0) / 10f, 1, (z1 - z0) / 10f); }
            Physics.SyncTransforms();
            if (moved > 0) report.AppendLine("  Bar Local: " + moved + " pared(es) corrida(s) para no meterse en lo del equipo (sala de " + (x1 - x0).ToString("0.0") + " × " + (z1 - z0).ToString("0.0") + " m).");
        }
        static int Push(Transform wall, Vector3 dirLocal, float maxTravel, HashSet<Collider> geometry)
        {
            if (!wall) return 0;
            var col = wall.GetComponent<Collider>(); if (col) col.enabled = false; // no chocarse consigo misma
            float traveled = 0f;
            while (WallHits(wall, geometry))
            {
                if (traveled + 0.25f > maxTravel + 1e-3f)
                {
                    wall.gameObject.SetActive(false);
                    report.AppendLine("  ⚠ Bar Local: '" + wall.name + "' chocaba con la escena del equipo y se apagó.");
                    break;
                }
                wall.localPosition += dirLocal * 0.25f; traveled += 0.25f;
                Physics.SyncTransforms();
            }
            if (col) col.enabled = true;
            return traveled > 0f ? 1 : 0;
        }
        static bool WallHits(Transform wall, HashSet<Collider> geometry)
        {
            Vector3 half = Vector3.Max(Vector3.one * 0.01f, Vector3.Scale(wall.lossyScale, Vector3.one * 0.5f) - Vector3.one * 0.05f);
            foreach (var c in Physics.OverlapBox(wall.position, half, wall.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (geometry.Contains(c)) return true;
            return false;
        }

        /// Cajas que el público no puede pisar: muebles DRUMIX (barra, mesas, parlantes...). Piso/tarima/paredes se tratan aparte.
        static List<Bounds> EnvBlockers(GameObject env)
        {
            var list = new List<Bounds>();
            if (!env) return list;
            foreach (var r in env.GetComponentsInChildren<Renderer>(false))
            {
                string n = r.name;
                var top = r.transform; while (top.parent && top.parent != env.transform) top = top.parent;
                if (top.name.StartsWith("Piso", StringComparison.Ordinal) || top.name.StartsWith("Pared", StringComparison.Ordinal) || top.name.StartsWith("Neon", StringComparison.Ordinal)
                    || top.name.StartsWith("Tarima DRUMIX (tapa", StringComparison.Ordinal) || r is ParticleSystemRenderer) continue;
                var b = r.bounds; b.Expand(new Vector3(0.1f, 0, 0.1f)); list.Add(b);
            }
            return list;
        }

        // ---------------------------------------------------------------- superficies del equipo que parpadean
        /// Dos superficies horizontales del equipo a la MISMA altura y encimadas (p. ej. las 2 tarimas SM_01 del concierto)
        /// parpadean (z-fighting). Sin tocar lo del equipo: una tapa negra de 3 mm encima, solo en la zona encimada.
        static int CoverCoplanar(Transform root, HashSet<Collider> geometry, GameObject envParent)
        {
            var near = geometry.Where(c => c && c.bounds.size.x > 1f && c.bounds.size.z > 1f && c.bounds.size.y < 3f &&
                                           new Vector2(c.bounds.center.x - root.position.x, c.bounds.center.z - root.position.z).magnitude < 30f).ToList();
            int plates = 0; var mat = Mat("Tarima_Negra", new Color(0.05f, 0.045f, 0.06f), 0, null, false, 0.15f);
            var done = new List<Bounds>();
            for (int i = 0; i < near.Count; i++)
                for (int j = i + 1; j < near.Count; j++)
                {
                    Bounds a = near[i].bounds, b = near[j].bounds;
                    if (Mathf.Abs(a.max.y - b.max.y) > 0.004f) continue;
                    float x0 = Mathf.Max(a.min.x, b.min.x), x1 = Mathf.Min(a.max.x, b.max.x), z0 = Mathf.Max(a.min.z, b.min.z), z1 = Mathf.Min(a.max.z, b.max.z);
                    if (x1 - x0 < 0.5f || z1 - z0 < 0.5f) continue;
                    float top = Mathf.Max(a.max.y, b.max.y);
                    // confirmar con un rayo que de verdad ambas tapas están ahí (no solo las cajas)
                    var hits = Physics.RaycastAll(new Vector3((x0 + x1) / 2f, top + 0.5f, (z0 + z1) / 2f), Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);
                    if (!(hits.Any(h => h.collider == near[i] && Mathf.Abs(h.point.y - top) < 0.006f) && hits.Any(h => h.collider == near[j] && Mathf.Abs(h.point.y - top) < 0.006f))) continue;
                    var area = new Bounds(new Vector3((x0 + x1) / 2f, top, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.01f, z1 - z0));
                    if (done.Any(d => d.Intersects(area))) continue;
                    done.Add(area);
                    var plate = Prim(PrimitiveType.Cube, "Tarima DRUMIX (tapa el parpadeo de " + near[i].name + " / " + near[j].name + ")", envParent.transform, Vector3.zero, Vector3.one, mat);
                    plate.transform.SetPositionAndRotation(new Vector3(area.center.x, top + 0.0015f, area.center.z), Quaternion.identity);
                    plate.transform.localScale = new Vector3(area.size.x, 0.003f, area.size.z);
                    plates++;
                    report.AppendLine("  Tapa anti-parpadeo de " + area.size.x.ToString("0.0") + " × " + area.size.z.ToString("0.0") + " m sobre '" + near[i].name + "' y '" + near[j].name + "' (estaban a la misma altura, encimadas).");
                }
            return plates;
        }

        // ---------------------------------------------------------------- portón del garaje
        static List<GameObject> FindDoors(Scene scene, GameObject root)
        {
            var list = new List<GameObject>();
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r == root) continue;
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if ((n.Contains("garage door") || n.Contains("garagedoor") || n.Contains("puerta garaje") || n.Contains("puerta del garaje") || n.Contains("porton")) && t.GetComponentInChildren<Renderer>(true))
                        list.Add(t.gameObject);
                }
            }
            return list;
        }

        // ---------------------------------------------------------------- ambiente por nivel
        static void ApplyLevelMood(Transform root, int mood, Camera camera, Light kitLight, Light crowdLight, GameObject kit, GameObject envParent)
        {
            var moodGo = Child("Ambiente del nivel (solo en Play)", root, Vector3.zero);
            var m = moodGo.AddComponent<DrumixLevelMood>();
            m.targetCamera = camera;
            Bounds kb; if (!DrumixOverlap.TryBounds(kit, out kb)) kb = new Bounds(root.position + Vector3.up, Vector3.one);
            Vector3 kitLocal = root.InverseTransformPoint(kb.center);
            switch (mood)
            {
                case MoodGarage:
                    m.fog = true; m.fogColor = new Color(0.22f, 0.17f, 0.12f); m.fogDensity = 0.012f;
                    m.overrideAmbient = true; m.ambientSky = new Color(0.42f, 0.36f, 0.30f); m.ambientEquator = new Color(0.30f, 0.24f, 0.18f); m.ambientGround = new Color(0.10f, 0.08f, 0.06f);
                    m.solidBackground = false; // por el portón abierto se ve la calle de día
                    if (kitLight) { kitLight.color = new Color(1f, 0.82f, 0.6f); kitLight.intensity = 2.2f; kitLight.range = 5f; }
                    if (crowdLight) { crowdLight.color = new Color(1f, 0.72f, 0.45f); crowdLight.intensity = 2.2f; }
                    Haze(envParent.transform, "Polvo del taller", root.TransformPoint(kitLocal + new Vector3(0, 0.6f, 0.2f)), root.rotation, new Vector3(4.5f, 2.2f, 3.5f),
                         new Color(1f, 0.85f, 0.6f, 0.45f), 60, 0.012f, 0.03f, 12f, 0.02f);
                    report.AppendLine("  Ambiente El Garaje: luz cálida de taller, polvo flotando, niebla cálida suave (solo en Play).");
                    break;
                case MoodBar:
                    m.fog = true; m.fogColor = new Color(0.07f, 0.04f, 0.11f); m.fogDensity = 0.025f;
                    m.overrideAmbient = true; m.ambientSky = new Color(0.30f, 0.20f, 0.45f); m.ambientEquator = new Color(0.18f, 0.10f, 0.26f); m.ambientGround = new Color(0.06f, 0.04f, 0.08f);
                    m.solidBackground = true; m.background = new Color(0.035f, 0.02f, 0.06f);
                    if (kitLight) { kitLight.color = new Color(1f, 0.8f, 0.85f); kitLight.intensity = 2.4f; kitLight.range = 4.5f; }
                    if (crowdLight) { crowdLight.color = new Color(0.75f, 0.35f, 1f); crowdLight.intensity = 2.8f; }
                    Haze(envParent.transform, "Humo del bar", root.TransformPoint(new Vector3(0, 2.2f, 4.5f)), root.rotation, new Vector3(12f, 1.4f, 8f),
                         new Color(0.7f, 0.45f, 1f, 0.05f), 12, 1.6f, 3f, 14f, 0.05f);
                    report.AppendLine("  Ambiente El Bar Local: noche violeta, neones, humo suave, niebla (solo en Play).");
                    break;
                case MoodConcert:
                    m.fog = true; m.fogColor = new Color(0.05f, 0.035f, 0.09f); m.fogDensity = 0.012f;
                    m.overrideAmbient = true; m.ambientSky = new Color(0.20f, 0.16f, 0.36f); m.ambientEquator = new Color(0.12f, 0.08f, 0.22f); m.ambientGround = new Color(0.04f, 0.03f, 0.06f);
                    m.solidBackground = true; m.background = new Color(0.012f, 0.01f, 0.03f);
                    if (kitLight) { kitLight.color = new Color(1f, 0.92f, 0.8f); kitLight.intensity = 2.6f; kitLight.range = 5f; }
                    if (crowdLight) { crowdLight.color = new Color(0.35f, 0.45f, 1f); crowdLight.intensity = 3.2f; }
                    // 2 focos reales (sin sombras) que pegan en la batería desde el frente
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var sp = Child(side < 0 ? "Foco escenario izq" : "Foco escenario der", root.Find("Luces DRUMIX (batería y público)") ?? root, Vector3.zero).AddComponent<Light>();
                        sp.type = LightType.Spot; sp.shadows = LightShadows.None; sp.range = 14f; sp.spotAngle = 42f; sp.intensity = 7f;
                        sp.color = side < 0 ? new Color(1f, 0.35f, 0.85f) : new Color(0.35f, 0.8f, 1f);
                        sp.transform.position = root.TransformPoint(kitLocal + new Vector3(side * 3.2f, 5.2f, 4.2f));
                        sp.transform.rotation = Quaternion.LookRotation(kb.center - sp.transform.position, Vector3.up);
                    }
                    // haces de luz falsos (malla aditiva, barata) que barren sobre el público y se avivan con los aciertos
                    var beams = Child("Haces de luz (sin costo de luz real)", envParent.transform, Vector3.zero).transform;
                    var beamMat = BeamMaterial(); var beamMesh = BeamMesh();
                    Color[] cols = { new Color(0.85f, 0.3f, 1f), new Color(0.3f, 0.75f, 1f), new Color(1f, 0.45f, 0.75f), new Color(0.45f, 0.35f, 1f), new Color(0.3f, 1f, 0.85f), new Color(1f, 0.75f, 0.35f) };
                    for (int i = 0; i < 6; i++)
                    {
                        float x = Mathf.Lerp(-6f, 6f, i / 5f);
                        var pivot = Child("Haz " + (i + 1), beams, Vector3.zero).transform;
                        pivot.position = root.TransformPoint(kitLocal + new Vector3(x, 6.5f, 2.2f));
                        pivot.rotation = root.rotation * Quaternion.Euler(-42f, x * 2.5f, 0f); // -X: el cono (abre a -Y) cae HACIA el público (+28 apuntaba de vuelta al baterista)
                        var cone = new GameObject("Cono"); cone.transform.SetParent(pivot, false);
                        cone.transform.localScale = new Vector3(1.1f, 10f, 1.1f);
                        cone.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                        var mr = cone.AddComponent<MeshRenderer>(); mr.sharedMaterial = beamMat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                        var mesh = UnityEngine.Object.Instantiate(beamMesh); // color propio por vértice
                        mesh.name = "Haz_" + (i + 1);
                        var vc = mesh.colors; for (int k = 0; k < vc.Length; k++) vc[k] = new Color(cols[i].r, cols[i].g, cols[i].b, vc[k].a);
                        mesh.colors = vc; cone.GetComponent<MeshFilter>().sharedMesh = SaveMesh(mesh);
                        var sw = pivot.gameObject.AddComponent<DrumixBeamSweep>(); sw.phase = i * 1.1f; sw.speed = 0.3f + 0.05f * i; sw.amplitude = 12f;
                    }
                    Haze(envParent.transform, "Humo del escenario", root.TransformPoint(kitLocal + new Vector3(0, 1.2f, 1.5f)), root.rotation, new Vector3(10f, 1.2f, 5f),
                         new Color(0.6f, 0.45f, 1f, 0.06f), 14, 2f, 4f, 16f, 0.08f);
                    report.AppendLine("  Ambiente El Gran Concierto: noche, 2 focos sobre la batería, 6 haces de luz que barren y se avivan con los aciertos, humo de escenario, niebla.");
                    break;
            }
        }

        /// Partículas lentas y pocas (polvo/humo): sin colisiones ni luces; aditivas suaves.
        static ParticleSystem Haze(Transform parent, string name, Vector3 center, Quaternion rot, Vector3 size, Color color, int max, float sizeMin, float sizeMax, float life, float speed)
        {
            var mat = ParticleMaterial(); if (!mat) return null;
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(center, rot);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = true; main.prewarm = true; main.maxParticles = max;
            main.startLifetime = life; main.startSpeed = speed; main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0f;
            var em = ps.emission; em.rateOverTime = max / life;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size; sh.randomDirectionAmount = 1f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                                              new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.25f), new GradientAlphaKey(1, 0.75f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var noise = ps.noise; noise.enabled = true; noise.strength = speed * 2f; noise.frequency = 0.2f; noise.quality = ParticleSystemNoiseQuality.Low;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.renderMode = ParticleSystemRenderMode.Billboard; r.maxParticleSize = 0.5f;
            return ps;
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            EnsureFolder(Generated + "/Meshes");
            string path = Generated + "/Meshes/" + mesh.name + ".asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old) { EditorUtility.CopySerialized(mesh, old); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(old); return old; }
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        /// Cono abierto (punta en el origen, abre hacia -Y, largo 1, radio 1): alfa 1 en la punta, 0 al final.
        static Mesh BeamMesh()
        {
            const int seg = 16;
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(dir * 0.04f); c.Add(new Color(1, 1, 1, 0.22f)); uv.Add(new Vector2(0.5f, 0.5f));
                v.Add(dir + Vector3.down); c.Add(new Color(1, 1, 1, 0f)); uv.Add(new Vector2(0.5f, 0.5f));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, n = a + 2, nb = a + 3;
                tri.AddRange(new[] { a, n, b, b, n, nb });
            }
            var m = new Mesh { name = "Haz_Base" };
            m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds(); m.RecalculateNormals();
            return SaveMesh(m);
        }

        static Material BeamMaterial()
        {
            var baseMat = ParticleMaterial(); if (!baseMat) return null;
            string path = Generated + "/Materials/FX_Haz.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(baseMat);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f); // se ve por dentro y por fuera
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
