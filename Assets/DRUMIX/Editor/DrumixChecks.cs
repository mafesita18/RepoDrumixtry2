using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Drumix.EditorTools
{
    /// DRUMIX > Revisar (autoprueba): valida la escena armada y reporta problemas en español.
    public static class DrumixChecks
    {
        static int ok, warn, err;
        static StringBuilder log;

        [MenuItem("DRUMIX/Revisar (autoprueba)", false, 10)]
        public static void Run()
        {
            ok = warn = err = 0; log = new StringBuilder();
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != DrumixBuilder.ScenePath)
                Warn("La escena abierta no es " + DrumixBuilder.ScenePath + " (se revisa igual la escena abierta).");

            var roots = scene.GetRootGameObjects();
            int rootCount = roots.Count(r => r.name == DrumixBuilder.RootName);
            Check(rootCount == 1, "Hay exactamente 1 DRUMIX_ROOT", rootCount == 0 ? "No hay DRUMIX_ROOT: usa DRUMIX > ARMAR TODO" : "Hay " + rootCount + " DRUMIX_ROOT (duplicados): vuelve a ARMAR TODO");

            // Juego / eventos
            var games = Find<DrumixGame>();
            Check(games.Length == 1, "1 DrumixGame (secuencia + combo + puntaje)", "DrumixGame: hay " + games.Length);
            Check(Find<DrumixInput>().Length >= 1, "Controles de prueba (teclado + botones Quest)", "Falta DrumixInput");
            if (games.Length == 1) Check(games[0].passPercent > 0 && games[0].passPercent <= 100 && games[0].goodWindow > games[0].perfectWindow,
                "Reglas de puntaje coherentes (" + games[0].passPercent + "% para pasar)", "Ventanas/porcentaje raros en DrumixGame");
            if (games.Length == 1)
            {
                var lv = games[0].levels ?? new DrumixLevel[0];
                bool lvOk = lv.Length >= 1 && lv.All(l => l && l.spec != null && l.spec.goodWindow >= l.spec.perfectWindow && l.spec.bpm > 0 && l.spec.bars > 0);
                Check(lvOk, lv.Length + " niveles asignados y válidos (" + string.Join(", ", lv.Where(l => l && l.spec != null).Select(l => l.spec.name).ToArray()) + ")",
                    "Niveles faltantes o vacíos en DrumixGame → DRUMIX > ARMAR TODO los crea en " + DrumixBuilder.ConfigDir + "/Niveles");
            }
            var fxs = Find<DrumixFX>();
            Check(fxs.Length == 1 && fxs[0].particleMaterial, "Partículas listas (material generado)", "Partículas sin material o duplicadas → ARMAR TODO");
            Check(Find<DrumixRootInfo>().Length == 1, "ARMAR TODO no destructivo: DRUMIX_ROOT sabe qué generó", "Falta DrumixRootInfo en DRUMIX_ROOT → ARMAR TODO");

            // Batería
            var pads = Find<DrumixPad>();
            var lanes = new HashSet<DrumixLane>(pads.Select(p => p.lane));
            Check(pads.Length >= 1, pads.Length + " piezas tocables", "No hay DrumixPad");
            foreach (DrumixLane lane in Enum.GetValues(typeof(DrumixLane)))
                if (!lanes.Contains(lane)) Warn("Falta la pieza " + DrumixEvents.LaneName(lane) + " (la secuencia la reemplaza por caja)");
            var dup = pads.GroupBy(p => p.lane).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToArray();
            Check(dup.Length == 0, "Sin piezas repetidas", "Piezas repetidas: " + string.Join(", ", dup));
            Camera cam = Camera.main;
            foreach (var pad in pads)
            {
                string n = DrumixEvents.LaneName(pad.lane);
                var cols = pad.GetComponents<Collider>().Where(c => c.enabled).ToArray();
                Check(cols.Length > 0 && cols.All(c => c.isTrigger), n + ": collider trigger", n + ": sin collider trigger activo");
                var body = pad.GetComponent<Rigidbody>();
                Check(!body || (body.isKinematic && !body.useGravity), n + ": Rigidbody cinemático sin gravedad", n + ": Rigidbody con física (se caería/movería)");
                Check(!pad.GetComponentsInChildren<MonoBehaviour>(true).Any(b => b && b.enabled && DrumixUtil.IsInteractable(b.GetType())),
                    n + ": sin Grab Interactable activo", n + ": tiene Grab Interactable ACTIVO (se puede mover al centro)");
                Check(pad.gameObject.activeInHierarchy, n + ": activo", n + ": DESACTIVADO");
                bool zone = pad.name.StartsWith("Zona_", StringComparison.Ordinal); // zona invisible sobre la batería incluida
                var visibleRoot = pad.transform.parent && (zone || pad.transform.parent.name.StartsWith("Pieza_", StringComparison.Ordinal)) ? pad.transform.parent : pad.transform;
                var rends = visibleRoot.GetComponentsInChildren<Renderer>(true).Where(r => r != pad.cueRenderer && r.enabled && r.gameObject.activeInHierarchy).ToArray();
                Check(rends.Length > 0, n + ": visible (" + rends.Length + " renderers)", n + ": NO tiene nada visible");
                if (rends.Length > 0)
                {
                    var b = DrumixUtil.RendererBounds(visibleRoot.gameObject);
                    Check(b.size.magnitude > 0.05f && b.size.magnitude < (zone ? 8f : 5f), n + ": tamaño razonable", n + ": tamaño raro (" + b.size + ")");
                    if (cam) Check(rends.All(r => (cam.cullingMask & (1 << r.gameObject.layer)) != 0), n + ": en capa que la cámara dibuja", n + ": en capa que la cámara NO dibuja");
                }
                if (cam) Check(!IsUnderRig(pad.transform),
                    n + ": no cuelga del jugador", n + ": cuelga del XR Origin/cámara (se movería con el jugador)");
                if (!pad.clip) Warn(n + ": sin AudioClip (usará sonido procedural)");
                if (DrumixUtil.HasTeamDrumPad(pad.gameObject)) Info(n + ": convive con DrumPad del equipo (sin doble sonido: muteIfTeamDrumPad=" + pad.muteIfTeamDrumPad + ")");
            }
            Check(Find<DrumixKitLock>().Length >= 1, "Kit bloqueado (DrumixKitLock)", "Falta DrumixKitLock en la raíz de la batería");

            // Baquetas
            var sticks = Find<DrumixStick>();
            Check(sticks.Length >= 2, sticks.Length + " baquetas", "Solo " + sticks.Length + " baqueta(s)");
            foreach (var s in sticks)
            {
                var c = s.GetComponent<Collider>(); var rb = s.GetComponent<Rigidbody>();
                Check(c && c.isTrigger && rb && rb.isKinematic, "Baqueta " + s.hand + ": trigger + cinemática", "Baqueta " + s.hand + ": collider/rigidbody mal");
            }

            // Cámara
            var cams = Find<Camera>().Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
            Check(cam != null, "Hay Main Camera", "No hay cámara con tag MainCamera");
            if (cams.Length > 1) Warn("Hay " + cams.Length + " cámaras activas: " + string.Join(", ", cams.Select(c => c.name).ToArray()));
            int listeners = Find<AudioListener>().Count(l => l.enabled && l.gameObject.activeInHierarchy);
            Check(listeners == 1, "1 AudioListener", "AudioListeners activos: " + listeners);

            // Público
            var npcs = Find<DrumixCrowdNPC>();
            Check(npcs.Length > 0, npcs.Length + " NPCs de público", "No hay público (DrumixCrowdNPC)");
            int hidden = 0, culled = 0, behind = 0, lod = 0;
            foreach (var npc in npcs)
            {
                var rs = npc.GetComponentsInChildren<Renderer>(true);
                if (!npc.gameObject.activeInHierarchy || rs.Length == 0 || rs.All(r => !r.enabled)) hidden++;
                if (cam && rs.Any(r => (cam.cullingMask & (1 << r.gameObject.layer)) == 0)) culled++;
                if (npc.transform.position.z < 0.5f) behind++;
                if (npc.GetComponentsInChildren<LODGroup>(true).Any(l => l.enabled)) lod++;
            }
            Check(hidden == 0, "Todo el público es visible", hidden + " NPC(s) sin renderers visibles");
            Check(culled == 0, "Público en capas visibles", culled + " NPC(s) en capas que la cámara no dibuja");
            Check(behind == 0, "Público delante del jugador", behind + " NPC(s) detrás del jugador");
            if (lod > 0) Warn(lod + " NPC(s) con LODGroup activo (podrían desaparecer de lejos)");

            // Lanzamientos
            var dirs = Find<DrumixThrowDirector>();
            Check(dirs.Length == 1, "1 director de lanzamientos", "Directores: " + dirs.Length);
            if (dirs.Length == 1)
            {
                var d = dirs[0];
                Check(d.maxActive <= d.poolSize && d.maxActive <= 10, "Límite activos " + d.maxActive + " ≤ pool " + d.poolSize, "maxActive/poolSize raros");
                Check(d.cooldown >= 1f, "Cooldown " + d.cooldown + " s", "Cooldown muy bajo: lanzarían todo el rato");
                Check(d.randomInterval.x >= 8f, "Random ocasional cada " + d.randomInterval.x + "-" + d.randomInterval.y + " s", "Random demasiado seguido");
                Check(d.maxPerSong > 0 && d.maxPerSong <= 20, "Máx " + d.maxPerSong + " por canción", "maxPerSong raro");
                bool templatesOk = d.templates != null && d.templates.Length > 0 && d.templates.All(t => t);
                Check(templatesOk, (d.templates == null ? 0 : d.templates.Length) + " objetos lanzables", "Sin plantillas (lanzará esferas)");
                if (templatesOk)
                    foreach (var t in d.templates)
                        Check(!t.GetComponentsInChildren<MeshCollider>(true).Any(m => m.enabled && !m.convex), t.name + ": colliders válidos", t.name + ": MeshCollider cóncavo (error de física)");
            }
            RulesSelfTest();

            // Público creado y visible (medido por la malla real: ni gigantes, ni enanos, ni apagados)
            {
                var crowdNpcs = Find<DrumixCrowdNPC>();
                int visible = 0; string bad = "";
                foreach (var n in crowdNpcs)
                {
                    bool rendering = false;
                    foreach (var r in n.GetComponentsInChildren<Renderer>(false)) if (r.enabled && !r.forceRenderingOff) { rendering = true; break; }
                    var b = DrumixUtil.MeasureBounds(n.gameObject, n.transform);
                    bool sized = b.size.y >= 1.3f && b.size.y <= 2.2f && Mathf.Abs(b.min.y) < 0.3f;
                    if (n.gameObject.activeInHierarchy && rendering && sized) visible++;
                    else if (bad.Length < 160) bad += n.name + (rendering ? "" : " (sin renderer activo)") + (sized ? "" : " (" + b.size.y.ToString("0.00") + " m)") + "; ";
                }
                Check(crowdNpcs.Length >= 8 && visible == crowdNpcs.Length, "Público creado y visible: " + visible + " personas de 1.3–2.2 m con malla activa",
                      "Público: " + crowdNpcs.Length + " NPC, " + visible + " visibles bien. " + (crowdNpcs.Length == 0 ? "No hay público: corre ARMAR TODO." : bad));
            }
            // UI
            Check(Find<DrumixUI>().Length == 1, "1 UI DRUMIX", "UIs DRUMIX: " + Find<DrumixUI>().Length);
            foreach (var ui in Find<DrumixUI>())
            {
                var canvas = ui.transform.Find("DRUMIX_Canvas");
                var noScreens = new System.Collections.Generic.List<string>();
                foreach (var screen in new[] { "Menu", "Niveles", "Ajustes", "Calibracion", "HUD", "Cuenta", "Pausa", "Resultados" })
                    if (!canvas || !canvas.Find(screen)) noScreens.Add(screen);
                Check(noScreens.Count == 0, "UI: menú, niveles, ajustes, calibración, HUD, cuenta, pausa y resultados", "UI sin pantallas: " + string.Join(", ", noScreens) + " (corre ARMAR TODO)");
            }
            var es = Find<EventSystem>();
            Check(es.Length == 1, "1 EventSystem", "EventSystems: " + es.Length);
            if (es.Length == 1) Check(es[0].GetComponent<BaseInputModule>() != null, "EventSystem con input module", "EventSystem sin input module (los botones no responden)");

            // Nada superpuesto: geometría, z-fighting y materiales
            var drumixRoot = roots.FirstOrDefault(r => r.name == DrumixBuilder.RootName);
            if (drumixRoot)
            {
                Vector3 origin = Vector3.zero;
                var rig = drumixRoot.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("XR Origin", StringComparison.Ordinal));
                if (rig) origin = rig.position; else if (cam) origin = new Vector3(cam.transform.position.x, 0, cam.transform.position.z);
                var items = DrumixOverlap.Collect(drumixRoot, origin);
                var hits = DrumixOverlap.FindOverlaps(items);
                Check(hits.Count == 0, "Nada metido dentro de otra cosa (" + items.Count + " objetos: batería, muebles, público, jugador, UI)",
                      hits.Count + " superposición(es): " + string.Join("; ", hits.Take(5).Select(h => h.Key + " / " + h.Value).ToArray()) + " -> vuelve a ARMAR TODO");
                var npcPoints = items.Where(i => i.kind == DrumixOverlap.Kind.Npc).Select(i => new[] { i.box.CenterX, i.box.CenterZ }).ToList();
                var close = DrumixLayout.TooClose(npcPoints, DrumixLayout.NpcMinSpacing - 0.05f);
                Check(close.Count == 0, "Público separado (≥ " + DrumixLayout.NpcMinSpacing + " m entre personas)", close.Count + " par(es) de NPCs demasiado juntos");
                var dupR = DrumixOverlap.DuplicateRenderers(drumixRoot);
                Check(dupR.Count == 0, "Sin mallas duplicadas en el mismo lugar (z-fighting)", dupR.Count + " malla(s) duplicada(s): " + string.Join("; ", dupR.Take(4).ToArray()));
                var flat = DrumixOverlap.CoplanarSurfaces(drumixRoot);
                Check(flat.Count == 0, "Pisos/tarimas a alturas distintas (sin z-fighting)", "Superficies a la misma altura: " + string.Join("; ", flat.Take(4).ToArray()));
                var extraNames = new List<string>();
                int extra = DrumixOverlap.ExtraMaterialRenderers(drumixRoot, extraNames);
                Check(extra == 0, "Sin materiales superpuestos (materiales = submeshes)", extra + " renderer(s) con más materiales que submeshes: " + string.Join(", ", extraNames.ToArray()));
                var pinkNames = new List<string>();
                int pink = DrumixOverlap.PinkRenderers(drumixRoot, pinkNames);
                Check(pink == 0, "Sin materiales rosados/vacíos", pink + " renderer(s) con material roto: " + string.Join(", ", pinkNames.ToArray()));
            }

            // Scripts faltantes en la escena
            int missing = 0;
            foreach (var r in roots) foreach (var t in r.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            Check(missing == 0, "Sin scripts faltantes", missing + " componente(s) con script faltante");

            // Duplicados viejos / equipo
            var old = DrumixCleanup.FindOldFiles();
            Check(old.Count == 0, "Sin scripts viejos de nuestros zips", old.Count + " archivo(s) viejos: usa DRUMIX > Limpiar paquete viejo");
            foreach (var n in new[] { "DrumPad", "AudioManager", "LevelManager", "DifficultyManager", "ChangeMaterialOnGrab" })
                if (DrumixUtil.FindType(n) != null) Info("Script del equipo presente y respetado: " + n);
            if (!EditorBuildSettings.scenes.Any(s => s.path == DrumixBuilder.ScenePath))
                Info("La escena DRUMIX_Bar no está en Build Settings (agrégala tú cuando vayan a compilar para Quest; no la toco para no chocar con ProjectSettings del equipo).");

            string title = err == 0 ? (warn == 0 ? "TODO BIEN" : "Bien, con avisos") : "HAY PROBLEMAS";
            string summary = title + ": " + ok + " OK · " + warn + " avisos · " + err + " errores\n\n" + log;
            if (err > 0) Debug.LogError("[DRUMIX] Autoprueba\n" + summary); else if (warn > 0) Debug.LogWarning("[DRUMIX] Autoprueba\n" + summary); else Debug.Log("[DRUMIX] Autoprueba\n" + summary);
            string shortLog = string.Join("\n", log.ToString().Split('\n').Where(l => l.StartsWith("✗") || l.StartsWith("!")).Take(15).ToArray());
            EditorUtility.DisplayDialog("DRUMIX · Revisar", title + "\n" + ok + " OK · " + warn + " avisos · " + err + " errores\n\n" + (shortLog.Length > 0 ? shortLog : "Nada que arreglar.") + "\n\n(Detalle en la Console)", "OK");
        }

        /// Solo lectura: busca en prefabs/modelos del EQUIPO renderers con más materiales que submeshes
        /// (material superpuesto) o materiales rotos. No modifica nada; DRUMIX corrige solo sus propias instancias.
        [MenuItem("DRUMIX/Revisar materiales del proyecto (solo lectura)", false, 11)]
        public static void ScanProjectMaterials()
        {
            var sb = new StringBuilder(); int files = 0, bad = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith("Assets/DRUMIX/", StringComparison.Ordinal)) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;
                files++;
                var extra = new List<string>(); var pink = new List<string>();
                int e = DrumixOverlap.ExtraMaterialRenderers(go, extra);
                int p = DrumixOverlap.PinkRenderers(go, pink);
                if (e + p == 0) continue;
                bad++;
                sb.AppendLine("· " + path + (e > 0 ? "  materiales sobrantes: " + string.Join(", ", extra.ToArray()) : "") +
                              (p > 0 ? "  material roto/vacío: " + string.Join(", ", pink.ToArray()) : ""));
            }
            string msg = files + " prefabs/modelos revisados, " + bad + " con avisos (no se modificó nada; en la escena DRUMIX se corrigen solos).";
            Debug.Log("[DRUMIX] Materiales del proyecto: " + msg + "\n" + sb);
            EditorUtility.DisplayDialog("DRUMIX · Materiales", msg + "\n\n(Detalle en la Console)", "OK");
        }

        static bool IsUnderRig(Transform t)
        {
            for (; t; t = t.parent) if (t.GetComponent<Camera>() || t.name.StartsWith("XR Origin", StringComparison.Ordinal)) return true;
            return false;
        }

        static T[] Find<T>() where T : UnityEngine.Object { return DrumixUtil.FindAll<T>(); }
        static void Check(bool pass, string good, string bad) { if (pass) { ok++; log.AppendLine("✓ " + good); } else { err++; log.AppendLine("✗ " + bad); } }
        static void Warn(string msg) { warn++; log.AppendLine("! " + msg); }
        static void Info(string msg) { log.AppendLine("· " + msg); }

        /// Mismas reglas puras que usa el runtime (también se corren fuera de Unity en Tests).
        static void RulesSelfTest()
        {
            int fails = 0;
            Action<bool> t = c => { if (!c) fails++; };
            t(!DrumixThrowRules.CanThrow(false, 0, 6, 0, 10, 99, 3));     // no fuera de Play
            t(DrumixThrowRules.CanThrow(true, 0, 6, 0, 10, 99, 3));
            t(!DrumixThrowRules.CanThrow(true, 6, 6, 0, 10, 99, 3));      // tope activos
            t(!DrumixThrowRules.CanThrow(true, 0, 6, 10, 10, 99, 3));     // tope por canción
            t(!DrumixThrowRules.CanThrow(true, 0, 6, 0, 10, 1, 3));       // cooldown
            t(!DrumixThrowRules.IsMissMoment(2, 3) && DrumixThrowRules.IsMissMoment(3, 3) && DrumixThrowRules.IsMissMoment(6, 3));
            float i0 = DrumixThrowRules.Interval(15, 30, 0), i1 = DrumixThrowRules.Interval(15, 30, 1);
            t(Mathf.Approximately(i0, 15) && Mathf.Approximately(i1, 30));
            Check(fails == 0, "Reglas de lanzamiento (7 pruebas)", fails + " prueba(s) de reglas fallaron");
        }
    }
}
