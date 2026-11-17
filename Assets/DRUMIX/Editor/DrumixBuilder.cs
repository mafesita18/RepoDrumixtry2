using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Drumix.EditorTools
{
    /// DRUMIX > ARMAR TODO (1 click). Crea/abre Assets/DRUMIX/Scenes/DRUMIX_Bar.unity y arma todo
    /// debajo de UN objeto raíz "DRUMIX_ROOT". Volver a ejecutarlo reemplaza esa raíz (sin duplicados).
    /// NUNCA abre ni modifica escenas del equipo (solo te pide guardar la que tengas abierta).
    public static partial class DrumixBuilder
    {
        public const string RootName = "DRUMIX_ROOT";
        public const string ScenePath = "Assets/DRUMIX/Scenes/DRUMIX_Bar.unity";
        /// Sube cuando cambia lo que arma ARMAR TODO: las escenas armadas con una versión menor se rearman solas al abrir Unity.
        /// Hornear el público grande a malla estática. APAGADO (más seguro): queda animado normal (skinned).
        public const bool BakeCrowdMeshes = false;
        public const int BuildVersion = 8; // 8: vista PC sin pelear con el simulador XR, un solo XR Origin a escala 1, limpieza de DRUMIX_ROOT fuera de los niveles.  7: el horneado del público recalcula bounds (en v6 quedaba skinned).  6: público grande del concierto (48, estático), bar no se cancela, paredes del bar, haces hacia el público, público a ≥ 2.8 m
        const string Generated = "Assets/DRUMIX/Generated";

        struct PieceSpec
        {
            public DrumixLane lane; public Vector3 pos; public Vector3 euler; public float size; public bool cymbal; public float height;
            public string[] keywords; public string[] exclude; public string bundledClip;
        }

        static readonly PieceSpec[] Pieces =
        {
            new PieceSpec { lane = DrumixLane.Kick, pos = new Vector3(0f, 0.28f, 0.66f), euler = new Vector3(-90, 0, 0), size = 0.56f, height = 0.4f,
                keywords = new[] { "kick", "bombo", "bassdrum", "bass drum", "kickdrum" }, exclude = new[] { "pedal", "beater", "sound", "sfx" }, bundledClip = "Drums/Kick.wav" },
            new PieceSpec { lane = DrumixLane.Snare, pos = new Vector3(-0.28f, 0.74f, 0.42f), euler = new Vector3(-10, 0, 0), size = 0.36f, height = 0.15f,
                keywords = new[] { "snare", "caja", "redoblante", "tarola" }, exclude = new[] { "stand", "sound", "sfx" }, bundledClip = "Drums/Snare.wav" },
            new PieceSpec { lane = DrumixLane.HiHat, pos = new Vector3(-0.6f, 0.96f, 0.42f), euler = new Vector3(-6, 0, 0), size = 0.36f, cymbal = true,
                keywords = new[] { "hihat", "hi hat", "hi-hat", "charles", "contratiempo" }, exclude = new[] { "stand", "pedal", "sound", "sfx" }, bundledClip = "Drums/HiHat.wav" },
            new PieceSpec { lane = DrumixLane.Tom1, pos = new Vector3(-0.15f, 0.94f, 0.64f), euler = new Vector3(-18, 0, 0), size = 0.28f, height = 0.22f,
                keywords = new[] { "tom1", "hightom", "high tom", "racktom", "rack tom", "tom" }, exclude = new[] { "floor", "piso", "tom2", "sound", "sfx" }, bundledClip = "Drums/Tom1.wav" },
            new PieceSpec { lane = DrumixLane.Tom2, pos = new Vector3(0.16f, 0.94f, 0.64f), euler = new Vector3(-18, 0, 0), size = 0.30f, height = 0.24f,
                keywords = new[] { "tom2", "midtom", "mid tom", "lowtom", "tom" }, exclude = new[] { "floor", "piso", "tom1", "sound", "sfx" }, bundledClip = "Drums/Tom2.wav" },
            new PieceSpec { lane = DrumixLane.FloorTom, pos = new Vector3(0.48f, 0.62f, 0.42f), euler = new Vector3(-6, 0, 0), size = 0.40f, height = 0.36f,
                keywords = new[] { "floortom", "floor tom", "tom de piso", "tompiso", "base tom" }, exclude = new[] { "sound", "sfx" }, bundledClip = "Drums/FloorTom.wav" },
            new PieceSpec { lane = DrumixLane.Crash, pos = new Vector3(-0.5f, 1.26f, 0.8f), euler = new Vector3(-14, 0, 8), size = 0.45f, cymbal = true,
                keywords = new[] { "crash", "platillo", "cymbal", "plato" }, exclude = new[] { "ride", "stand", "sound", "sfx" }, bundledClip = "Drums/Crash.wav" },
            new PieceSpec { lane = DrumixLane.Ride, pos = new Vector3(0.62f, 1.16f, 0.74f), euler = new Vector3(-12, 0, -8), size = 0.50f, cymbal = true,
                keywords = new[] { "ride", "cymbal", "platillo", "plato" }, exclude = new[] { "crash", "stand", "sound", "sfx" }, bundledClip = "Drums/Ride.wav" },
        };

        static StringBuilder report;
        static Shader lit, unlit;

        // =====================================================================
        // Opciones (por PC, EditorPrefs). Se cambian en el menú DRUMIX > Opciones.
        const string PrefStage = "DRUMIX_UsarEscenarioProyecto";
        const string PrefLevelSystems = "DRUMIX_IncluirLevelSystems";
        const string MenuStage = "DRUMIX/Opciones/Usar escenario 3D del proyecto";
        const string MenuLevelSystems = "DRUMIX/Opciones/Incluir LevelSystems del equipo";
        static bool UseProjectStage { get { return EditorPrefs.GetBool(PrefStage, true); } }
        static bool IncludeLevelSystems { get { return EditorPrefs.GetBool(PrefLevelSystems, false); } }
        [MenuItem(MenuStage, false, 60)] static void ToggleStage() { EditorPrefs.SetBool(PrefStage, !UseProjectStage); }
        [MenuItem(MenuStage, true)] static bool ToggleStageCheck() { Menu.SetChecked(MenuStage, UseProjectStage); return true; }
        [MenuItem(MenuLevelSystems, false, 61)] static void ToggleLevelSystems() { EditorPrefs.SetBool(PrefLevelSystems, !IncludeLevelSystems); }
        [MenuItem(MenuLevelSystems, true)] static bool ToggleLevelSystemsCheck() { Menu.SetChecked(MenuLevelSystems, IncludeLevelSystems); return true; }

        [MenuItem("DRUMIX/ARMAR TODO (1 click)", false, 0)]
        static void BuildAllMenu() { BuildAll(true); }

        /// interactive=false: sin diálogos (lo usa la configuración automática del proyecto standalone).
        public static bool BuildAll(bool interactive)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { if (interactive) EditorUtility.DisplayDialog("DRUMIX", "Primero sal de Play.", "OK"); return false; }
            if (interactive && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            report = new StringBuilder(); loggedFurniture.Clear(); stickLogged = false;
            matBySignature.Clear(); signatureByName.Clear(); materialsShared = 0; slotFixes = 0; duplicatesHidden = 0;
            DrumixAssetFinder.ClearCache();
            EnsureFolder(Generated); EnsureFolder(Generated + "/Materials"); EnsureFolder("Assets/DRUMIX/Scenes");
            ResolveShaders();

            Scene scene;
            if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            visual = EnsureVisualConfig();
            var levelAssets = EnsureLevels();
            var kept = SaveUserObjects(scene);
            RemoveOldRoots(scene, legacyRoot);

            try
            {
                var root = new GameObject(RootName);
                BuildEnvironment(root.transform);
                Light[] stageLights = BuildLights(root.transform);

                var game = new GameObject("DRUMIX_Juego (eventos + secuencia)");
                game.transform.SetParent(root.transform, false);
                var drumGame = game.AddComponent<DrumixGame>(); game.AddComponent<DrumixInput>(); game.AddComponent<DrumixTeamBridge>();
                drumGame.levels = levelAssets; drumGame.levelIndex = 0;
                report.AppendLine("Niveles: " + string.Join(" · ", levelAssets.Where(l => l).Select(l => l.spec.name + " (" + l.spec.bpm + " bpm, " + l.spec.pattern + ")").ToArray()) +
                                  " → editables en " + ConfigDir + "/Niveles");
                TeamLevelSystems(root.transform);

                var kit = BuildKit(root.transform);
                SnapToSurface(kit, kitSurfaceY + 0.006f);
                Camera camera; Transform left, right; bool xriPrefab;
                BuildRig(root.transform, out camera, out left, out right, out xriPrefab);
                LiftRig(root.transform, camera, kitSurfaceY);
                BuildSticks(left, XRNode.LeftHand); BuildSticks(right, XRNode.RightHand);
                BuildCrowd(root.transform);
                BuildUI(root.transform, camera, xriPrefab, left, right);
                ResolveLayout(root);
                var fx = stageLights.Length > 0 ? stageLights[0].transform.parent.gameObject.AddComponent<DrumixStageFX>() : null;
                if (fx) fx.lights = stageLights;
                ConfigureRender(camera);
                BuildFX(root.transform, kit);
                BuildPostFX(root.transform, camera);

                foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);
                var info = root.AddComponent<DrumixRootInfo>();
                info.builtWith = "DRUMIX " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                info.builtVersion = BuildVersion;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) info.generated.Add(t.gameObject);
                RestoreUserObjects(root, kept);
                Selection.activeGameObject = kit;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                report.AppendLine("ERROR: " + e.Message + " (pega este error en el chat)");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AddSceneToBuildSettings();

            // Escenas DEL EQUIPO (1.NivelUno, 2.NivelDos, 3.NivelTres): DRUMIX_ROOT adentro de cada una (solo se agrega eso).
            string mainReport = report.ToString();
            var team = BuildTeamScenes(levelAssets);
            report.Insert(0, mainReport);
            try { int cleaned = DrumixSceneGuard.CleanProjectScenes(); if (cleaned > 0) report.AppendLine("Limpieza: quité DRUMIX_ROOT de " + cleaned + " escena(s) que no son niveles."); }
            catch (Exception e) { Debug.LogException(e); }
            string openPath = team.Count > 0 ? team[0] : ScenePath;
            if (SceneManager.GetActiveScene().path != openPath) EditorSceneManager.OpenScene(openPath, OpenSceneMode.Single);

            string summary = "DRUMIX armado en " + (team.Count > 0 ? string.Join(", ", team.Select(Path.GetFileNameWithoutExtension).ToArray()) + " (escenas del equipo) y " : "") + ScenePath + "\n\n" + report;
            Debug.Log("[DRUMIX] " + summary);
            if (interactive) EditorUtility.DisplayDialog("DRUMIX listo", Shorten(summary, 1500) + "\n\nDale Play. PC: Enter y A S D F G H J K.\n(Resumen completo en la Console)", "¡Dale!");
            return true;
        }

        /// Agrega DRUMIX_Bar AL FINAL de Build Settings (solo lo llaman los proyectos DRUMIX_Proyecto*).
        /// Nunca cambia el orden: los niveles del equipo (1.NivelUno = 0, 2.NivelDos = 1, 3.NivelTres = 2) dependen del buildIndex.
        public static void AddSceneToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in list) if (s.path == ScenePath) { s.enabled = true; EditorBuildSettings.scenes = list.ToArray(); return; }
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        /// LevelSystems.prefab del equipo: nunca se duplica. Solo se agrega si la opción está activa y no hay LevelManager en la escena.
        static void TeamLevelSystems(Transform root)
        {
            GameObject prefab = null;
            foreach (var guid in AssetDatabase.FindAssets("LevelSystems t:Prefab", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/DRUMIX/", StringComparison.Ordinal)) continue;
                if (Path.GetFileNameWithoutExtension(path).Equals("LevelSystems", StringComparison.OrdinalIgnoreCase)) { prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); break; }
            }
            Type levelManager = DrumixUtil.FindType("LevelManager");
            Type audioManager = DrumixUtil.FindType("AudioManager");
            bool already = DrumixUtil.FindAll(levelManager).Length > 0
                        || DrumixUtil.FindAll(audioManager).Length > 0;
            if (!prefab) { report.AppendLine("LevelSystems del equipo: no está en este proyecto (DRUMIX funciona solo)."); return; }
            if (already) { report.AppendLine("LevelSystems/AudioManager del equipo: ya hay uno en la escena, no se duplica."); return; }
            if (!IncludeLevelSystems) { report.AppendLine("LevelSystems del equipo detectado (" + AssetDatabase.GetAssetPath(prefab) + "): NO se agrega. Si lo quieres: DRUMIX > Opciones > Incluir LevelSystems del equipo y ARMAR TODO."); return; }
            var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go) { go.transform.SetParent(root, false); report.AppendLine("LevelSystems del equipo agregado (1 sola vez, dentro de DRUMIX_ROOT)."); }
        }

        static string Shorten(string s, int max) { return s.Length <= max ? s : s.Substring(0, max) + "..."; }

        static void RemoveOldRoots(Scene scene, bool legacy)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName || go.name.StartsWith("EventSystem (DRUMIX", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(go);
            if (!legacy) return;
            // Restos sueltos de una versión MUY anterior (cámara/luz por defecto sin nada más) en NUESTRA escena.
            foreach (var go in scene.GetRootGameObjects())
                if ((go.name == "Main Camera" || go.name == "Directional Light") && go.transform.childCount == 0 &&
                    go.GetComponents<Component>().Length <= 4) UnityEngine.Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- ARMAR TODO no destructivo
        class KeptObject { public GameObject go; public string[] parentPath; public int sibling; }
        static bool legacyRoot;

        /// Lo que alguien agregó a mano DENTRO de DRUMIX_ROOT (no está en la lista de DrumixRootInfo) se saca antes
        /// de rearmar y después se devuelve al mismo padre (por nombre). Lo que está FUERA de DRUMIX_ROOT nunca se toca.
        static List<KeptObject> SaveUserObjects(Scene scene)
        {
            var kept = new List<KeptObject>();
            legacyRoot = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != RootName) continue;
                var info = root.GetComponent<DrumixRootInfo>();
                if (!info) { legacyRoot = true; continue; } // armado con una versión vieja: no se sabe qué es de quién
                var generated = new HashSet<GameObject>(info.generated.Where(g => g));
                var stack = new Stack<Transform>(); stack.Push(root.transform);
                var found = new List<Transform>();
                while (stack.Count > 0)
                {
                    var t = stack.Pop();
                    foreach (Transform c in t)
                    {
                        if (!generated.Contains(c.gameObject)) { found.Add(c); continue; } // no bajar dentro de lo del usuario
                        stack.Push(c);
                    }
                }
                foreach (var t in found)
                {
                    var path = new List<string>();
                    for (var p = t.parent; p && p != root.transform; p = p.parent) path.Insert(0, p.name);
                    kept.Add(new KeptObject { go = t.gameObject, parentPath = path.ToArray(), sibling = t.GetSiblingIndex() });
                    t.SetParent(null, true);
                }
            }
            return kept;
        }

        static void RestoreUserObjects(GameObject root, List<KeptObject> kept)
        {
            int back = 0, loose = 0;
            foreach (var k in kept)
            {
                if (!k.go) continue;
                Transform parent = root.transform;
                foreach (var name in k.parentPath)
                {
                    Transform next = null;
                    foreach (Transform c in parent) if (c.name == name) { next = c; break; }
                    if (!next) { parent = null; break; }
                    parent = next;
                }
                if (parent) { k.go.transform.SetParent(parent, true); k.go.transform.SetSiblingIndex(Mathf.Min(k.sibling, parent.childCount - 1)); back++; }
                else { k.go.transform.SetParent(root.transform, true); loose++; }
            }
            if (kept.Count > 0)
                report.AppendLine("Cosas agregadas a mano: " + back + " devueltas a su lugar" + (loose > 0 ? ", " + loose + " quedaron directo bajo DRUMIX_ROOT (su padre ya no existe)" : "") + ".");
        }

        // ---------------------------------------------------------------- helpers
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void ResolveShaders()
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
            if (!lit) lit = Shader.Find("Standard");
            if (!unlit) unlit = lit;
            report.AppendLine("Render: " + (urp ? "URP" : "Built-in") + " · shader " + (lit ? lit.name : "?"));
        }

        // Materiales generados deduplicados: mismos parámetros -> el MISMO asset compartido (nada de copias idénticas);
        // mismo nombre con otros parámetros -> nombre con sufijo (antes uno pisaba al otro).
        static int slotFixes, duplicatesHidden;
        // Alturas de apoyo (las fija BuildEnvironment): piso visible, superficie de la batería/jugador.
        const float TarimaTop = 0.04f;
        static bool teamStage;
        /// Filas del público {radio, personas, ángulo}; null = las de siempre (2 filas, 15 personas).
        static float[][] crowdRowsOverride;
        /// Concierto: muchas personas → malla horneada compartida, instancing, sin sombras, animación escalonada.
        static bool crowdBig; static int crowdBaked;
        static readonly Dictionary<Mesh, Mesh> bakedCrowd = new Dictionary<Mesh, Mesh>();

        /// Cambia cada SkinnedMeshRenderer por una malla estática con la MISMA pose (brazos abajo). Se valida midiendo:
        /// si la altura/pies no coinciden (FBX con escalas raras), se deja el skinned como estaba.
        static bool BakeStatic(GameObject visual, Transform space)
        {
            bool any = false;
            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.sharedMesh || !smr.enabled) continue;
                Bounds before = LocalBounds(visual, space);
                Mesh cached; bakedCrowd.TryGetValue(smr.sharedMesh, out cached);
                var tries = new List<Func<Mesh>>();
                if (cached) tries.Add(() => cached);
                tries.Add(() => { var m = new Mesh(); smr.BakeMesh(m, true); m.RecalculateBounds(); return m; });  // con escala: mismos TRS que el skinned
                tries.Add(() => { var m = new Mesh(); smr.BakeMesh(m, false); m.RecalculateBounds(); return m; });
                bool ok = false;
                foreach (var make in tries)
                {
                    var mesh = make();
                    var go = new GameObject(smr.name + " (estático)");
                    go.transform.SetParent(smr.transform.parent, false);
                    go.transform.localPosition = smr.transform.localPosition; go.transform.localRotation = smr.transform.localRotation; go.transform.localScale = smr.transform.localScale;
                    go.layer = smr.gameObject.layer;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterials = smr.sharedMaterials;
                    smr.enabled = false;
                    Bounds after = LocalBounds(visual, space);
                    if (after.size.y > 0.01f && Mathf.Abs(after.size.y - before.size.y) <= 0.06f * before.size.y && Mathf.Abs(after.min.y - before.min.y) <= 0.06f)
                    {
                        if (!cached && mesh != cached)
                        {
                            mesh.name = "Publico_Estatico_" + Safe(smr.sharedMesh.name);
                            mesh = SaveMesh(mesh); go.GetComponent<MeshFilter>().sharedMesh = mesh;
                            bakedCrowd[smr.sharedMesh] = mesh; cached = mesh;
                        }
                        foreach (var m in mr.sharedMaterials) if (m && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
                        ok = true; any = true; break;
                    }
                    smr.enabled = true; UnityEngine.Object.DestroyImmediate(go);
                    if (mesh != cached) UnityEngine.Object.DestroyImmediate(mesh);
                }
                if (!ok) Debug.LogWarning("[DRUMIX] " + space.name + ": no se pudo hornear la malla sin cambiar la medida (alto " + before.size.y.ToString("0.00") + " m, pies " + before.min.y.ToString("0.00") + "); se deja animada (skinned).");
            }
            return any;
        }
        static float floorY, kitSurfaceY = TarimaTop;
        static readonly Dictionary<string, Material> matBySignature = new Dictionary<string, Material>();
        static readonly Dictionary<string, string> signatureByName = new Dictionary<string, string>();
        static int materialsShared;

        /// Solo texturas 2D a _BaseMap/_MainTex: si una vino importada como Cube/3D/array (meta viejo o roto) se avisa y no se asigna,
        /// en vez de "Error assigning CUBE texture to 2D texture property".
        static Texture Tex2DOrNull(Texture t, string who)
        {
            if (!t) return null;
            if (t.dimension == TextureDimension.Tex2D) return t;
            string msg = "Textura " + AssetDatabase.GetAssetPath(t) + " está importada como " + t.dimension + " (no 2D); no se usa en " + who +
                         ". Arreglo: clic derecho en Assets/DRUMIX > Reimport (o en la textura: Texture Shape = 2D).";
            Debug.LogWarning("[DRUMIX] " + msg); report.AppendLine("  ⚠ " + msg);
            return null;
        }

        static Material Mat(string name, Color color, float emission = 0f, Texture texture = null, bool unlitShader = false, float smooth = 0.4f, float metal = 0f)
        {
            var shader = unlitShader ? unlit : lit;
            string texKey = texture ? (AssetDatabase.GetAssetPath(texture) + ":" + texture.name) : null;
            string sig = DrumixLayout.MaterialSignature(shader ? shader.name : "?", color.r, color.g, color.b, color.a, emission, texKey, smooth, metal);
            Material cached;
            if (matBySignature.TryGetValue(sig, out cached) && cached) { materialsShared++; return cached; }
            string known;
            if (signatureByName.TryGetValue(name, out known) && known != sig) name = name + "_" + DrumixLayout.ShortHash(sig);
            signatureByName[name] = sig;

            string path = Generated + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            Texture tex = Tex2DOrNull(texture, name);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emission > 0 && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(m);
            matBySignature[sig] = m;
            return m;
        }

        /// Ranuras de material: si un renderer tiene MÁS materiales que submeshes, Unity vuelve a dibujar la última
        /// submesh con cada material extra (materiales superpuestos + z-fighting). Se recortan; las vacías se rellenan.
        /// Solo se usa en instancias DRUMIX dentro de la escena (nunca toca prefabs/escenas del equipo).
        static int FixMaterialSlots(GameObject go, Material fallback)
        {
            int fixedCount = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Mesh mesh = null;
                var mf = r.GetComponent<MeshFilter>();
                if (mf) mesh = mf.sharedMesh;
                var smr = r as SkinnedMeshRenderer;
                if (smr) mesh = smr.sharedMesh;
                var mats = r.sharedMaterials;
                int subs = mesh ? mesh.subMeshCount : 0;
                int extra = DrumixLayout.ExtraMaterialSlots(mats.Length, subs);
                bool changed = false;
                if (extra > 0) { Array.Resize(ref mats, subs); changed = true; }
                for (int i = 0; i < mats.Length; i++) if (!mats[i]) { mats[i] = fallback; changed = true; }
                if (changed) { r.sharedMaterials = mats; fixedCount++; }
            }
            return fixedCount;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Child(string name, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos; return go;
        }

        /// Instancia (como prefab si se puede), resetea pose y escala para que su lado mayor mida 'size'.
        /// mode: 0 = horizontal (max x/z), 1 = alto (y), 2 = largo (máximo de los tres).
        static GameObject Place(GameObject asset, Transform parent, float size, int mode, bool feetOnFloor)
        {
            // Envoltorio "Escala": la escala/centrado va aquí, así un Animator del modelo no la pisa.
            var wrap = new GameObject("Escala (" + asset.name + ")").transform;
            wrap.SetParent(parent, false);
            var go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (!go) go = UnityEngine.Object.Instantiate(asset);
            go.transform.SetParent(wrap, false);
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
            StripCamerasAndLights(go);
            Bounds b = LocalBounds(go, parent);
            if (size <= 0f) size = mode == 0 ? Mathf.Max(b.size.x, b.size.z) : mode == 1 ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)); // tamaño real
            float measure = mode == 0 ? Mathf.Max(b.size.x, b.size.z) : mode == 1 ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (measure < 0.0001f || float.IsNaN(measure) || float.IsInfinity(measure)) { UnityEngine.Object.DestroyImmediate(wrap.gameObject); return null; }
            wrap.localScale = Vector3.one * (size / measure);
            b = LocalBounds(go, parent);
            Vector3 shift = -b.center;
            if (feetOnFloor) shift.y = -b.min.y;
            wrap.localPosition += shift;
            DrumixUtil.ForceVisible(go);
            return go;
        }

        /// Muchos FBX traen Camera/Light importadas (p. ej. palitotambor.fbx). Dentro de DRUMIX serían una 2a cámara
        /// en la mano y una luz extra: se apagan (el objeto si solo tiene eso; si no, el componente).
        static int StripCamerasAndLights(GameObject go)
        {
            int n = 0;
            var comps = new List<Behaviour>();
            comps.AddRange(go.GetComponentsInChildren<Camera>(true));
            comps.AddRange(go.GetComponentsInChildren<Light>(true));
            foreach (var c in comps)
            {
                if (!c) continue;
                bool onlyThis = c.gameObject != go && c.transform.childCount == 0 && c.GetComponents<Component>().Length <= 3
                                && !c.GetComponent<Renderer>();
                if (onlyThis) c.gameObject.SetActive(false); else c.enabled = false;
                n++;
            }
            return n;
        }

        /// Medida por VÉRTICES reales (BakeMesh en mallas con huesos): no depende de la escala de importación del FBX.
        static Bounds LocalBounds(GameObject go, Transform space)
        {
            return DrumixUtil.MeasureBounds(go, space);
        }

        /// Arregla materiales rosados/vacíos (shader no URP) y, si hay .mtl incluido, usa sus colores.
        static void FixMaterials(GameObject go, Material fallback, Dictionary<string, Color> mtl = null)
        {
            slotFixes += FixMaterialSlots(go, fallback);
            bool urp = lit && lit.name.StartsWith("Universal", StringComparison.Ordinal);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    string key = m ? m.name.Replace(" (Instance)", "") : null;
                    Color c;
                    if (key != null && mtl != null && mtl.TryGetValue(key, out c))
                    {
                        bool glow = key.IndexOf("neon", StringComparison.OrdinalIgnoreCase) >= 0;
                        mats[i] = Mat("Auto_" + Safe(key), c, glow ? 3f : 0f, null, glow, glow ? 0.5f : 0.35f);
                        changed = true; continue;
                    }
                    bool broken = !m || !m.shader || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported ||
                                  (urp && (m.shader.name == "Standard" || m.shader.name == "Standard (Specular setup)" || m.shader.name.StartsWith("Legacy Shaders", StringComparison.Ordinal)));
                    if (!broken) continue;
                    if (m && m.HasProperty("_Color") && !m.mainTexture)
                        mats[i] = Mat("Auto_" + Safe(key), m.GetColor("_Color")); // conserva el color original
                    else if (m && m.mainTexture)
                        mats[i] = Mat("Auto_" + Safe(key), Color.white, 0, m.mainTexture);
                    else mats[i] = fallback;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static string Safe(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name ?? "x") sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            return sb.Length > 0 ? sb.ToString() : "x";
        }

        /// Lee el .mtl hermano de un OBJ incluido: nombre -> color (Kd lineal -> gamma).
        static Dictionary<string, Color> BundledMtl(string relativeObj)
        {
            if (string.IsNullOrEmpty(relativeObj) || !relativeObj.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)) return null;
            string path = DrumixAssetFinder.Bundled + "/" + relativeObj.Substring(0, relativeObj.Length - 4) + ".mtl";
            if (!File.Exists(path)) return null;
            var dict = new Dictionary<string, Color>();
            string current = null;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith("newmtl ", StringComparison.Ordinal)) current = line.Substring(7).Trim();
                else if (current != null && line.StartsWith("Kd ", StringComparison.Ordinal))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    float r, g, b;
                    if (parts.Length >= 4 &&
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r) &&
                        float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g) &&
                        float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b))
                        dict[current] = new Color(r, g, b).gamma;
                }
            }
            return dict;
        }

        // ---------------------------------------------------------------- escenario
        static void BuildEnvironment(Transform root)
        {
            var env = Child("Bar (escenario)", root, Vector3.zero).transform;
            teamStage = false; floorY = 0f; kitSurfaceY = TarimaTop;
            var floor = Prim(PrimitiveType.Plane, "Piso", env, Vector3.zero, new Vector3(2.4f, 1, 2.4f), Mat("Piso", new Color(0.16f, 0.1f, 0.08f), 0, null, false, 0.25f), true);
            floor.GetComponent<Collider>().enabled = true;
            // Tarima: base 1 mm sobre el piso y tapa a 4 cm (nada coplanar con el piso).
            var tarima = Prim(PrimitiveType.Cube, "Tarima", env, new Vector3(0, (TarimaTop + 0.001f) / 2f, 0.35f), new Vector3(3.2f, TarimaTop - 0.001f, 2.4f), Mat("Tarima", new Color(0.22f, 0.08f, 0.32f)));
            if (ProjectStage(env))
            {
                if (teamStage)
                {
                    // La tarima del escenario del equipo queda EXACTAMENTE en y = 0 (donde pisa el jugador): no hace falta la
                    // tarima DRUMIX (sería otra superficie casi a la misma altura = z-fighting) y el piso baja 2 cm para que
                    // solo se vea fuera del escenario.
                    UnityEngine.Object.DestroyImmediate(tarima);
                    floorY = -0.02f; kitSurfaceY = 0f;
                    report.AppendLine("  Sin tarima DRUMIX ni bar incluido (el escenario del equipo ya es la tarima; piso DRUMIX 2 cm abajo).");
                }
                else floorY = -0.01f;
                floor.transform.localPosition = new Vector3(0, floorY, 0);
                return;
            }
            BuildBarRoom(env);
        }

        /// Paredes, neones y muebles del bar (coordenadas locales: jugador en el origen mirando a +Z). Lo usan DRUMIX_Bar y 2.NivelDos.
        static void BuildBarRoom(Transform env)
        {
            var wall = Mat("Pared", new Color(0.1f, 0.06f, 0.14f), 0, null, false, 0.1f);
            Prim(PrimitiveType.Cube, "Pared fondo", env, new Vector3(0, 2.5f, 9.5f), new Vector3(22, 5, 0.2f), wall);
            Prim(PrimitiveType.Cube, "Pared izq", env, new Vector3(-9, 2.5f, 3), new Vector3(0.2f, 5, 15), wall);
            Prim(PrimitiveType.Cube, "Pared der", env, new Vector3(9, 2.5f, 3), new Vector3(0.2f, 5, 15), wall);
            Prim(PrimitiveType.Cube, "Pared atras", env, new Vector3(0, 2.5f, -4.5f), new Vector3(22, 5, 0.2f), wall);
            var neonA = Mat("Neon_Violeta", new Color(0.75f, 0.3f, 1f), 3f, null, true);
            var neonB = Mat("Neon_Cian", new Color(0.3f, 0.9f, 1f), 3f, null, true);
            Prim(PrimitiveType.Cube, "Neon 1", env, new Vector3(0, 3.6f, 9.35f), new Vector3(10, 0.08f, 0.05f), neonA);
            Prim(PrimitiveType.Cube, "Neon 2", env, new Vector3(0, 3.35f, 9.35f), new Vector3(7, 0.06f, 0.05f), neonB);
            Prim(PrimitiveType.Cube, "Neon izq", env, new Vector3(-8.85f, 2.2f, 4), new Vector3(0.05f, 0.06f, 8), neonB);
            Prim(PrimitiveType.Cube, "Neon der", env, new Vector3(8.85f, 2.2f, 4), new Vector3(0.05f, 0.06f, 8), neonA);

            // Bar y escenario: primero modelos del proyecto, si no los incluidos (3DAssets.dev CC0), si no nada.
            string[] counter = { "bar counter", "barcounter", "barra", "mostrador" };
            for (int i = 0; i < 4; i++) Furniture(env, counter, "Bar/BarCounter.obj", new Vector3(-1.8f + i * 1.2f, 0, 8.3f), 180, 1.05f);
            Furniture(env, new[] { "back bar", "backbar", "liquor shelf", "estante" }, "Bar/BackBarSpirits.obj", new Vector3(-0.6f, 0, 9.15f), 180, 2.2f);
            Furniture(env, new[] { "glass shelf", "glassshelf" }, "Bar/BackBarGlasses.obj", new Vector3(0.6f, 0, 9.15f), 180, 2.2f);
            Furniture(env, new[] { "lightbox", "bar sign", "letrero" }, "Bar/BarLightbox.obj", new Vector3(0, 2.6f, 9.3f), 180, 0.6f, false);
            string[] stool = { "barstool", "bar stool", "stool", "taburete" };
            for (int i = 0; i < 4; i++) Furniture(env, stool, "Bar/BarStool.obj", new Vector3(-1.8f + i * 1.2f, 0, 7.55f), 0, 0.78f);
            string[] table = { "high table", "hightable", "bar table", "bartable", "mesa alta" };
            Furniture(env, table, "Bar/HighTable.obj", new Vector3(-6.6f, 0, 5.2f), 90, 1.05f);
            Furniture(env, table, "Bar/HighTable.obj", new Vector3(6.6f, 0, 5.2f), -90, 1.05f);
            string[] round = { "pub table", "round table", "tableround", "mesa" };
            Furniture(env, round, "Bar/PubTableRound.obj", new Vector3(-5.6f, 0, 1.6f), 0, 0.75f);
            Furniture(env, round, "Bar/PubTableRound.obj", new Vector3(5.6f, 0, 1.6f), 0, 0.75f);
            foreach (var x in new[] { -6.2f, -5.0f, 5.0f, 6.2f }) Furniture(env, new[] { "stool back", "bar chair", "silla" }, "Bar/BarStoolBack.obj", new Vector3(x, 0, 1.6f), x < 0 ? 90 : -90, 1.13f);
            string[] pa = { "pa speaker", "paspeaker", "speaker", "parlante", "altavoz" };
            Furniture(env, pa, "Bar/PASpeaker.obj", new Vector3(-1.9f, 0, 1.4f), 0, 1.81f);
            Furniture(env, pa, "Bar/PASpeaker.obj", new Vector3(1.9f, 0, 1.4f), 0, 1.81f);
            string[] monitor = { "stage monitor", "monitor wedge", "wedge" };
            Furniture(env, monitor, "Bar/StageMonitor.obj", new Vector3(-1.05f, TarimaTop, 1.3f), 180, 0.5f);
            Furniture(env, monitor, "Bar/StageMonitor.obj", new Vector3(1.05f, TarimaTop, 1.3f), 180, 0.5f);
            Furniture(env, new[] { "guitar amp", "amplifier", "amplificador", "amp" }, "Bar/GuitarAmp.obj", new Vector3(-1.45f, TarimaTop, -0.3f), 20, 1.2f);
            Furniture(env, new[] { "mic stand", "microphone", "microfono" }, "Bar/MicBoom.obj", new Vector3(1.3f, TarimaTop, 1.1f), 200, 1.84f);
            Furniture(env, new[] { "lighting bar", "light bar", "truss" }, "Bar/LightingBar.obj", new Vector3(0, 3.1f, 1.2f), 180, 0.54f, false);
            Furniture(env, new[] { "lighting bar", "light bar", "truss" }, "Bar/LightingBar.obj", new Vector3(0, 3.1f, 4.2f), 0, 0.54f, false);
            foreach (var x in new[] { -6f, -3f, 3f, 6f }) Furniture(env, new[] { "neon strip", "neonstrip", "neon" }, "Bar/NeonStrip.obj", new Vector3(x, 3.0f, 9.35f), 180, 0.14f, false);
        }

        /// 'ESCENARIO 3D' / '3d' del equipo: si hay un modelo de escenario/bar, se usa EN VEZ del bar incluido.
        static bool ProjectStage(Transform env)
        {
            if (!UseProjectStage) { report.AppendLine("Escenario del proyecto: desactivado en Opciones, uso el bar incluido."); return false; }
            var asset = DrumixAssetFinder.FindModel(DrumixKeywords.Stage, DrumixKeywords.StageExclude);
            if (!asset) return false;
            string stagePath = AssetDatabase.GetAssetPath(asset);
            if (DrumixTeamKitData.IsTeamStage(stagePath))
            {
                var r = DrumixTeamKitData.StageRootPosition;
                var teamHolder = Child("Escenario del EQUIPO (" + asset.name + ")", env, new Vector3(r[0], r[1], r[2]));
                teamStage = true;
                teamHolder.transform.localRotation = Quaternion.Euler(0, DrumixTeamKitData.StageYaw, 0);
                var stage = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                if (!stage) stage = UnityEngine.Object.Instantiate(asset);
                stage.transform.SetParent(teamHolder.transform, false);
                stage.transform.localPosition = Vector3.zero; stage.transform.localRotation = Quaternion.identity; stage.transform.localScale = Vector3.one;
                StripCamerasAndLights(stage);
                DrumixUtil.NeutralizeGrab(stage, true);
                foreach (var c in stage.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                foreach (var rb in stage.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
                FixMaterials(stage, Mat("Mueble", new Color(0.35f, 0.22f, 0.15f)));
                DrumixUtil.ForceVisible(stage);
                report.AppendLine("Escenario del EQUIPO: " + stagePath + " (tamaño real, girado 180° como en sus niveles, su tarima justo en y = 0)." +
                    " ¿No cuadra? DRUMIX > Opciones > Usar escenario 3D del proyecto (desmarcar) y ARMAR TODO.");
                return true;
            }
            var holder = Child("Escenario del PROYECTO", env, new Vector3(0, 0, 2.5f));
            Bounds b = DrumixAssetFinder.MeasurePrefab(asset);
            float max = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            bool native = max >= 2f && max <= 300f;
            var go = Place(asset, holder.transform, native ? 0f : 20f, 2, true);
            if (!go) { UnityEngine.Object.DestroyImmediate(holder); return false; }
            DrumixUtil.NeutralizeGrab(go, true);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
            FixMaterials(go, Mat("Mueble", new Color(0.35f, 0.22f, 0.15f)));
            report.AppendLine("Escenario del PROYECTO: " + AssetDatabase.GetAssetPath(asset) + (native ? " (tamaño original)" : " (escalado a 20 m)") +
                ". ¿No cuadra? DRUMIX > Opciones > Usar escenario 3D del proyecto (desmarcar) y ARMAR TODO.");
            return true;
        }

        // ---------------------------------------------------------------- configuración editable (fuera del paquete)
        public const string ConfigDir = "Assets/DRUMIX_Config";
        static DrumixVisualConfig visual;

        static DrumixVisualConfig EnsureVisualConfig()
        {
            EnsureFolder(ConfigDir);
            string path = ConfigDir + "/Visual.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<DrumixVisualConfig>(path);
            if (!cfg && File.Exists(path)) report.AppendLine("⚠ " + path + " estaba roto (script perdido). Se rehízo con valores de fábrica.");
            if (!cfg) { cfg = ScriptableObject.CreateInstance<DrumixVisualConfig>(); AssetDatabase.CreateAsset(cfg, path); }
            else if (cfg.Upgrade()) { EditorUtility.SetDirty(cfg); report.AppendLine("Config visual actualizada a la versión " + DrumixVisualConfig.CurrentVersion + " (luz más clara, GOOD azul)."); }
            return cfg;
        }

        /// Crea los 3 niveles SOLO si no existen (lo que Andrés pula en el Inspector nunca se pisa).
        static DrumixLevel[] EnsureLevels()
        {
            EnsureFolder(ConfigDir + "/Niveles");
            var defaults = DrumixLevelData.Defaults();
            var list = new List<DrumixLevel>();
            for (int i = 0; i < defaults.Length; i++)
            {
                string path = ConfigDir + "/Niveles/Nivel" + (i + 1) + "_" + Safe(defaults[i].name.Replace(" ", "")) + ".asset";
                var lvl = AssetDatabase.LoadAssetAtPath<DrumixLevel>(path);
                if (!lvl && File.Exists(path)) report.AppendLine("⚠ " + path + " estaba roto (script perdido: se borró Assets/DRUMIX sin sus .meta). Se rehízo con valores de fábrica.");
                if (!lvl)
                {
                    lvl = ScriptableObject.CreateInstance<DrumixLevel>();
                    lvl.spec = defaults[i];
                    AssetDatabase.CreateAsset(lvl, path);
                }
                list.Add(lvl);
            }
            // niveles extra que Andrés haya creado en la carpeta (Create > DRUMIX > Nivel) van después, en orden de nombre
            foreach (var guid in AssetDatabase.FindAssets("t:DrumixLevel", new[] { ConfigDir + "/Niveles" }))
            {
                var extra = AssetDatabase.LoadAssetAtPath<DrumixLevel>(AssetDatabase.GUIDToAssetPath(guid));
                if (extra && !list.Contains(extra)) list.Add(extra);
            }
            return list.ToArray();
        }

        static void BuildFX(Transform root, GameObject kit)
        {
            var go = Child("FX (partículas)", root, Vector3.zero);
            var fx = go.AddComponent<DrumixFX>();
            fx.config = visual;
            fx.particleMaterial = ParticleMaterial();
            var point = Child("Punto de celebración", go.transform, kit ? kit.transform.position + new Vector3(0, 1.6f, 0.9f) : new Vector3(0, 1.6f, 1.2f));
            fx.celebrationPoint = point.transform;
            report.AppendLine("Partículas: " + (fx.particleMaterial ? "acierto, fallo, combo, lanzamiento y confeti (aditivas, pocas, sin colisiones)" : "sin shader de partículas URP: desactivadas"));
        }

        static Material ParticleMaterial()
        {
            Shader shader = null;
            foreach (var n in new[] { "Universal Render Pipeline/Particles/Unlit", "Universal Render Pipeline/Particles/Simple Lit", "Universal Render Pipeline/Unlit", "Sprites/Default", "Legacy Shaders/Particles/Additive" })
            { shader = Shader.Find(n); if (shader) break; }
            if (!shader) return null;
            EnsureFolder(Generated + "/Textures");
            string texPath = Generated + "/Textures/FX_Suave.png";
            if (!File.Exists(texPath))
            {
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)); a = a * a * (3f - 2f * a);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                // Una sola vez (al crear el archivo), fuera de cualquier callback de import, y solo si difiere.
                var imp = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (imp && (imp.textureShape != TextureImporterShape.Texture2D || !imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp || !imp.mipmapEnabled))
                {
                    imp.textureShape = TextureImporterShape.Texture2D; imp.textureType = TextureImporterType.Default;
                    imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.SaveAndReimport();
                }
            }
            var soft = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string path = Generated + "/Materials/FX_Particulas.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            var softTex = Tex2DOrNull(soft, "FX_Particulas");
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", softTex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", softTex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // transparente
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);       // aditivo
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.One);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// Volumen global con bloom suave + color (por reflexión: compila sin URP). En Quest lo apaga DrumixVisualRuntime
        /// salvo que Config visual > Post Processing Quest esté marcado.
        static void BuildPostFX(Transform root, Camera camera)
        {
            if (camera)
            {
                var vr = camera.GetComponent<DrumixVisualRuntime>();
                if (!vr) vr = camera.gameObject.AddComponent<DrumixVisualRuntime>();
                vr.config = visual;
                DrumixVisualRuntime.SetPostProcessing(camera, visual ? visual.postProcessingPC : true);
            }
            Type volumeT = DrumixUtil.FindType("UnityEngine.Rendering.Volume");
            Type profileT = DrumixUtil.FindType("UnityEngine.Rendering.VolumeProfile");
            Type bloomT = DrumixUtil.FindType("UnityEngine.Rendering.Universal.Bloom");
            Type colorT = DrumixUtil.FindType("UnityEngine.Rendering.Universal.ColorAdjustments");
            Type toneT = DrumixUtil.FindType("UnityEngine.Rendering.Universal.Tonemapping");
            if (volumeT == null || profileT == null || bloomT == null) { report.AppendLine("Post-procesado: URP no encontrado, se omite."); return; }
            try
            {
                string path = Generated + "/DRUMIX_PostFX.asset";
                var profile = AssetDatabase.LoadAssetAtPath(path, profileT);
                if (!profile) { profile = ScriptableObject.CreateInstance(profileT); AssetDatabase.CreateAsset(profile, path); }
                var bloom = GetOrAddComponent(profile, profileT, bloomT);
                Override(bloom, "intensity", visual ? visual.bloomIntensity : 0.45f);
                Override(bloom, "threshold", visual ? visual.bloomThreshold : 1f);
                Override(bloom, "scatter", 0.6f);
                Override(bloom, "highQualityFiltering", false); // barato para Quest
                if (colorT != null)
                {
                    var color = GetOrAddComponent(profile, profileT, colorT);
                    Override(color, "saturation", visual ? visual.saturation : 12f);
                    Override(color, "contrast", 8f);
                }
                if (toneT != null)
                {
                    var tone = GetOrAddComponent(profile, profileT, toneT);
                    Type modeT = DrumixUtil.FindType("UnityEngine.Rendering.Universal.TonemappingMode");
                    if (modeT != null) Override(tone, "mode", Enum.Parse(modeT, "Neutral"));
                }
                EditorUtility.SetDirty(profile);
                var go = Child("Post-procesado (global)", root, Vector3.zero);
                var vol = go.AddComponent(volumeT);
                volumeT.GetProperty("isGlobal").SetValue(vol, true, null);
                volumeT.GetProperty("sharedProfile").SetValue(vol, profile, null);
                report.AppendLine("Post-procesado: bloom suave + color (PC). Quest: " + (visual && visual.postProcessingQuest ? "activado" : "apagado (rendimiento)") + " → " + ConfigDir + "/Visual.asset");
            }
            catch (Exception e) { report.AppendLine("Post-procesado: se omite (" + e.GetType().Name + ": " + e.Message + ")"); }
        }

        static UnityEngine.Object GetOrAddComponent(UnityEngine.Object profile, Type profileT, Type compT)
        {
            var componentsField = profileT.GetField("components");
            var list = componentsField != null ? componentsField.GetValue(profile) as System.Collections.IList : null;
            if (list != null) foreach (var c in list) if (c != null && c.GetType() == compT) return (UnityEngine.Object)c;
            var add = profileT.GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            var comp = (UnityEngine.Object)add.Invoke(profile, new object[] { compT, true });
            comp.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(comp, profile);
            return comp;
        }

        static void Override(UnityEngine.Object component, string field, object value)
        {
            if (!component) return;
            var f = component.GetType().GetField(field);
            var param = f != null ? f.GetValue(component) : null;
            if (param == null) return;
            var m = param.GetType().GetMethod("Override");
            if (m == null) return;
            var arg = m.GetParameters()[0].ParameterType;
            try { m.Invoke(param, new[] { arg.IsEnum && !(value is Enum) ? Enum.ToObject(arg, value) : Convert.ChangeType(value, arg.IsEnum ? value.GetType() : arg) }); }
            catch (Exception) { }
            EditorUtility.SetDirty(component);
        }

        // ---------------------------------------------------------------- nada superpuesto
        /// Apoya la batería (su pieza más baja, la alfombra) 6 mm sobre la superficie: ni enterrada ni coplanar.
        static void SnapToSurface(GameObject go, float surface)
        {
            Bounds b;
            if (!go || !DrumixOverlap.TryBounds(go, out b)) return;
            float dy = surface - b.min.y;
            if (Mathf.Abs(dy) > 0.0005f) go.transform.position += new Vector3(0, dy, 0);
        }

        /// El origen del jugador pisa la misma superficie que la batería (tarima DRUMIX o tarima del equipo).
        static void LiftRig(Transform root, Camera camera, float surface)
        {
            if (!camera) return;
            Transform t = camera.transform;
            while (t.parent && t.parent != root) t = t.parent;
            if (t.parent != root) return;
            var p = t.localPosition; t.localPosition = new Vector3(p.x, p.y + surface, p.z);
        }

        static Vector3? playerOriginOverride;
        static Vector3 PlayerOrigin(GameObject root)
        {
            if (playerOriginOverride.HasValue) return playerOriginOverride.Value;
            foreach (Transform t in root.transform)
                if (t.name.StartsWith("XR Origin", StringComparison.Ordinal)) return t.position;
            var cam = root.GetComponentInChildren<Camera>();
            return cam ? new Vector3(cam.transform.position.x, kitSurfaceY, cam.transform.position.z) : new Vector3(0, kitSurfaceY, 0);
        }

        /// Después de armar: muebles y público se acomodan sin meterse en la batería, el jugador, la UI, el escenario
        /// ni entre ellos. Se empujan de a 10 cm (hasta 1.2 m); si no hay lugar, se omiten y se reporta.
        static int crowdKept;
        static void ResolveLayout(GameObject root)
        {
            crowdKept = 0;
            // Copias exactas de la misma malla en el mismo lugar (pasa en algunos FBX): z-fighting seguro.
            // Se vacía la malla de la copia en NUESTRA instancia (el prefab/escena del equipo no se toca); así ni ForceVisible
            // ni el runtime la vuelven a dibujar.
            foreach (var d in DrumixOverlap.FindDuplicateRenderers(root))
            {
                var mf = d.Value.GetComponent<MeshFilter>();
                if (mf) { mf.sharedMesh = null; duplicatesHidden++; }
            }

            var items = DrumixOverlap.Collect(root, PlayerOrigin(root));
            var fixedBoxes = items.Where(i => i.kind == DrumixOverlap.Kind.Kit || i.kind == DrumixOverlap.Kind.Player ||
                                              i.kind == DrumixOverlap.Kind.Ui || i.kind == DrumixOverlap.Kind.Stage).Select(i => i.box).ToList();
            var tarima = root.transform.Find(DrumixOverlap.EnvName + "/Tarima");
            Box3? tarimaBox = null; Bounds tb;
            if (tarima && DrumixOverlap.TryBounds(tarima.gameObject, out tb)) tarimaBox = DrumixOverlap.ToBox(tb);
            int moved = 0, removed = 0;
            var removedNames = new List<string>();
            var placed = new List<Box3>(fixedBoxes);

            // 1) Muebles
            foreach (var f in items.Where(i => i.kind == DrumixOverlap.Kind.Furniture))
            {
                var obstacles = new List<Box3>(placed);
                // Un mueble apoyado en el piso no puede atravesar la tarima (los que van encima ya están a su altura).
                if (tarimaBox.HasValue && f.box.y0 < TarimaTop - 0.01f) obstacles.Add(tarimaBox.Value);
                float dx, dz;
                if (DrumixLayout.FindFreeOffset(f.box, obstacles, 0f, 0.5f, 0.1f, 1.2f, out dx, out dz))
                {
                    if (dx != 0 || dz != 0) { f.go.transform.position += new Vector3(dx, 0, dz); moved++; }
                    placed.Add(f.box.Moved(dx, 0, dz));
                }
                else { removedNames.Add(f.name); UnityEngine.Object.DestroyImmediate(f.go); removed++; }
            }

            // 2) Público: lejos del escenario del equipo (fuera de su tarima), de la batería, muebles y de los demás NPCs.
            if (teamStage)
            {
                var r = DrumixTeamKitData.StagePlatformWorld();
                placed.Add(new Box3(r[0], -0.5f, r[2], r[1], 0.05f, r[3])); // tarima del equipo: el público queda abajo, al frente
            }
            if (tarimaBox.HasValue) placed.Add(tarimaBox.Value);
            foreach (var n in items.Where(i => i.kind == DrumixOverlap.Kind.Npc))
            {
                float dx, dz;
                if (DrumixLayout.FindFreeOffset(n.box, placed, 0f, DrumixLayout.CrowdCenterZ(teamStage), 0.1f, 1.2f, out dx, out dz))
                {
                    if (dx != 0 || dz != 0)
                    {
                        var t = n.go.transform; t.position += new Vector3(dx, 0, dz); moved++;
                        Vector3 look = -t.position; look.y = 0;
                        if (look.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                    }
                    var box = n.box.Moved(dx, 0, dz);
                    // separación mínima entre personas (no solo que las cajas no se toquen)
                    float pad = DrumixLayout.NpcPadding(box.SizeX);
                    placed.Add(new Box3(box.x0 - pad, box.y0, box.z0 - pad, box.x1 + pad, box.y1, box.z1 + pad));
                }
                else if (DrumixLayout.FindFreeOffset(n.box, placed, 0f, DrumixLayout.CrowdCenterZ(teamStage), 0.15f, 3f, out dx, out dz))
                {
                    // segundo intento más lejos (hasta 3 m)
                    var t = n.go.transform; t.position += new Vector3(dx, 0, dz); moved++;
                    Vector3 look = -t.position; look.y = 0;
                    if (look.sqrMagnitude > 0.01f) t.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                    placed.Add(n.box.Moved(dx, 0, dz));
                }
                else { crowdKept++; placed.Add(n.box); } // el público NUNCA se borra: se queda donde estaba y se avisa
            }
            if (crowdKept > 0) report.AppendLine("  ⚠ " + crowdKept + " persona(s) del público sin lugar libre: se dejaron en su sitio (no se borran).");

            var left = DrumixOverlap.FindOverlaps(DrumixOverlap.Collect(root, PlayerOrigin(root)));
            report.AppendLine("Acomodo sin superposiciones: " + moved + " objeto(s) corridos, " + removed + " omitido(s)" +
                (removed > 0 ? " (" + string.Join(", ", removedNames.Take(6).ToArray()) + ")" : "") +
                (left.Count == 0 ? ", 0 choques." : ", " + left.Count + " choque(s) quedan: " + left[0].Key + " / " + left[0].Value));
            report.AppendLine("Materiales: " + matBySignature.Count + " generados únicos (" + materialsShared + " reusos), " + slotFixes + " renderer(s) con ranuras sobrantes/vacías corregidos, " + duplicatesHidden + " malla(s) duplicada(s) quitadas.");
        }

        /// En las escenas del equipo el bar usa SIEMPRE los muebles incluidos (si no, 'stool'/'table' agarraban los del garaje).
        static bool preferBundledFurniture;
        static readonly HashSet<string> loggedFurniture = new HashSet<string>();
        /// Proyecto (escalado a 'height') -> incluido (tamaño real, ya en metros) -> nada.
        static void Furniture(Transform parent, string[] keywords, string bundled, Vector3 pos, float yaw, float height, bool onFloor = true)
        {
            var asset = preferBundledFurniture ? null : DrumixAssetFinder.FindModel(keywords, new[] { "drum", "stick", "character", "npc" });
            bool fromProject = asset != null;
            string origin = fromProject ? "PROYECTO " + AssetDatabase.GetAssetPath(asset) : "INCLUIDO";
            if (!asset) asset = DrumixAssetFinder.LoadBundled<GameObject>(bundled);
            if (!asset) return;
            var holder = Child(Path.GetFileNameWithoutExtension(bundled), parent, pos);
            holder.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var go = Place(asset, holder.transform, fromProject ? height : 0f, 1, onFloor);
            if (!go) { UnityEngine.Object.DestroyImmediate(holder); return; }
            FixMaterials(go, Mat("Mueble", new Color(0.35f, 0.22f, 0.15f)), fromProject ? null : BundledMtl(bundled));
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            DrumixUtil.NeutralizeGrab(go, true);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
            string key = bundled + origin;
            if (loggedFurniture.Add(key)) report.AppendLine("Escenario " + Path.GetFileNameWithoutExtension(bundled) + ": " + origin);
        }

        static Light[] BuildLights(Transform root)
        {
            var lights = Child("Luces", root, Vector3.zero).transform;
            var sun = Child("Luz ambiente", lights, new Vector3(0, 5, 0)).AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = visual ? visual.directionalIntensity : 1.2f;
            sun.color = visual ? visual.directionalColor : new Color(1f, 0.93f, 0.85f);
            sun.transform.localRotation = Quaternion.Euler(55, -25, 0); sun.shadows = LightShadows.None;
            var list = new List<Light>();
            list.Add(Point(lights, "Luz violeta", new Vector3(-2.8f, 3.2f, 3f), new Color(0.7f, 0.3f, 1f), 3f, 9f));
            list.Add(Point(lights, "Luz cian", new Vector3(2.8f, 3.2f, 3f), new Color(0.3f, 0.6f, 1f), 3f, 9f));
            list.Add(Point(lights, "Luz bateria", new Vector3(0, 2.4f, 0.2f), new Color(1f, 0.9f, 0.75f), 2.5f, 4f));
            list.Add(Point(lights, "Luz publico", new Vector3(0, 3.2f, 5.5f), new Color(1f, 0.6f, 0.8f), 3f, 9f));
            float k = visual ? visual.stageLightMultiplier : 1f;
            foreach (var l in list) { l.intensity *= k; l.shadows = LightShadows.None; }
            // Quest: 1 direccional + 4 puntuales, todas sin sombras (dentro del límite de luces por objeto de URP).
            return list.ToArray();
        }
        static Light Point(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var l = Child(name, parent, pos).AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            return l;
        }

        static void ConfigureRender(Camera camera)
        {
            var v = visual;
            RenderSettings.ambientMode = AmbientMode.Trilight; // degradado morado: barato y da volumen
            RenderSettings.ambientSkyColor = v ? v.ambientSky : new Color(0.30f, 0.20f, 0.45f);
            RenderSettings.ambientEquatorColor = v ? v.ambientEquator : new Color(0.18f, 0.10f, 0.26f);
            RenderSettings.ambientGroundColor = v ? v.ambientGround : new Color(0.06f, 0.04f, 0.08f);
            RenderSettings.skybox = null; // fondo de color sólido: lo más barato en Quest
            RenderSettings.fog = v ? v.fog : true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = v ? v.fogColor : new Color(0.07f, 0.04f, 0.11f);
            RenderSettings.fogDensity = v ? v.fogDensity : 0.025f;
            if (camera)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = v ? v.background : new Color(0.035f, 0.02f, 0.06f);
                camera.cullingMask = ~0; // dibuja TODAS las capas: nada del público/batería se "desaparece"
                camera.nearClipPlane = 0.02f; camera.farClipPlane = 60f;
            }
        }

        // ---------------------------------------------------------------- batería
        static GameObject BuildKit(Transform root)
        {
            var kit = Child("Kit (bateria fija)", root, new Vector3(0, 0.05f, 0));
            var shell = Mat("Bateria_Casco", new Color(0.42f, 0.12f, 0.55f), 0, null, false, 0.75f, 0.2f);
            var head = Mat("Bateria_Parche", new Color(0.93f, 0.92f, 0.88f), 0, null, false, 0.3f);
            var brass = Mat("Bateria_Platillo", new Color(0.85f, 0.65f, 0.25f), 0, null, false, 0.85f, 0.9f);
            var chrome = Mat("Bateria_Cromo", new Color(0.75f, 0.75f, 0.8f), 0, null, false, 0.9f, 1f);
            var ring = Mat("Bateria_Aro_Cue", new Color(0.75f, 0.35f, 1f), 2f, null, true);

            // 0) Batería DEL EQUIPO (Drum Set_sketchfab) con las zonas que el equipo calibró en 1.NivelUno.
            if (BuildTeamKit(kit.transform, ring, shell))
            {
                foreach (var body in kit.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                kit.AddComponent<DrumixKitLock>();
                DrumixUtil.ForceVisible(kit);
                return kit;
            }

            // 1) ¿El proyecto tiene una batería completa con piezas con nombre?
            var used = new HashSet<Transform>();
            var found = new Dictionary<DrumixLane, Transform>();
            GameObject wholeKitAsset = DrumixAssetFinder.FindModel(KitKeywords, KitExclude);
            GameObject wholeKit = null;
            if (wholeKitAsset)
            {
                var holder = Child("Bateria del proyecto", kit.transform, new Vector3(0, 0, 0.55f));
                wholeKit = Place(wholeKitAsset, holder.transform, 1.6f, 0, true);
                if (wholeKit)
                {
                    foreach (var spec in Pieces)
                    {
                        var t = DrumixAssetFinder.FindChild(wholeKit.transform, spec.keywords, spec.exclude, used);
                        if (t) { used.Add(t); found[spec.lane] = t; }
                    }
                }
                if (found.Count < 2)
                {
                    report.AppendLine("Batería completa '" + wholeKitAsset.name + "' no tiene piezas con nombre (" + found.Count + "): se usan piezas sueltas.");
                    UnityEngine.Object.DestroyImmediate(holder); wholeKit = null; found.Clear();
                }
                else
                {
                    DrumixUtil.NeutralizeGrab(wholeKit, true);
                    report.AppendLine("Batería completa del PROYECTO: " + AssetDatabase.GetAssetPath(wholeKitAsset) + " (" + found.Count + "/8 piezas por nombre)");
                }
            }

            bool anyProjectPiece = found.Count > 0;
            if (!anyProjectPiece) foreach (var sp in Pieces) if (DrumixAssetFinder.FindModel(sp.keywords, sp.exclude)) { anyProjectPiece = true; break; }
            if (!anyProjectPiece && BuildBundledKit(kit.transform, ring))
            {
                foreach (var body in kit.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
                kit.AddComponent<DrumixKitLock>();
                DrumixUtil.ForceVisible(kit);
                return kit;
            }

            foreach (var spec in Pieces)
            {
                Transform piece;
                string origin;
                if (found.TryGetValue(spec.lane, out piece))
                {
                    origin = "PROYECTO (dentro de la batería) " + piece.name;
                    var teamPad = FindTeamPad(piece.gameObject);
                    var host = teamPad ? teamPad : piece.gameObject;
                    SetupPad(host, spec, ring, null, true);
                }
                else
                {
                    var holder = Child("Pieza_" + spec.lane, kit.transform, spec.pos);
                    holder.transform.localRotation = Quaternion.Euler(spec.euler);
                    var asset = DrumixAssetFinder.FindModel(spec.keywords, spec.exclude);
                    if (asset && spec.lane == DrumixLane.Kick) holder.transform.localRotation = Quaternion.identity; // un bombo real ya viene "de pie"
                    GameObject visual = asset ? Place(asset, holder.transform, spec.size, 0, false) : null;
                    if (visual)
                    {
                        origin = "PROYECTO " + AssetDatabase.GetAssetPath(asset);
                        DrumixUtil.NeutralizeGrab(visual, true);
                        FixMaterials(visual, spec.cymbal ? brass : shell);
                        var teamPad = FindTeamPad(visual);
                        if (teamPad) { origin += " (con DrumPad del equipo: se conserva, DrumixPad sin doble sonido)"; SetupPad(teamPad, spec, ring, holder.transform, true); }
                        else SetupPad(holder, spec, ring, null, true);
                    }
                    else
                    {
                        origin = "PRIMITIVA";
                        PrimitivePiece(holder.transform, spec, shell, head, brass, chrome);
                        SetupPad(holder, spec, ring, null, false);
                    }
                }
                report.AppendLine("  " + DrumixEvents.LaneName(spec.lane) + " [" + DrumixEvents.LaneKey(spec.lane) + "]: " + origin);
            }

            // Bloqueo anti "bombo al centro": kinematic, sin grab, pose fija.
            foreach (var body in kit.GetComponentsInChildren<Rigidbody>(true)) { body.isKinematic = true; body.useGravity = false; }
            kit.AddComponent<DrumixKitLock>();
            DrumixUtil.ForceVisible(kit);
            return kit;
        }

        static readonly string[] KitKeywords = DrumixKeywords.Kit;
        static readonly string[] KitExclude = DrumixKeywords.KitExclude;

        /// Batería del equipo a tamaño real (sin re-escalar ni re-centrar) + zonas calibradas por el equipo.
        static bool BuildTeamKit(Transform kit, Material ringMat, Material shell)
        {
            var asset = DrumixAssetFinder.FindModel(KitKeywords, KitExclude);
            if (!asset || !DrumixTeamKitData.IsTeamKit(asset.name)) return false;
            var holder = Child("Bateria del equipo (" + asset.name + ")", kit, new Vector3(0, DrumixTeamKitData.RootHeight, DrumixTeamKitData.RootForward));
            var model = PrefabUtility.InstantiatePrefab(asset) as GameObject;
            if (!model) model = UnityEngine.Object.Instantiate(asset);
            model.transform.SetParent(holder.transform, false);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity; model.transform.localScale = Vector3.one;

            // ¿Es de verdad el mismo modelo/importación? (si alguien cambió la escala del FBX, no se usa la calibración)
            int ok = 0;
            foreach (var probe in DrumixTeamKitData.Signature)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != probe.node) continue;
                    Vector3 local = model.transform.InverseTransformPoint(t.position);
                    if ((local - new Vector3(probe.x, probe.y, probe.z)).magnitude <= DrumixTeamKitData.SignatureTolerance) { ok++; break; }
                }
            }
            if (ok < DrumixTeamKitData.Signature.Length - 1)
            {
                report.AppendLine("Batería del equipo '" + AssetDatabase.GetAssetPath(asset) + "': la importación no coincide con la calibración (" + ok + "/" + DrumixTeamKitData.Signature.Length + "), se usa otra batería.");
                UnityEngine.Object.DestroyImmediate(holder);
                return false;
            }

            StripCamerasAndLights(model);
            DrumixUtil.NeutralizeGrab(model, true);
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false; // solo las zonas golpean

            // Tamaño real de batería: el FBX del equipo mide ~2.7 m de ancho y la caja queda a ~1 m (demasiado grande).
            // Escala por medidas: caja a 0.65 m del piso, nunca más de 1.8 m de ancho. Las zonas son hijas: se recalculan solas.
            var snareZone = Array.Find(DrumixTeamKitData.Zones, z => z.lane == DrumixLane.Snare);
            float snareHeight = DrumixTeamKitData.RootHeight + snareZone.y;
            Bounds kb = LocalBounds(model, holder.transform);
            float kitScale = DrumixScaleRules.KitScale(snareHeight, kb.size.x);
            holder.transform.localScale = Vector3.one * kitScale;
            holder.transform.localPosition = new Vector3(0, DrumixTeamKitData.RootHeight * kitScale, DrumixTeamKitData.SnareAhead - snareZone.z * kitScale);
            report.AppendLine("Batería: escala " + kitScale.ToString("0.00") + " → ancho " + (kb.size.x * kitScale).ToString("0.00") + " m, caja a " + (snareHeight * kitScale).ToString("0.00") + " m del piso.");
            FixMaterials(model, shell);
            DrumixUtil.ForceVisible(model);

            foreach (var z in DrumixTeamKitData.Zones)
            {
                var spec = Array.Find(Pieces, p => p.lane == z.lane);
                var zone = Child("Zona_" + z.lane + " (" + z.teamName + ")", holder.transform, new Vector3(z.x, z.y, z.z));
                zone.transform.localRotation = new Quaternion(z.qx, z.qy, z.qz, z.qw).normalized;
                SetupPad(zone, spec, ringMat, null, false, new Bounds(Vector3.zero, new Vector3(z.sx, z.sy, z.sz)));
                var pad = zone.GetComponent<DrumixPad>();
                AudioClip teamClip = string.IsNullOrEmpty(z.clip) ? null : DrumixAssetFinder.FindClipExact(z.clip);
                if (teamClip) { pad.clip = teamClip; EditorUtility.SetDirty(pad); }
                else if (pad.clip && string.IsNullOrEmpty(z.clip) && !AssetDatabase.GetAssetPath(pad.clip).StartsWith(DrumixAssetFinder.Bundled, StringComparison.Ordinal))
                { pad.clip = DrumixAssetFinder.LoadBundled<AudioClip>(spec.bundledClip); EditorUtility.SetDirty(pad); } // bombo/hi-hat: sonido incluido, no un platillo del equipo
                report.AppendLine("  " + DrumixEvents.LaneName(z.lane) + " [" + DrumixEvents.LaneKey(z.lane) + "]: zona del equipo " + z.teamName +
                                  (teamClip ? " + sonido " + AssetDatabase.GetAssetPath(teamClip) : " + sonido incluido"));
            }
            report.AppendLine("Batería: la del EQUIPO " + AssetDatabase.GetAssetPath(asset) + " (tamaño real, zonas calibradas de 1.NivelUno, fija).");
            return true;
        }

        // Zonas de golpe medidas sobre la batería Grab3D incluida (coordenadas del OBJ, metros).
        // x del OBJ: el signo se detecta al importar (Unity espeja OBJ en X). z negativo = lado del baterista.
        struct Zone { public DrumixLane lane; public float x, y, z; public Vector3 euler; public float size, height; public bool cymbal; }
        static readonly Zone[] BundledZones =
        {
            new Zone { lane = DrumixLane.Kick,     x = -0.05f, y = 0.29f, z = 0.40f, euler = new Vector3(-90, 0, 0), size = 0.58f, height = 0.45f },
            new Zone { lane = DrumixLane.Snare,    x =  0.31f, y = 0.58f, z = -0.02f, euler = Vector3.zero,          size = 0.36f, height = 0.14f },
            new Zone { lane = DrumixLane.HiHat,    x =  0.71f, y = 0.88f, z = 0.09f, euler = Vector3.zero,           size = 0.36f, height = 0.06f, cymbal = true },
            new Zone { lane = DrumixLane.Tom1,     x =  0.10f, y = 0.765f, z = 0.42f, euler = new Vector3(-17, 0, 0), size = 0.25f, height = 0.19f },
            new Zone { lane = DrumixLane.Tom2,     x = -0.20f, y = 0.76f, z = 0.42f, euler = new Vector3(-15, 0, 0), size = 0.30f, height = 0.22f },
            new Zone { lane = DrumixLane.FloorTom, x = -0.63f, y = 0.40f, z = 0.16f, euler = Vector3.zero,           size = 0.41f, height = 0.35f },
            new Zone { lane = DrumixLane.Crash,    x =  0.48f, y = 1.20f, z = 0.47f, euler = Vector3.zero,           size = 0.45f, height = 0.16f, cymbal = true },
            new Zone { lane = DrumixLane.Ride,     x = -0.70f, y = 1.04f, z = 0.44f, euler = Vector3.zero,           size = 0.50f, height = 0.18f, cymbal = true },
        };
        const float BundledKitForward = 0.42f; // la caja queda ~40 cm delante del jugador

        static bool BuildBundledKit(Transform kit, Material ringMat)
        {
            const string rel = "DrumKit/drum-kit.obj";
            var asset = DrumixAssetFinder.LoadBundled<GameObject>(rel);
            if (!asset) return false;
            var holder = Child("Bateria Grab3D (CC0)", kit, new Vector3(0, 0, BundledKitForward));
            var model = Place(asset, holder.transform, 0f, 0, true); // tamaño real (metros)
            if (!model) { UnityEngine.Object.DestroyImmediate(holder); return false; }
            var tex = DrumixAssetFinder.LoadBundled<Texture2D>("DrumKit/drum-kit_body_basecolor.jpg");
            var mat = Mat("Bateria_Grab3D", Color.white, 0, tex, false, 0.55f, 0.1f);
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            { var arr = r.sharedMaterials; for (int i = 0; i < arr.Length; i++) arr[i] = mat; r.sharedMaterials = arr; }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) c.enabled = false;

            // ¿Unity espejó X? Contar vértices cerca de la caja en +x y en -x.
            float sign = DetectMirror(model, holder.transform, new Vector3(0.31f, 0.65f, -0.02f));
            foreach (var z in BundledZones)
            {
                var spec = Array.Find(Pieces, p => p.lane == z.lane);
                spec.size = z.size; spec.height = z.height; spec.cymbal = z.cymbal;
                var zone = Child("Zona_" + z.lane, holder.transform, new Vector3(z.x * sign, z.y, z.z));
                zone.transform.localRotation = Quaternion.Euler(z.euler);
                SetupPad(zone, spec, ringMat, null, false);
                report.AppendLine("  " + DrumixEvents.LaneName(z.lane) + " [" + DrumixEvents.LaneKey(z.lane) + "]: INCLUIDO (zona sobre batería Grab3D)");
            }
            report.AppendLine("Batería: INCLUIDA Grab3D CC0 (espejo X " + (sign < 0 ? "sí" : "no") + "). Si tocas de pie: Kit > DrumixKitLock > heightOffset 0.25");
            return true;
        }

        /// +1 si la caja (x>0 en el OBJ) quedó en +x, -1 si Unity espejó el modelo.
        static float DetectMirror(GameObject model, Transform space, Vector3 probe)
        {
            int plus = 0, minus = 0;
            Vector3 mirrored = new Vector3(-probe.x, probe.y, probe.z);
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh; if (!mesh) continue;
                Vector3[] verts;
                try { verts = mesh.vertices; } catch (Exception) { continue; }
                if (verts == null) continue;
                for (int i = 0; i < verts.Length; i += 3)
                {
                    Vector3 p = space.InverseTransformPoint(mf.transform.TransformPoint(verts[i]));
                    if ((p - probe).sqrMagnitude < 0.04f) plus++;
                    else if ((p - mirrored).sqrMagnitude < 0.04f) minus++;
                }
            }
            if (plus == 0 && minus == 0) return -1f; // sin datos: Unity espeja los OBJ en X
            return minus > plus ? -1f : 1f;
        }

        static GameObject FindTeamPad(GameObject go)
        {
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (b && b.GetType().Name == "DrumPad" && string.IsNullOrEmpty(b.GetType().Namespace)) return b.gameObject;
            return null;
        }

        static void PrimitivePiece(Transform holder, PieceSpec s, Material shell, Material head, Material brass, Material chrome)
        {
            if (s.cymbal)
            {
                Prim(PrimitiveType.Cylinder, "Platillo", holder, Vector3.zero, new Vector3(s.size, 0.004f, s.size), brass);
                Prim(PrimitiveType.Sphere, "Campana", holder, new Vector3(0, 0.008f, 0), new Vector3(0.07f, 0.025f, 0.07f), brass);
                if (s.lane == DrumixLane.HiHat) Prim(PrimitiveType.Cylinder, "Platillo abajo", holder, new Vector3(0, -0.02f, 0), new Vector3(s.size, 0.004f, s.size), brass);
            }
            else
            {
                Prim(PrimitiveType.Cylinder, "Casco", holder, Vector3.zero, new Vector3(s.size, s.height * 0.5f, s.size), shell);
                Prim(PrimitiveType.Cylinder, "Parche", holder, new Vector3(0, s.height * 0.5f + 0.002f, 0), new Vector3(s.size * 0.97f, 0.003f, s.size * 0.97f), head);
                Prim(PrimitiveType.Cylinder, "Aro", holder, new Vector3(0, s.height * 0.5f - 0.004f, 0), new Vector3(s.size * 1.02f, 0.008f, s.size * 1.02f), chrome);
            }
            // Soporte hasta el piso (sin collider, fuera del pad lógico).
            float worldY = holder.position.y;
            if (s.lane != DrumixLane.Kick && worldY > 0.2f)
            {
                var stand = Prim(PrimitiveType.Cylinder, "Soporte", holder.parent, new Vector3(holder.localPosition.x, worldY * 0.5f, holder.localPosition.z + 0.02f),
                    new Vector3(0.025f, worldY * 0.5f, 0.025f), chrome);
                stand.name = "Soporte_" + s.lane;
            }
        }

        static void SetupPad(GameObject host, PieceSpec spec, Material ringMat, Transform poseRoot, bool fromModel, Bounds? forced = null)
        {
            var pad = host.GetComponent<DrumixPad>();
            if (!pad) pad = host.AddComponent<DrumixPad>();
            pad.lane = spec.lane;
            pad.muteIfTeamDrumPad = true;

            // Audio: proyecto -> incluido -> procedural (en runtime).
            var projectClip = DrumixAssetFinder.FindClip(spec.keywords, new[] { "loop", "music", "song", "musica" });
            pad.clip = projectClip ? projectClip : DrumixAssetFinder.LoadBundled<AudioClip>(spec.bundledClip);
            if (projectClip && !forced.HasValue) report.AppendLine("    sonido PROYECTO: " + AssetDatabase.GetAssetPath(projectClip));

            // Collider trigger que cubre la pieza (solo la parte golpeable).
            Bounds local;
            if (forced.HasValue) local = forced.Value;
            else if (fromModel) local = LocalBounds(host, host.transform);
            else local = spec.cymbal ? new Bounds(Vector3.zero, new Vector3(spec.size, 0.07f, spec.size))
                                     : new Bounds(new Vector3(0, 0.02f, 0), new Vector3(spec.size, spec.height + 0.06f, spec.size));
            if (local.size.sqrMagnitude < 0.0001f) local = new Bounds(Vector3.zero, Vector3.one * spec.size);
            bool hasCollider = false;
            foreach (var c in host.GetComponents<Collider>())
            {
                var mesh = c as MeshCollider;
                if (mesh && !mesh.convex) { mesh.enabled = false; continue; } // trigger no-convexo = error de física
                c.isTrigger = true; c.enabled = true; hasCollider = true;
            }
            if (!hasCollider)
            {
                var box = host.AddComponent<BoxCollider>();
                box.isTrigger = true; box.center = local.center;
                box.size = new Vector3(Mathf.Max(local.size.x, 0.12f), Mathf.Max(local.size.y, 0.08f), Mathf.Max(local.size.z, 0.12f)) * 1.05f;
            }
            // Colliders sólidos del modelo debajo: desactivados (si no, empujan las baquetas o el bombo "salta").
            foreach (var c in host.GetComponentsInChildren<Collider>(true))
                if (c.gameObject != host && !c.isTrigger) c.enabled = false;

            var body = host.GetComponent<Rigidbody>();
            if (!body) body = host.AddComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false; body.constraints = RigidbodyConstraints.FreezeAll;

            // Aro que brilla con la nota (cue).
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Cue (brilla con la nota)";
            UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
            ring.transform.SetParent(host.transform, false);
            float d = Mathf.Max(local.size.x, local.size.z) * 1.08f;
            ring.transform.localPosition = new Vector3(local.center.x, local.max.y + 0.006f, local.center.z);
            ring.transform.localScale = new Vector3(d, 0.002f, d);
            var rr = ring.GetComponent<Renderer>(); rr.sharedMaterial = ringMat; rr.shadowCastingMode = ShadowCastingMode.Off;
            rr.enabled = false;
            pad.cueRenderer = rr;
            EditorUtility.SetDirty(pad);
        }

        // ---------------------------------------------------------------- rig / cámara
        static void BuildRig(Transform root, out Camera camera, out Transform left, out Transform right, out bool xriPrefab)
        {
            camera = null; left = right = null; xriPrefab = false;
            Type originType = DrumixUtil.FindType("Unity.XR.CoreUtils.XROrigin");
            if (originType != null)
            {
                var prefab = FindXROriginPrefab();
                if (prefab)
                {
                    var rig = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    rig.name = "XR Origin (DRUMIX)";
                    rig.transform.SetParent(root, false); rig.transform.localPosition = Vector3.zero; rig.transform.localRotation = Quaternion.identity;
                    camera = rig.GetComponentInChildren<Camera>(true);
                    int disabled = 0;
                    foreach (var mb in rig.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!mb) continue;
                        string ns = mb.GetType().Namespace ?? "";
                        string n = mb.GetType().Name;
                        if (ns.Contains("Locomotion") || n.Contains("MoveProvider") || n.Contains("TurnProvider") || n.Contains("Teleport"))
                        { mb.enabled = false; disabled++; }
                    }
                    left = FindController(rig.transform, true); right = FindController(rig.transform, false);
                    xriPrefab = true;
                    report.AppendLine("Rig VR: PROYECTO " + AssetDatabase.GetAssetPath(prefab) + " (locomoción desactivada: " + disabled + ")");
                }
                else
                {
                    var rig = new GameObject("XR Origin (DRUMIX respaldo)");
                    rig.transform.SetParent(root, false);
                    var offset = Child("Camera Offset", rig.transform, Vector3.zero);
                    var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(DrumixTrackedPose));
                    camGo.tag = "MainCamera";
                    camGo.transform.SetParent(offset.transform, false); camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
                    camGo.GetComponent<DrumixTrackedPose>().node = XRNode.CenterEye;
                    camera = camGo.GetComponent<Camera>();
                    var origin = rig.AddComponent(originType);
                    SetProp(origin, "Origin", rig); SetProp(origin, "Camera", camera); SetProp(origin, "CameraFloorOffsetObject", offset);
                    var mode = originType.GetProperty("RequestedTrackingOriginMode");
                    if (mode != null && mode.CanWrite)
                        try { mode.SetValue(origin, Enum.Parse(mode.PropertyType, "Floor")); } catch (Exception) { }
                    left = Controller(offset.transform, true); right = Controller(offset.transform, false);
                    report.AppendLine("Rig VR: RESPALDO DRUMIX (no encontré prefab 'XR Origin' en Assets; si importas XRI > Starter Assets y vuelves a ARMAR TODO, se usa ese).");
                }
            }
            else
            {
                var camGo = new GameObject("Main Camera (PC)", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(root, false);
                camGo.transform.localPosition = new Vector3(0, DrumixScaleRules.SeatedEyeHeight, -DrumixScaleRules.SeatedBack); camGo.transform.localRotation = Quaternion.Euler(DrumixScaleRules.SeatedPitch, 0, 0);
                camera = camGo.GetComponent<Camera>(); camera.fieldOfView = 70f;
                left = Child("Mano izq (sin VR)", camGo.transform, new Vector3(-0.25f, -0.4f, 0.5f)).transform;
                right = Child("Mano der (sin VR)", camGo.transform, new Vector3(0.25f, -0.4f, 0.5f)).transform;
                report.AppendLine("Rig: CÁMARA DE PC (no hay XR Interaction Toolkit). Teclado: A S D F G H J K, Enter.");
            }
            if (camera)
            {
                if (!camera.CompareTag("MainCamera")) camera.tag = "MainCamera";
                var view = camera.gameObject.GetComponent<DrumixDesktopView>();
                if (!view) view = camera.gameObject.AddComponent<DrumixDesktopView>();
                view.target = camera;
                // Vista de PC "sentado en la batería": ojos a 1.15 m, 0.45 m detrás, mirando 15° hacia abajo.
                view.desktopPosition = new Vector3(0, kitSurfaceY + DrumixScaleRules.SeatedEyeHeight, -DrumixScaleRules.SeatedBack);
                view.desktopEuler = new Vector3(DrumixScaleRules.SeatedPitch, 0, 0);
                if (!camera.GetComponent<AudioListener>()) camera.gameObject.AddComponent<AudioListener>();
            }
        }

        static GameObject FindXROriginPrefab()
        {
            GameObject best = null; int bestScore = int.MaxValue;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab XR Origin", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/DRUMIX/", StringComparison.Ordinal) || path.Contains("/BateriaVR/")) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                int score;
                if (file.Equals("XR Origin (XR Rig)", StringComparison.OrdinalIgnoreCase)) score = 0;
                else if (file.StartsWith("XR Origin", StringComparison.OrdinalIgnoreCase)) score = 10;
                else continue;
                if (path.Contains("Starter Assets")) score -= 1;
                if (file.IndexOf("Hands", StringComparison.OrdinalIgnoreCase) >= 0 || file.IndexOf("Device Simulator", StringComparison.OrdinalIgnoreCase) >= 0) score += 20;
                if (score >= bestScore) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go && go.GetComponentInChildren<Camera>(true)) { best = go; bestScore = score; }
            }
            return best;
        }

        static Transform FindController(Transform rig, bool leftHand)
        {
            string side = leftHand ? "left" : "right";
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name.Equals(leftHand ? "Left Controller" : "Right Controller", StringComparison.OrdinalIgnoreCase)) return t;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains(side) && n.Contains("controller")) return t;
            }
            Transform offset = rig;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) if (t.name == "Camera Offset") { offset = t; break; }
            report.AppendLine("  (no encontré " + side + " controller en el rig: añadí tracking de respaldo)");
            return Controller(offset, leftHand);
        }

        static Transform Controller(Transform parent, bool leftHand)
        {
            var go = new GameObject(leftHand ? "Left Controller (DRUMIX)" : "Right Controller (DRUMIX)", typeof(DrumixTrackedPose));
            go.transform.SetParent(parent, false);
            go.GetComponent<DrumixTrackedPose>().node = leftHand ? XRNode.LeftHand : XRNode.RightHand;
            return go.transform;
        }

        static void SetProp(Component c, string name, object value)
        {
            if (!c) return;
            var p = c.GetType().GetProperty(name);
            if (p != null && p.CanWrite) { try { p.SetValue(c, value, null); } catch (Exception) { } }
        }

        static bool stickLogged;
        static void BuildSticks(Transform hand, XRNode node)
        {
            if (!hand) return;
            var holder = new GameObject("Baqueta DRUMIX"); holder.transform.SetParent(hand, false);
            holder.transform.localRotation = Quaternion.Euler(20, 0, 0);
            var asset = DrumixAssetFinder.FindModel(DrumixKeywords.Stick, DrumixKeywords.StickExclude);
            GameObject visual = null;
            if (asset)
            {
                var axis = Child("Modelo", holder.transform, Vector3.zero).transform;
                visual = Place(asset, axis, 0.40f, 2, false);
                if (visual)
                {
                    Bounds b = LocalBounds(visual, axis);
                    // Alinear el lado largo con el "forward" del control.
                    if (b.size.x >= b.size.y && b.size.x >= b.size.z) axis.localRotation = Quaternion.Euler(0, 90, 0);
                    else if (b.size.y >= b.size.x && b.size.y >= b.size.z) axis.localRotation = Quaternion.Euler(90, 0, 0);
                    axis.localPosition = new Vector3(0, 0, 0.17f);
                    foreach (var c in visual.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                    DrumixUtil.NeutralizeGrab(visual, true);
                    foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
                    FixMaterials(visual, Mat("Baqueta", new Color(0.85f, 0.7f, 0.45f)));
                }
            }
            if (!visual)
            {
                var stick = Prim(PrimitiveType.Cylinder, "Baqueta (primitiva)", holder.transform, new Vector3(0, 0, 0.17f), new Vector3(0.018f, 0.2f, 0.018f), Mat("Baqueta", new Color(0.85f, 0.7f, 0.45f)));
                stick.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            var tip = new GameObject("Punta (golpea)", typeof(SphereCollider), typeof(Rigidbody), typeof(DrumixStick));
            tip.transform.SetParent(holder.transform, false);
            tip.transform.localPosition = new Vector3(0, 0, 0.36f);
            tip.GetComponent<SphereCollider>().radius = 0.03f; tip.GetComponent<SphereCollider>().isTrigger = true;
            var body = tip.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            tip.GetComponent<DrumixStick>().hand = node;
            if (!stickLogged || node == XRNode.RightHand)
                report.AppendLine("Baqueta " + (node == XRNode.LeftHand ? "izq" : "der") + ": " + (visual ? "PROYECTO " + AssetDatabase.GetAssetPath(asset) : "PRIMITIVA"));
            stickLogged = true;
        }

        // ---------------------------------------------------------------- público
        static readonly string[] SkinNames = { "skaterFemaleA", "skaterMaleA", "criminalMaleA", "cyborgFemaleA" };

        static void BuildCrowd(Transform root)
        {
            var crowd = Child("Publico", root, Vector3.zero);
            armsLowered = 0; crowdMin = 99f; crowdMax = 0f; crowdModels = 0; crowdPrims = 0; crowdFixed = 0;
            var director = crowd.AddComponent<DrumixThrowDirector>();
            var audio = crowd.AddComponent<DrumixCrowdAudio>();
            audio.cheer = PickClip(DrumixKeywords.Cheer, "Crowd/cheer.ogg", "Aplauso");
            audio.boo = PickClip(DrumixKeywords.Boo, "Crowd/boo.ogg", "Abucheo");
            audio.murmur = PickClip(DrumixKeywords.Murmur, "Crowd/murmur.ogg", "Murmullo");

            // Modelos: proyecto -> Kenney incluido -> cápsulas.
            var models = DrumixAssetFinder.FindModels(DrumixKeywords.Crowd, DrumixKeywords.CrowdExclude, 6);
            string origin;
            GameObject kenney = null;
            if (models.Count > 0)
            {
                origin = "PROYECTO: ";
                foreach (var m in models) origin += AssetDatabase.GetAssetPath(m) + "  ";
            }
            else
            {
                EnsureCharacterImport(DrumixAssetFinder.Bundled + "/Characters/characterMedium.fbx");
                kenney = DrumixAssetFinder.LoadBundled<GameObject>("Characters/characterMedium.fbx");
                // Sin Animator: el clip idle.fbx (otro rig, en cm) dejaba al público gigante y en T-pose. El movimiento es procedural.
                origin = kenney ? "INCLUIDO (Kenney CC0, 4 skins)" : "PRIMITIVAS (cápsulas)";
            }
            report.AppendLine("Público: " + origin);

            var rowsDef = crowdRowsOverride ?? new[] { new[] { 3.6f, 7f, 62f }, new[] { 4.7f, 8f, 66f } };
            var rows = rowsDef.Select(x => new { r = x[0], n = (int)x[1], a = x[2] }).ToArray();
            Vector3 center = new Vector3(0, 0, DrumixLayout.CrowdCenterZ(teamStage));
            int index = 0;
            var rnd = new System.Random(42);
            foreach (var row in rows)
                for (int i = 0; i < row.n; i++)
                {
                    float t = row.n == 1 ? 0.5f : (float)i / (row.n - 1);
                    float ang = Mathf.Lerp(-row.a, row.a, t) * Mathf.Deg2Rad;
                    Vector3 pos = center + new Vector3(Mathf.Sin(ang) * row.r, floorY, Mathf.Cos(ang) * row.r);
                    var npc = Child("NPC_" + (++index).ToString("00"), crowd.transform, pos);
                    Vector3 look = new Vector3(0, 0, 0) - pos; look.y = 0;
                    npc.transform.localRotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                    float height = DrumixScaleRules.PersonHeight(index);
                    var visualRoot = Child("Visual", npc.transform, Vector3.zero); // lo que anima DrumixCrowdNPC (sin escala)
                    GameObject visual = null;
                    if (models.Count > 0) visual = Place(models[index % models.Count], visualRoot.transform, height, 1, true);
                    else if (kenney)
                    {
                        visual = Place(kenney, visualRoot.transform, height, 1, true);
                        if (visual)
                        {
                            var skin = DrumixAssetFinder.LoadBundled<Texture2D>("Characters/Skins/" + SkinNames[index % SkinNames.Length] + ".png");
                            var mat = Mat("Publico_" + SkinNames[index % SkinNames.Length], Color.white, 0, skin, false, 0.2f);
                            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                            { var arr = r.sharedMaterials; for (int k = 0; k < arr.Length; k++) arr[k] = mat; r.sharedMaterials = arr; }
                            foreach (var anim in visual.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(anim);
                        }
                    }
                    if (visual)
                    {
                        // T-pose -> brazos abajo, y re-medir (los brazos abiertos inflaban el ancho) para dejar la altura exacta.
                        if (DrumixUtil.LowerArms(visual.transform, Vector3.up) > 0) armsLowered++;
                        var wrap = visual.transform.parent;
                        Bounds vb = LocalBounds(visual, npc.transform);
                        if (vb.size.y > 0.01f) { wrap.localScale *= height / vb.size.y; vb = LocalBounds(visual, npc.transform); }
                        wrap.localPosition += new Vector3(-vb.center.x, -vb.min.y, -vb.center.z);
                        vb = LocalBounds(visual, npc.transform);
                        // Red de seguridad: si la medida salió absurda (escala NaN/enorme, altura fuera de 1.2–2.4 m o lejos de su sitio),
                        // en vez de dejar un gigante invisible se pone una persona de primitivas. Nunca queda un NPC vacío.
                        float sc = wrap.localScale.x;
                        bool bad = !DrumixScaleRules.PersonPlausible(vb.size.y, vb.min.y, vb.center.x, vb.center.z, sc);
                        if (bad)
                        {
                            Debug.LogWarning("[DRUMIX] " + npc.name + ": el modelo midió " + vb.size.y.ToString("0.00") + " m (escala " + sc + "); se usa persona de primitivas.");
                            UnityEngine.Object.DestroyImmediate(wrap.gameObject); visual = null; crowdFixed++;
                        }
                        else { DrumixOverlapNote(npc.name, vb); crowdModels++; }
                    }
                    if (!visual) { visual = CapsulePerson(visualRoot.transform, height, rnd); crowdPrims++; DrumixOverlapNote(npc.name, LocalBounds(visual, npc.transform)); }
                    FixMaterials(visual, Mat("Publico_Ropa", new Color(0.3f, 0.3f, 0.4f)));
                    foreach (var c in visual.GetComponentsInChildren<Collider>(true)) c.enabled = false; // que no choquen con lo que tiran
                    DrumixUtil.NeutralizeGrab(visual, true);
                    foreach (var a in visual.GetComponentsInChildren<Animator>(true)) { a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
                    foreach (var lod in visual.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
                    foreach (var tr in visual.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = 0;
                    var c2 = npc.AddComponent<DrumixCrowdNPC>();
                    c2.seed = index * 31 + 7;
                    c2.visual = visualRoot.transform;
                    c2.scaleRoot = visual.transform.parent != visualRoot.transform ? visual.transform.parent : visual.transform;
                    c2.targetHeight = height;
                    if (crowdBig)
                    {
                        // Público grande (Quest): malla estática compartida (sin skinning por cuadro), sin sombras, animación escalonada.
                        if (BakeCrowdMeshes && BakeStatic(visual, npc.transform)) crowdBaked++; // apagado: sin probar en Unity, el público queda animado normal
                        foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
                        c2.updateEvery = index > 12 ? 2 : 1;
                    }
                }

            if (crowdBig) report.AppendLine("  Público grande: sin sombras y con animación escalonada" + (crowdBaked > 0 ? ", " + crowdBaked + " con malla estática compartida." : " (animado normal, sin hornear)."));
            director.templates = BuildThrowables(crowd.transform);
            report.AppendLine("  Público creado: " + (crowdModels + crowdPrims) + " personas (" + crowdModels + " modelos, " + crowdPrims + " de primitivas" +
                              (crowdFixed > 0 ? ", " + crowdFixed + " cambiadas a primitivas por medida rara" : "") + ").");
            Debug.Log("[DRUMIX] Público creado: " + (crowdModels + crowdPrims) + " personas (" + crowdModels + " modelos, " + crowdPrims + " primitivas).");
            report.AppendLine("  Personas medidas por malla real: " + crowdMin.ToString("0.00") + "–" + crowdMax.ToString("0.00") + " m de alto" +
                              (armsLowered > 0 ? " · brazos bajados en " + armsLowered : ""));
            if (crowdMax > DrumixScaleRules.PersonMax * 1.05f || crowdMin < DrumixScaleRules.PersonMin * 0.9f)
                report.AppendLine("  ⚠ Alguna persona quedó fuera de 1.60–1.85 m: al dar Play DrumixCrowdNPC la corrige midiendo la malla.");
        }
        /// Import del personaje Kenney: legible (para medir/BakeMesh también en Quest), sin cámaras/luces ni animación propia.
        /// La escala NO se toca: se mide la malla real y se ajusta en el envoltorio.
        /// El .meta incluido YA trae estos valores, así que normalmente no cambia nada. Se llama una vez por ARMAR TODO (nunca desde
        /// un AssetPostprocessor) y solo reimporta si algún valor difiere: no puede entrar en bucle.
        static void EnsureCharacterImport(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            bool changed = false;
            if (!mi.isReadable) { mi.isReadable = true; changed = true; }
            if (mi.importCameras) { mi.importCameras = false; changed = true; }
            if (mi.importLights) { mi.importLights = false; changed = true; }
            if (mi.importAnimation) { mi.importAnimation = false; changed = true; }
            if (changed) { mi.SaveAndReimport(); report.AppendLine("  Import de " + Path.GetFileName(path) + " ajustado (legible, sin animación/cámaras/luces)."); }
        }
        static int armsLowered, crowdModels, crowdPrims, crowdFixed; static float crowdMin = 99f, crowdMax;
        static void DrumixOverlapNote(string name, Bounds b) { crowdMin = Mathf.Min(crowdMin, b.size.y); crowdMax = Mathf.Max(crowdMax, b.size.y); }

        static AudioClip PickClip(string[] keywords, string bundled, string label)
        {
            var clip = DrumixAssetFinder.FindClip(keywords, DrumixKeywords.CrowdClipExclude);
            if (clip) { report.AppendLine("  " + label + ": PROYECTO " + AssetDatabase.GetAssetPath(clip)); return clip; }
            clip = DrumixAssetFinder.LoadBundled<AudioClip>(bundled);
            report.AppendLine("  " + label + ": " + (clip ? "INCLUIDO" : "PROCEDURAL"));
            return clip;
        }

        static GameObject CapsulePerson(Transform parent, float height, System.Random rnd)
        {
            var person = Child("Persona (primitiva)", parent, Vector3.zero);
            Color shirt = Color.HSVToRGB((float)rnd.NextDouble(), 0.55f, 0.8f);
            var body = Mat("Publico_Camisa_" + (int)(shirt.r * 9) + (int)(shirt.g * 9) + (int)(shirt.b * 9), shirt);
            var skin = Mat("Publico_Piel", new Color(0.85f, 0.65f, 0.5f));
            Prim(PrimitiveType.Capsule, "Cuerpo", person.transform, new Vector3(0, height * 0.4f, 0), new Vector3(0.45f, height * 0.4f, 0.32f), body);
            Prim(PrimitiveType.Sphere, "Cabeza", person.transform, new Vector3(0, height * 0.88f, 0), Vector3.one * 0.24f, skin);
            return person;
        }

        static GameObject[] BuildThrowables(Transform crowd)
        {
            var parent = Child("Lanzables (plantillas, inactivas)", crowd, new Vector3(0, -10, 0));
            var list = new List<GameObject>();
            var foodTex = DrumixAssetFinder.LoadBundled<Texture2D>("Food/Textures/colormap.png");
            var foodMat = Mat("Comida_Kenney", Color.white, 0, foodTex, false, 0.3f);
            list.Add(Throwable(parent.transform, "Zanahoria", new[] { "carrot", "zanahoria" }, "Food/carrot.fbx", 0.2f, foodMat, new Color(1f, 0.5f, 0.1f)));
            list.Add(Throwable(parent.transform, "Tomate", DrumixKeywords.Tomato, "Food/tomato.fbx", 0.11f, foodMat, new Color(0.9f, 0.15f, 0.1f)));
            list.Add(Throwable(parent.transform, "Cerveza", new[] { "beerbottle", "beer bottle", "beer", "cerveza", "botella" }, "Bar/BeerBottle.obj", 0.24f, null, new Color(0.35f, 0.2f, 0.05f)));
            list.Add(Throwable(parent.transform, "Vaso", new[] { "pint", "pintglass", "vaso", "beer mug" }, "Bar/PintGlass.obj", 0.15f, null, new Color(0.95f, 0.8f, 0.3f)));
            list.RemoveAll(g => !g);
            parent.SetActive(false);
            return list.ToArray();
        }

        static GameObject Throwable(Transform parent, string name, string[] keywords, string bundled, float size, Material bundledMat, Color color)
        {
            var holder = Child(name, parent, Vector3.zero);
            var asset = DrumixAssetFinder.FindModel(keywords, DrumixKeywords.ThrowExclude);
            string origin = asset ? "PROYECTO " + AssetDatabase.GetAssetPath(asset) : null;
            if (!asset && bundled != null) { asset = DrumixAssetFinder.LoadBundled<GameObject>(bundled); if (asset) origin = "INCLUIDO"; }
            GameObject visual = asset ? Place(asset, holder.transform, size, 2, false) : null;
            if (visual)
            {
                if (origin == "INCLUIDO" && !bundledMat) FixMaterials(visual, Mat("Lanzable_" + name, color), BundledMtl(bundled));
                if (origin == "INCLUIDO" && bundledMat)
                    foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) { var arr = r.sharedMaterials; for (int i = 0; i < arr.Length; i++) arr[i] = bundledMat; r.sharedMaterials = arr; }
                FixMaterials(visual, Mat("Lanzable_" + name, color));
                foreach (var c in visual.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            }
            else
            {
                origin = "PRIMITIVA";
                var m = Mat("Lanzable_" + name, color, 0, null, false, 0.8f);
                if (name == "Cerveza")
                {
                    Prim(PrimitiveType.Cylinder, "Cuerpo", holder.transform, Vector3.zero, new Vector3(0.07f, 0.08f, 0.07f), m);
                    Prim(PrimitiveType.Cylinder, "Cuello", holder.transform, new Vector3(0, 0.11f, 0), new Vector3(0.03f, 0.04f, 0.03f), m);
                }
                else Prim(PrimitiveType.Cylinder, "Cuerpo", holder.transform, Vector3.zero, new Vector3(size * 0.6f, size * 0.5f, size * 0.6f), m);
            }
            var col = holder.AddComponent<SphereCollider>(); col.radius = size * 0.4f;
            holder.AddComponent<DrumixProjectile>();
            report.AppendLine("  Lanzable " + name + ": " + origin);
            return holder;
        }

        // ---------------------------------------------------------------- UI + EventSystem
        static void BuildUI(Transform root, Camera camera, bool xriPrefab, Transform left, Transform right)
        {
            var go = Child("UI (world-space, VR + PC)", root, new Vector3(0, 1.95f, 2.3f));
            var ui = go.AddComponent<DrumixUI>();
            ui.metersPerPixel = 0.0012f;
            ui.hoverClip = PickUi(DrumixKeywords.UiHover, "UI/hover.ogg");
            ui.clickClip = PickUi(DrumixKeywords.UiClick, "UI/click.ogg");
            const string tracked = "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster";
            if (xriPrefab && DrumixUtil.FindType(tracked) != null) ui.xrRaycasterType = tracked;
            ui.Build(); // visible ya en la Scene view (en Play se reconstruye igual)

            if (reuseEventSystem)
            {
                // Escena del equipo: ya trae EventSystem (XRI) y rayos en su rig: no se pone un segundo EventSystem.
                report.AppendLine("UI: EventSystem y rayos del EQUIPO (no se duplica)");
                return;
            }
            var es = new GameObject("EventSystem (DRUMIX)", typeof(EventSystem));
            es.transform.SetParent(root, false);
            Type xrModule = DrumixUtil.FindType("UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule");
            if (xriPrefab && xrModule != null)
            {
                var m = es.AddComponent(xrModule);
                SetProp(m, "enableXRInput", true); SetProp(m, "enableMouseInput", true); SetProp(m, "enableTouchInput", true);
                report.AppendLine("UI: XRI (rayos del rig) + mouse");
            }
            else
            {
                DrumixUI.AddBestInputModule(es);
                var lineMat = Mat("Rayo_UI", new Color(0.75f, 0.5f, 1f), 1f, null, true);
                foreach (var hand in new[] { left, right })
                {
                    if (!hand || camera && hand.IsChildOf(camera.transform)) continue;
                    var p = hand.gameObject.AddComponent<DrumixVRPointer>();
                    p.hand = hand == left ? XRNode.LeftHand : XRNode.RightHand;
                    var ray = new GameObject("Rayo UI"); ray.transform.SetParent(hand, false);
                    var line = ray.AddComponent<LineRenderer>();
                    line.sharedMaterial = lineMat; line.useWorldSpace = true; line.positionCount = 2;
                    line.startWidth = line.endWidth = 0.003f; line.enabled = false;
                    p.line = line;
                }
                report.AppendLine("UI: mouse + rayo DRUMIX con gatillo");
            }
        }

        static bool reuseEventSystem;
        static AudioClip PickUi(string[] keywords, string bundled)
        {
            var c = DrumixAssetFinder.FindClip(keywords, DrumixKeywords.UiExclude);
            return c ? c : DrumixAssetFinder.LoadBundled<AudioClip>(bundled);
        }

        // =====================================================================
        [MenuItem("DRUMIX/Convertir selección en público", false, 20)]
        public static void ConvertSelection()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (!go.scene.IsValid()) continue; // solo objetos en escena, no assets
                if (go.GetComponent<DrumixCrowdNPC>()) continue;
                Undo.AddComponent<DrumixCrowdNPC>(go);
                foreach (var a in go.GetComponentsInChildren<Animator>(true)) { Undo.RecordObject(a, "DRUMIX público"); a.applyRootMotion = false; }
                n++;
            }
            EditorUtility.DisplayDialog("DRUMIX", n > 0 ? n + " objeto(s) ahora son público. Ctrl+D para duplicar." : "Selecciona objetos de la escena (modelos/cápsulas) primero.", "OK");
        }
        [MenuItem("DRUMIX/Convertir selección en público", true)]
        static bool ConvertSelectionValid() { return Selection.gameObjects.Length > 0; }

        [MenuItem("DRUMIX/Abrir escena DRUMIX_Bar", false, 40)]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("DRUMIX", "Aún no existe. Usa DRUMIX > ARMAR TODO (1 click).", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
