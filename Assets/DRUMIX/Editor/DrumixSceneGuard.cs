using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Drumix.EditorTools
{
    /// DRUMIX vive SOLO en los niveles (1.NivelUno, 2.NivelDos, 3.NivelTres) y en DRUMIX_Bar.
    ///  · Si un DRUMIX_ROOT GENERADO por DRUMIX (con DrumixRootInfo) aparece en otra escena (SampleScene, BasicScene,
    ///    BackUpNiveles, una copia...), se quita: al abrir Unity se limpia el archivo y se guarda; al abrir esa escena a mano
    ///    se quita y queda marcada para guardar. Un objeto que alguien llamó igual a mano NO se toca.
    ///  · Al dar Play en una escena que no es un nivel DRUMIX, avisa en la Console cuál abrir.
    [InitializeOnLoad]
    public static class DrumixSceneGuard
    {
        static DrumixSceneGuard()
        {
            EditorSceneManager.sceneOpened -= Opened; EditorSceneManager.sceneOpened += Opened;
            EditorApplication.playModeStateChanged -= Play; EditorApplication.playModeStateChanged += Play;
        }

        public static HashSet<string> Targets()
        {
            var set = new HashSet<string>(DrumixBuilder.FindTeamLevelScenes());
            set.Add(DrumixBuilder.ScenePath);
            return set;
        }

        static bool Skip(string path)
        {
            return string.IsNullOrEmpty(path) || path.Contains("/Samples/") || path.Contains("/_Recovery/") || !path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        /// Quita los DRUMIX_ROOT generados de una escena cargada que NO es destino. Devuelve cuántos quitó.
        static int RemoveStray(Scene scene, HashSet<string> targets)
        {
            if (!scene.IsValid() || !scene.isLoaded || Skip(scene.path) || targets.Contains(scene.path)) return 0;
            int n = 0;
            foreach (var r in scene.GetRootGameObjects())
                if (r && r.name == DrumixBuilder.RootName && r.GetComponent<DrumixRootInfo>()) { UnityEngine.Object.DestroyImmediate(r); n++; }
            return n;
        }

        static void Opened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                int n = RemoveStray(scene, Targets());
                if (n == 0) return;
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.LogWarning("[DRUMIX] '" + scene.name + "' no es un nivel DRUMIX: le quité " + n + " DRUMIX_ROOT que DRUMIX había puesto ahí. Guarda (Ctrl+S). Los niveles son 1.NivelUno, 2.NivelDos y 3.NivelTres.");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Play(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.ExitingEditMode) return;
            try
            {
                var scene = SceneManager.GetActiveScene();
                var levels = DrumixBuilder.FindTeamLevelScenes();
                if (levels.Count == 0 || Targets().Contains(scene.path)) return;
                Debug.LogWarning("[DRUMIX] Estás dando Play en '" + scene.name + "', que NO es un nivel DRUMIX (es una escena de prueba del equipo y DRUMIX no la toca: " +
                                 "ahí no hay público, batería ni UI de DRUMIX). Abre " + levels[0] + " (menú DRUMIX > Abrir Nivel 1).");
            }
            catch (Exception) { }
        }

        /// Revisa los .unity del proyecto (texto) y limpia los que tengan un DRUMIX_ROOT generado fuera de los niveles.
        /// No toca escenas abiertas con cambios sin guardar. Devuelve cuántas escenas limpió.
        public static int CleanProjectScenes()
        {
            var targets = Targets();
            int cleaned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Skip(path) || targets.Contains(path) || !File.Exists(path)) continue;
                string text;
                try { text = File.ReadAllText(path); } catch (Exception) { continue; }
                if (!DrumixLevelData.HasGeneratedRootInSceneText(text, DrumixBuilder.RootName)) continue;
                var loaded = SceneManager.GetSceneByPath(path);
                bool wasLoaded = loaded.IsValid() && loaded.isLoaded;
                if (wasLoaded && loaded.isDirty) { Debug.LogWarning("[DRUMIX] " + path + " tiene un DRUMIX_ROOT que no va ahí, pero tiene cambios sin guardar: guarda y vuelve a abrir Unity."); continue; }
                var scene = wasLoaded ? loaded : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                int n = RemoveStray(scene, targets);
                if (n > 0 && EditorSceneManager.SaveScene(scene)) { cleaned++; Debug.Log("[DRUMIX] Limpié " + path + ": tenía " + n + " DRUMIX_ROOT (DRUMIX solo va en los niveles). Haz commit de ese cambio."); }
                if (!wasLoaded && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
            }
            return cleaned;
        }

        [MenuItem("DRUMIX/Abrir Nivel 1 (1.NivelUno)", false, 39)]
        static void OpenLevel1()
        {
            var levels = DrumixBuilder.FindTeamLevelScenes();
            string path = levels.Count > 0 ? levels[0] : DrumixBuilder.ScenePath;
            if (!File.Exists(path)) { EditorUtility.DisplayDialog("DRUMIX", "No encontré " + path + ".", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        [MenuItem("DRUMIX/Quitar DRUMIX de escenas que no son niveles", false, 31)]
        static void CleanMenu()
        {
            int n = CleanProjectScenes();
            EditorUtility.DisplayDialog("DRUMIX", n > 0 ? "Limpié " + n + " escena(s) (detalle en la Console)." : "Ninguna escena fuera de los niveles tiene DRUMIX. Todo bien.", "OK");
        }
    }
}
