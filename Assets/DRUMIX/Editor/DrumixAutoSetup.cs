using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Drumix.EditorTools
{
    /// Red de seguridad "cero clicks": al abrir Unity, si alguna escena de nivel del equipo (o DRUMIX_Bar) no tiene DRUMIX_ROOT
    /// armado con la versión actual, corre ARMAR TODO UNA vez solo. Si ya está armado (lo normal después de un Pull), no hace nada.
    /// Lee los .unity como texto (no abre escenas) para decidir. Nunca corre en Play, compilando, en modo batch ni con escenas sin guardar.
    [InitializeOnLoad]
    public static class DrumixAutoSetup
    {
        // Con la versión en la clave: si llegan scripts nuevos con Unity abierto (Cursor/Pull), se vuelve a revisar sin reiniciar Unity.
        static readonly string SessionKey = "DRUMIX_AutoSetup_Hecho_v" + DrumixBuilder.BuildVersion;
        const string PrefOff = "DRUMIX_AutoArmarApagado";
        const string MenuOff = "DRUMIX/Opciones/Armar solo al abrir Unity";
        const string StandaloneMarker = "Assets/DRUMIX_Proyecto/PROYECTO_STANDALONE.txt";
        const string StandaloneState = "ProjectSettings/DRUMIX_SetupEstado.txt";
        static double startAt;

        static DrumixAutoSetup()
        {
            if (Application.isBatchMode || SessionState.GetBool(SessionKey, false) || EditorPrefs.GetBool(PrefOff, false)) return;
            startAt = EditorApplication.timeSinceStartup + 5.0; // que termine de importar
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem(MenuOff, false, 62)] static void Toggle() { EditorPrefs.SetBool(PrefOff, !EditorPrefs.GetBool(PrefOff, false)); }
        [MenuItem(MenuOff, true)] static bool ToggleCheck() { Menu.SetChecked(MenuOff, !EditorPrefs.GetBool(PrefOff, false)); return true; }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < startAt) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Tick;
            SessionState.SetBool(SessionKey, true);
            // El proyecto standalone (DRUMIX_Proyecto*) tiene su propia configuración inicial: que termine ella primero.
            if (File.Exists(StandaloneMarker) && (!File.Exists(StandaloneState) || !File.ReadAllText(StandaloneState).Trim().StartsWith("3", StringComparison.Ordinal))) return;
            bool dirty = false;
            for (int i = 0; i < EditorSceneManager.sceneCount; i++) if (EditorSceneManager.GetSceneAt(i).isDirty) dirty = true;
            // DRUMIX solo va en los niveles: si quedó un DRUMIX_ROOT generado en SampleScene/BasicScene/BackUpNiveles, se quita.
            if (!dirty) { try { DrumixSceneGuard.CleanProjectScenes(); } catch (Exception e) { Debug.LogException(e); } }
            string why = WhatIsMissing();
            if (why == null)
            {
                var active = EditorSceneManager.GetActiveScene();
                var levels = DrumixBuilder.FindTeamLevelScenes();
                // Unity abre la última escena usada (a veces SampleScene, que es de prueba del equipo y no tiene DRUMIX):
                // si no hay cambios sin guardar, se abre el Nivel 1 para que Play muestre el juego.
                if (levels.Count > 0 && !DrumixSceneGuard.Targets().Contains(active.path))
                {
                    if (!dirty) { EditorSceneManager.OpenScene(levels[0], OpenSceneMode.Single); Debug.Log("[DRUMIX] Abrí " + levels[0] + " (estaba '" + active.name + "', que no es un nivel DRUMIX)."); }
                    else Debug.Log("[DRUMIX] Tienes abierta '" + active.name + "', que no es un nivel DRUMIX. Para jugar abre " + levels[0] + " (DRUMIX > Abrir Nivel 1).");
                }
                return;
            }
            if (dirty)
            {
                Debug.LogWarning("[DRUMIX] " + why + ", pero tienes cambios sin guardar: guarda y usa DRUMIX > ARMAR TODO (1 click).");
                return;
            }
            Debug.Log("[DRUMIX] " + why + ": armando solo (una vez)...");
            try
            {
                if (DrumixBuilder.BuildAll(false))
                    EditorUtility.DisplayDialog("DRUMIX listo", "Se armó solo: público, batería y UI en las escenas de los niveles.\n\nDale Play. PC: Enter y A S D F G H J K.\n\nAcuérdate: cerrar Unity, commit y Push para que el equipo no tenga que armar nada.", "¡Dale!");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[DRUMIX] No se pudo armar solo. Usa DRUMIX > ARMAR TODO (1 click) y pega este error en el chat.");
            }
        }

        /// null = todo armado con la versión actual.
        public static string WhatIsMissing()
        {
            var scenes = DrumixBuilder.FindTeamLevelScenes();
            if (scenes.Count == 0) scenes.Add(DrumixBuilder.ScenePath);
            foreach (var path in scenes)
            {
                if (!File.Exists(path)) return Path.GetFileNameWithoutExtension(path) + " no existe";
                int v = BuiltVersion(File.ReadAllText(path));
                if (v < 0) return Path.GetFileNameWithoutExtension(path) + " no tiene DRUMIX";
                if (v < DrumixBuilder.BuildVersion) return Path.GetFileNameWithoutExtension(path) + " tiene DRUMIX viejo (v" + v + ")";
            }
            return null;
        }

        /// Versión guardada en DrumixRootInfo dentro del .unity (texto YAML). -1 si no hay DRUMIX_ROOT.
        public static int BuiltVersion(string sceneYaml) { return DrumixLevelData.BuiltVersionInSceneText(sceneYaml, DrumixBuilder.RootName); }
    }
}
