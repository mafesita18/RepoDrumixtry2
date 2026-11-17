using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace Drumix.EditorTools
{
    /// ARMAR TODO dentro de las escenas DEL EQUIPO (1.NivelUno = El Garaje, 2.NivelDos = El Bar Local, 3.NivelTres = El Gran Concierto).
    /// Solo se agrega/reemplaza DRUMIX_ROOT: nada del equipo se borra, mueve ni edita en el archivo de la escena.
    /// Lo viejo del equipo que chocaría (su batería con DrumPad, sus baquetas, su UI) lo apaga DrumixTeamSceneAdapter SOLO al dar Play.
    public static partial class DrumixBuilder
    {
        /// Escenas del equipo con nivel (0, 1, 2...), en orden. Ignora DRUMIX, Samples, BackUp, Sample, Basic, _Recovery.
        public static List<string> FindTeamLevelScenes()
        {
            var byLevel = new SortedDictionary<int, string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/DRUMIX", StringComparison.Ordinal) || path.Contains("/Samples/") || path.Contains("/_Recovery/") ||
                    path.Contains("/Simple Garage/") || path.Contains("/Simple_garage/") || path.Contains("/OlegWER/")) continue;
                int level = DrumixLevelData.LevelFromSceneName(Path.GetFileNameWithoutExtension(path));
                if (level < 0) continue;
                string prev;
                // Si hay dos (p. ej. una copia), gana la que está en Assets/Scenes.
                if (byLevel.TryGetValue(level, out prev) && prev.StartsWith("Assets/Scenes/", StringComparison.Ordinal)) continue;
                byLevel[level] = path;
            }
            var list = new List<string>();
            int expect = 0;
            foreach (var kv in byLevel) { if (kv.Key != expect) break; list.Add(kv.Value); expect++; } // 0,1,2 seguidos
            return list;
        }

        /// Niveles del equipo primero y en orden (su LevelManager usa buildIndex), luego lo que ya hubiera, DRUMIX_Bar al final.
        static void EnsureTeamBuildSettings(List<string> teamScenes)
        {
            var old = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in teamScenes) list.Add(new EditorBuildSettingsScene(p, true));
            foreach (var s in old) if (!teamScenes.Contains(s.path) && s.path != ScenePath) list.Add(s);
            var bar = old.FirstOrDefault(s => s.path == ScenePath);
            list.Add(new EditorBuildSettingsScene(ScenePath, bar == null || bar.enabled));
            bool same = list.Count == old.Count;
            for (int i = 0; same && i < list.Count; i++) same = list[i].path == old[i].path && list[i].enabled == old[i].enabled;
            if (!same) EditorBuildSettings.scenes = list.ToArray();
            report.AppendLine("Build Settings: " + string.Join(" · ", list.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray()) + (same ? " (ya estaba)" : " (actualizado)"));
        }

        /// Arma DRUMIX dentro de cada escena de nivel del equipo. Devuelve las rutas armadas (vacío si el proyecto no las tiene).
        static List<string> BuildTeamScenes(DrumixLevel[] levelAssets)
        {
            var scenes = FindTeamLevelScenes();
            report.Length = 0;
            if (scenes.Count == 0) { report.AppendLine("Escenas del equipo: no encontré 1.NivelUno/2.NivelDos/3.NivelTres (todo queda en DRUMIX_Bar)."); return scenes; }
            EnsureTeamBuildSettings(scenes);
            var names = scenes.Select(Path.GetFileNameWithoutExtension).ToArray();
            var levelScenes = new string[Mathf.Max(names.Length, levelAssets.Length)];
            for (int i = 0; i < levelScenes.Length; i++) levelScenes[i] = i < names.Length ? names[i] : "";
            for (int i = 0; i < scenes.Count; i++)
            {
                try { BuildTeamScene(scenes[i], i, levelAssets, levelScenes); }
                catch (Exception e) { Debug.LogException(e); report.AppendLine("ERROR en " + scenes[i] + ": " + e.Message + " (pega este error en el chat)"); }
            }
            AssetDatabase.SaveAssets();
            return scenes;
        }

        static void BuildTeamScene(string path, int level, DrumixLevel[] levelAssets, string[] levelScenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string label = Path.GetFileNameWithoutExtension(path) + " → " + (level < levelAssets.Length && levelAssets[level] ? levelAssets[level].spec.name : "nivel " + (level + 1));
            report.AppendLine("\n=== " + label + " ===");
            loggedFurniture.Clear(); stickLogged = false;
            var kept = SaveUserObjects(scene);
            RemoveOldRoots(scene, false); // en escenas del equipo NUNCA se limpia nada más que DRUMIX_ROOT

            // ---- lo que ya trae la escena
            Transform teamKit = FindTeamKitInScene(scene);
            Component origin = FindTeamRig(scene);
            Transform rig = origin ? origin.transform : null;
            Camera camera = rig ? rig.GetComponentInChildren<Camera>(true) : null;
            bool hasEventSystem = scene.GetRootGameObjects().Any(g => g.GetComponentInChildren<EventSystem>(true));
            report.AppendLine("  Del equipo: batería " + (teamKit ? "'" + teamKit.name + "'" : "no") + " · XR Origin " + (rig ? "'" + rig.name + "'" : "no") +
                              " · EventSystem " + (hasEventSystem ? "sí" : "no"));

            var temp = new List<Collider>();
            var geometry = new HashSet<Collider>();
            GameObject root = null;
            int mood = level; // 0 Garaje · 1 Bar Local · 2 Gran Concierto (otros niveles: sin preset)
            var doors = FindDoors(scene, null);
            try
            {
                teamStage = false; floorY = 0f; kitSurfaceY = 0f; playerOriginOverride = Vector3.zero;
                reuseEventSystem = hasEventSystem && rig;
                root = new GameObject(RootName);

                var game = new GameObject("DRUMIX_Juego (eventos + secuencia)");
                game.transform.SetParent(root.transform, false);
                var drumGame = game.AddComponent<DrumixGame>(); game.AddComponent<DrumixInput>(); game.AddComponent<DrumixTeamBridge>();
                drumGame.levels = levelAssets; drumGame.levelIndex = level; drumGame.sceneLevel = level; drumGame.levelScenes = levelScenes;

                var kit = BuildKit(root.transform);
                // El bar se arma solo si alrededor de la batería no hay nada del equipo (se decide con la escena real más abajo);
                // la tarima del bar (4 cm) sube batería y asiento, así que se arma ANTES del acomodo para que ResolveLayout lo vea.
                GameObject bar = null; float lift = 0f;
                var ambient = Child("Ambiente DRUMIX (nivel)", root.transform, Vector3.zero);
                if (mood == MoodBar) { bar = BuildTeamBar(ambient.transform); lift = TarimaTop; }
                SnapToSurface(kit, lift + 0.006f);
                Transform left, right; bool xri;
                var seat = Child("Asiento (jugador DRUMIX)", root.transform, new Vector3(0, lift, 0)).transform;
                if (rig && camera)
                {
                    xri = true;
                    left = TeamHand(rig, root.transform, true); right = TeamHand(rig, root.transform, false);
                    var view = Child("Vista PC (DRUMIX)", root.transform, Vector3.zero).AddComponent<DrumixDesktopView>();
                    view.target = camera; view.space = root.transform; view.rig = rig;
                    view.desktopPosition = new Vector3(0, lift + DrumixScaleRules.SeatedEyeHeight, -DrumixScaleRules.SeatedBack);
                    view.desktopEuler = new Vector3(DrumixScaleRules.SeatedPitch, 0, 0);
                    report.AppendLine("  Rig: el XR Origin DEL EQUIPO (se sienta en la batería al dar Play; su locomoción se apaga).");
                }
                else
                {
                    reuseEventSystem = false;
                    BuildRig(root.transform, out camera, out left, out right, out xri);
                }
                BuildSticks(left, XRNode.LeftHand); BuildSticks(right, XRNode.RightHand);
                // Concierto: público GRANDE (48) en 4 filas; barato para Quest (malla estática compartida, sin sombras, escalonado).
                crowdRowsOverride = mood == MoodConcert ? ConcertRows : null;
                crowdBig = mood == MoodConcert; crowdBaked = 0; bakedCrowd.Clear();
                BuildCrowd(root.transform);
                crowdRowsOverride = null; crowdBig = false;
                BuildUI(root.transform, camera, xri, left, right);
                ResolveLayout(root);
                BuildFX(root.transform, kit);
                var lights = Child("Luces DRUMIX (batería y público)", root.transform, Vector3.zero).transform;
                var kitLight = Point(lights, "Luz bateria", new Vector3(0, 2.2f, 0.4f), new Color(1f, 0.9f, 0.75f), 2f, 4f);
                var crowdLight = Point(lights, "Luz publico", new Vector3(0, 3f, 3.5f), new Color(1f, 0.7f, 0.85f), 2.5f, 8f);

                // ---- ubicar todo donde el equipo puso su batería (o delante de su rig), sobre el piso/tarima real
                CollectGeometry(scene, root, teamKit, rig, geometry, temp, doors);
                Physics.SyncTransforms();
                PlaceRoot(root.transform, kit, teamKit, rig, geometry);
                Physics.SyncTransforms();
                if (bar)
                {
                    if (TeamHasRoomAround(root.transform, geometry))
                    {
                        UnityEngine.Object.DestroyImmediate(bar); bar = null;
                        report.AppendLine("  Bar Local: la escena del equipo ya tiene paredes/muebles alrededor; no se arma el bar DRUMIX (no se duplica).");
                    }
                    else
                    {
                        FitBarWalls(bar, geometry);
                        foreach (var c in bar.GetComponentsInChildren<Collider>(false)) geometry.Add(c); // piso y paredes del bar cuentan para el público
                        Physics.SyncTransforms();
                    }
                }
                CoverCoplanar(root.transform, geometry, ambient);
                if (doors.Count > 0)
                    report.AppendLine("  Portón: '" + string.Join("', '", doors.Select(d => d.name).ToArray()) + "' se ABRE al dar Play (se oculta) para que el público vea desde afuera.");
                FitUi(root.transform, geometry);
                Physics.SyncTransforms();
                PlaceCrowd(root.transform, kit, geometry, crowdLight, EnvBlockers(bar), mood == MoodConcert ? 18f : 12f, mood == MoodConcert ? 0.75f : 0.85f);
                ApplyLevelMood(root.transform, mood, camera, kitLight, crowdLight, kit, ambient);

                var adapter = root.AddComponent<DrumixTeamSceneAdapter>();
                adapter.teamRig = rig; adapter.seat = seat;
                adapter.hideOnPlay = TeamThingsToHide(scene, root, teamKit, rig);
                foreach (var d in doors) if (!adapter.hideOnPlay.Contains(d)) adapter.hideOnPlay.Add(d);
                report.AppendLine("  Al dar Play se ocultan del equipo: " + (adapter.hideOnPlay.Count > 0 ? string.Join(", ", adapter.hideOnPlay.Select(g => g.name).ToArray()) : "nada") +
                                  " (en el archivo de la escena siguen igual).");
                try { foreach (var g in adapter.hideOnPlay) SceneVisibilityManager.instance.Hide(g, true); } catch (Exception) { } // solo la vista Scene

                foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);
                var info = root.AddComponent<DrumixRootInfo>();
                info.builtWith = "DRUMIX " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · " + label;
                info.builtVersion = BuildVersion;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) info.generated.Add(t.gameObject);
                RestoreUserObjects(root, kept);
            }
            finally
            {
                foreach (var c in temp) if (c) UnityEngine.Object.DestroyImmediate(c);
                playerOriginOverride = null; reuseEventSystem = false; crowdRowsOverride = null; crowdBig = false;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- detección en la escena del equipo
        static bool UnderAny(Transform t, params Transform[] parents)
        {
            foreach (var p in parents) if (p && (t == p || t.IsChildOf(p))) return true;
            return false;
        }

        /// La batería del equipo: el objeto cuyos hijos coinciden con la firma calibrada (BATERIA = Drum Set_sketchfab desempacado).
        static Transform FindTeamKitInScene(Scene scene)
        {
            Transform best = null; int bestOk = 0;
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == RootName || t.childCount < 4) continue;
                    int ok = 0;
                    foreach (var probe in DrumixTeamKitData.Signature)
                    {
                        var c = t.Find(probe.node);
                        if (!c) continue;
                        Vector3 local = t.InverseTransformPoint(c.position);
                        if ((local - new Vector3(probe.x, probe.y, probe.z)).magnitude <= DrumixTeamKitData.SignatureTolerance * Mathf.Max(1f, t.lossyScale.x)) ok++;
                    }
                    if (ok > bestOk) { bestOk = ok; best = t; }
                }
            if (best && bestOk >= DrumixTeamKitData.Signature.Length - 1) return best;
            foreach (var r in scene.GetRootGameObjects())
                if (r.name.Equals("BATERIA", StringComparison.OrdinalIgnoreCase) || DrumixTeamKitData.IsTeamKit(r.name)) return r.transform;
            return null;
        }

        static Component FindTeamRig(Scene scene)
        {
            Type t = DrumixUtil.FindType("Unity.XR.CoreUtils.XROrigin");
            if (t == null) return null;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r.name == RootName) continue;
                var c = r.GetComponentInChildren(t, true);
                if (c && c.gameObject.activeInHierarchy) return c;
            }
            return null;
        }

        /// Mano del rig del equipo SIN meter nada dentro de su prefab: un objeto DRUMIX que copia la pose del control.
        static Transform TeamHand(Transform rig, Transform root, bool leftHand)
        {
            Transform ctrl = null;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                if (t.name.Equals(leftHand ? "Left Controller" : "Right Controller", StringComparison.OrdinalIgnoreCase)) { ctrl = t; break; }
            if (!ctrl)
                foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains(leftHand ? "left" : "right") && (n.Contains("controller") || n.Contains("hand"))) { ctrl = t; break; }
                }
            var hands = root.Find("Manos (siguen al rig del equipo)");
            if (!hands) hands = Child("Manos (siguen al rig del equipo)", root, Vector3.zero).transform;
            if (ctrl)
            {
                var go = new GameObject(leftHand ? "Mano izq → " + ctrl.name : "Mano der → " + ctrl.name);
                go.transform.SetParent(hands, false);
                go.AddComponent<DrumixFollow>().target = ctrl;
                return go.transform;
            }
            // Sin control con nombre: tracking directo (el rig se sienta en DRUMIX_ROOT, así que el espacio coincide).
            report.AppendLine("  (no encontré el control " + (leftHand ? "izq" : "der") + " del rig: tracking DRUMIX de respaldo)");
            return Controller(hands, leftHand);
        }

        /// Geometría del equipo para rayos/choques: sus colliders + MeshCollider TEMPORALES (no se guardan) en mallas sin collider.
        static void CollectGeometry(Scene scene, GameObject root, Transform teamKit, Transform rig, HashSet<Collider> geometry, List<Collider> temp, List<GameObject> skip = null)
        {
            var skipT = (skip ?? new List<GameObject>()).Where(g => g).Select(g => g.transform).ToArray();
            Transform sim = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name.IndexOf("Device Simulator", StringComparison.OrdinalIgnoreCase) >= 0) sim = r.transform;
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r == root || !r.activeInHierarchy) continue;
                foreach (var col in r.GetComponentsInChildren<Collider>(false))
                    if (col.enabled && !col.isTrigger && !UnderAny(col.transform, teamKit, rig, sim) && !UnderAny(col.transform, skipT) && !col.GetComponentInParent<Rigidbody>()) geometry.Add(col);
                foreach (var mf in r.GetComponentsInChildren<MeshFilter>(false))
                {
                    if (!mf.sharedMesh || UnderAny(mf.transform, teamKit, rig, sim) || UnderAny(mf.transform, skipT) || mf.GetComponent<Collider>() || mf.GetComponentInParent<Canvas>()) continue;
                    var mr = mf.GetComponent<MeshRenderer>(); if (!mr || !mr.enabled) continue;
                    if (mf.GetComponentInParent<Rigidbody>()) continue; // cosas que se agarran/caen
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.hideFlags = HideFlags.HideAndDontSave; mc.sharedMesh = mf.sharedMesh;
                    temp.Add(mc); geometry.Add(mc);
                }
            }
        }

        static bool GeometryHitDown(Vector3 from, float distance, HashSet<Collider> geometry, out float y)
        {
            y = 0; bool any = false;
            foreach (var h in Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (geometry.Contains(h.collider) && h.normal.y > 0.6f && (!any || h.point.y > y)) { y = h.point.y; any = true; }
            return any;
        }
        static bool GeometryBlocks(Vector3 a, Vector3 b, HashSet<Collider> geometry, float margin)
        {
            Vector3 d = b - a; float len = d.magnitude; if (len < 0.01f) return false;
            foreach (var h in Physics.RaycastAll(a, d / len, len, ~0, QueryTriggerInteraction.Ignore))
                if (geometry.Contains(h.collider) && h.distance < len - margin) return true;
            return false;
        }
        static bool GeometryOverlaps(Vector3 center, Vector3 half, HashSet<Collider> geometry)
        {
            foreach (var c in Physics.OverlapBox(center, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (geometry.Contains(c)) return true;
            return false;
        }

        /// DRUMIX_ROOT se arma en (0,0,0) mirando a +Z y después se mueve entero: la batería DRUMIX cae donde el equipo puso
        /// la suya (mismo centro y giro), apoyada en la superficie real que hay debajo (piso del garaje, tarima del concierto...).
        static void PlaceRoot(Transform root, GameObject kit, Transform teamKit, Transform rig, HashSet<Collider> geometry)
        {
            Transform ourModelRoot = null;
            foreach (Transform c in kit.transform) if (c.name.StartsWith("Bateria del equipo", StringComparison.Ordinal)) ourModelRoot = c;
            Vector3 ourCenter; Bounds kb;
            if (ourModelRoot) ourCenter = ourModelRoot.position;
            else ourCenter = DrumixOverlap.TryBounds(kit, out kb) ? kb.center : kit.transform.position;
            ourCenter.y = 0;

            Vector3 target; float yaw; float guessFloor; string how;
            if (teamKit)
            {
                yaw = teamKit.eulerAngles.y;
                target = teamKit.position;
                guessFloor = ourModelRoot || DrumixTeamKitData.IsTeamKit(teamKit.name) || teamKit.Find(DrumixTeamKitData.Signature[0].node)
                    ? teamKit.position.y - DrumixTeamKitData.RootHeight * teamKit.lossyScale.y : teamKit.position.y;
                how = "donde el equipo puso su batería '" + teamKit.name + "'";
            }
            else if (rig)
            {
                yaw = rig.eulerAngles.y;
                target = rig.position + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, ourCenter.z);
                guessFloor = rig.position.y;
                how = "delante del XR Origin del equipo";
            }
            else { yaw = 0; target = ourCenter; guessFloor = 0; how = "en el origen (la escena no tiene batería ni rig del equipo)"; }

            var rot = Quaternion.Euler(0, yaw, 0);
            Vector3 pos = new Vector3(target.x, 0, target.z) - rot * ourCenter;
            // superficie: rayo hacia abajo desde ~1.2 m sobre el piso estimado (por debajo de techos), en la batería y en el asiento.
            float surface = guessFloor, y;
            Vector3 kitWorld = pos + rot * ourCenter, seatWorld = pos;
            bool hitKit = GeometryHitDown(new Vector3(kitWorld.x, guessFloor + 1.2f, kitWorld.z), 4f, geometry, out y);
            if (hitKit) surface = y;
            float ySeat;
            if (GeometryHitDown(new Vector3(seatWorld.x, guessFloor + 1.2f, seatWorld.z), 4f, geometry, out ySeat) && (!hitKit || ySeat > surface)) surface = ySeat;
            root.SetPositionAndRotation(new Vector3(pos.x, surface, pos.z), rot);
            report.AppendLine("  Batería DRUMIX " + how + ": (" + root.position.x.ToString("0.00") + ", " + surface.ToString("0.00") + ", " + root.position.z.ToString("0.00") +
                              "), giro " + yaw.ToString("0") + "°, apoyada en " + (hitKit ? "la superficie real de la escena" : "el piso estimado (no encontré piso con collider/malla)") + ".");
        }

        /// La UI flotante no puede quedar dentro de una pared/puerta: si algo la tapa desde los ojos, se acerca (mín. 1.1 m).
        static void FitUi(Transform root, HashSet<Collider> geometry)
        {
            DrumixUI ui = root.GetComponentInChildren<DrumixUI>(true);
            if (!ui) return;
            Vector3 eye = root.TransformPoint(new Vector3(0, DrumixScaleRules.SeatedEyeHeight, 0));
            Vector3 to = ui.transform.position - eye; float dist = to.magnitude; if (dist < 0.1f) return;
            foreach (var h in Physics.RaycastAll(eye, to / dist, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (!geometry.Contains(h.collider)) continue;
                float d = Mathf.Max(1.1f, h.distance - 0.3f);
                if (d < dist) { ui.transform.position = eye + to / dist * d; report.AppendLine("  UI acercada a " + d.ToString("0.0") + " m (había una pared/puerta)."); }
                break;
            }
        }

        /// {radio, personas, ángulo}: 8+11+13+16 = 48 personas.
        internal static readonly float[][] ConcertRows = { new[] { 3.6f, 8f, 62f }, new[] { 4.7f, 11f, 66f }, new[] { 5.8f, 13f, 70f }, new[] { 6.9f, 16f, 74f } };

        /// Público en la escena real: delante de la batería, en el piso real (rayo hacia abajo), sin meterse en paredes/muebles,
        /// sin tapar la batería/UI/jugador, a ≥ 0.85 m entre sí y VISIBLE desde el asiento (nada en medio, p. ej. una puerta cerrada).
        /// Nadie a menos de 2.8 m del centro de la batería (render del garaje: a 1.9 m quedaban pegados a los platillos, dentro del garaje).
        const float MinCrowdDistance = 2.8f;

        static void PlaceCrowd(Transform root, GameObject kit, HashSet<Collider> geometry, Light crowdLight, List<Bounds> extraBlocked = null, float rMax = 12f, float minGap = 0.85f)
        {
            var npcs = root.GetComponentsInChildren<DrumixCrowdNPC>(true).ToList();
            if (npcs.Count == 0) return;
            Bounds kb; if (!DrumixOverlap.TryBounds(kit, out kb)) kb = new Bounds(kit.transform.position, Vector3.one);
            Vector3 kitC = kb.center, fwd = root.forward; fwd.y = 0; fwd.Normalize();
            Vector3 eye = root.TransformPoint(new Vector3(0, DrumixScaleRules.SeatedEyeHeight, 0));
            float surface = root.position.y;
            var blocked = new List<Bounds>();
            var kitPad = kb; kitPad.Expand(new Vector3(0.7f, 0, 0.7f)); blocked.Add(kitPad);
            if (extraBlocked != null) blocked.AddRange(extraBlocked);
            // ¿La batería quedó sobre una tarima/escenario? Entonces el público va ABAJO, en el piso (no al lado del baterista).
            float ground = surface; bool onStage = false;
            foreach (var h in Physics.RaycastAll(new Vector3(kitC.x, surface - 0.05f, kitC.z), Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (geometry.Contains(h.collider) && h.normal.y > 0.6f) { ground = h.point.y; break; }
            if (surface - ground > 0.4f) { onStage = true; report.AppendLine("  La batería está sobre un escenario de " + (surface - ground).ToString("0.00") + " m: el público va abajo, en el piso."); }
            blocked.Add(new Bounds(root.position + Vector3.up, new Vector3(1.4f, 2.2f, 1.4f))); // jugador
            var ui = root.GetComponentInChildren<DrumixUI>(true);
            if (ui)
            {
                var rts = ui.GetComponentsInChildren<RectTransform>(true);
                if (rts.Length > 0)
                {
                    var corners = new Vector3[4]; rts[0].GetWorldCorners(corners);
                    var ub = new Bounds(corners[0], Vector3.zero); foreach (var c in corners) ub.Encapsulate(c);
                    ub.Expand(new Vector3(0.4f, 0.2f, 0.4f)); blocked.Add(ub);
                }
            }
            var placed = new List<Vector3>();
            var done = new HashSet<DrumixCrowdNPC>();
            int ok = 0, hidden = 0; float maxR = 0;
            foreach (var pass in new[] { 75f, 115f })
            {
                var slots = DrumixLayout.CrowdSlots(kitC.x, kitC.z, fwd.x, fwd.z, Mathf.Max(Mathf.Max(kb.extents.x, kb.extents.z) + 0.9f, MinCrowdDistance), rMax, 0.45f, pass);
                int si = 0;
                foreach (var npc in npcs)
                {
                    if (done.Contains(npc)) continue;
                    for (; si < slots.Count && !done.Contains(npc); si++)
                    {
                        var s = slots[si];
                        float fy;
                        if (!GeometryHitDown(new Vector3(s[0], surface + 1.2f, s[1]), 3.5f, geometry, out fy)) continue; // sin piso
                        if (fy > surface + 0.6f) continue;
                        if (onStage && fy > surface - 0.4f) continue; // nada de público arriba del escenario
                        var p = new Vector3(s[0], fy, s[1]);
                        var body = new Bounds(p + Vector3.up * 0.95f, new Vector3(0.5f, 1.8f, 0.5f));
                        if (blocked.Any(b => b.Intersects(body))) continue;
                        if (placed.Any(q => (new Vector2(q.x - p.x, q.z - p.z)).magnitude < minGap)) continue;
                        if (GeometryOverlaps(body.center + Vector3.up * 0.05f, new Vector3(0.22f, 0.82f, 0.22f), geometry)) continue;
                        if (GeometryBlocks(eye, p + Vector3.up * 1.45f, geometry, 0.3f)) continue; // no se vería
                        Vector3 look = kitC - p; look.y = 0;
                        npc.transform.SetPositionAndRotation(p, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look.normalized, Vector3.up) : npc.transform.rotation);
                        npc.gameObject.SetActive(true);
                        placed.Add(p); ok++; maxR = Mathf.Max(maxR, s[3]); done.Add(npc);
                    }
                }
            }
            foreach (var npc in npcs)
            {
                if (done.Contains(npc)) continue;
                npc.gameObject.SetActive(false); hidden++; // no cabe: se apaga (no se borra)
            }
            if (crowdLight && placed.Count > 0)
            {
                Vector3 c = Vector3.zero; foreach (var p in placed) c += p; c /= placed.Count;
                crowdLight.transform.position = c + Vector3.up * 3f; crowdLight.range = Mathf.Max(8f, maxR + 3f);
            }
            report.AppendLine("  Público: " + ok + " persona(s) delante de la batería en el piso real (hasta " + maxR.ToString("0.0") + " m)" +
                              (hidden > 0 ? ", " + hidden + " apagada(s) porque no había lugar libre y visible (paredes/puerta/muebles)" : "") + ".");
        }

        /// Lo viejo del equipo que chocaría con DRUMIX (se apaga SOLO en Play): su batería, lo que tiene DrumPad /
        /// TargetZoneTrigger / ChangeMaterialOnGrab (sensores y baquetas) y sus canvas de instrucciones/UI.
        static List<GameObject> TeamThingsToHide(Scene scene, GameObject root, Transform teamKit, Transform rig)
        {
            var hide = new List<GameObject>();
            if (teamKit) hide.Add(teamKit.gameObject);
            string[] types = { "DrumPad", "TargetZoneTrigger", "ChangeMaterialOnGrab", "AutoInstructions" };
            foreach (var r in scene.GetRootGameObjects())
            {
                if (r == root || (rig && UnderAny(rig, r.transform)) || r.GetComponentInChildren<EventSystem>(true)) continue;
                if (r.name.IndexOf("Device Simulator", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("Permissions", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (r.GetComponentInChildren<Camera>(true) && r.GetComponentInChildren<Camera>(true).gameObject.activeInHierarchy) continue;
                bool team = r.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m && types.Contains(m.GetType().Name));
                bool canvas = r.GetComponentInChildren<Canvas>(true);
                bool managers = r.GetComponentsInChildren<MonoBehaviour>(true).Any(m => m && (m.GetType().Name == "LevelManager" || m.GetType().Name == "AudioManager" || m.GetType().Name == "BeatManager"));
                if ((team || canvas) && !managers && !hide.Contains(r)) hide.Add(r);
            }
            return hide;
        }
    }
}
