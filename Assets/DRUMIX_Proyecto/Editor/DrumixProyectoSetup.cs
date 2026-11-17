using System;
using System.IO;
using Drumix.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Drumix.Proyecto
{
    /// SOLO en los proyectos DRUMIX_Proyecto / DRUMIX_Proyecto_Completo (no va al repo del equipo).
    /// Solo arregla lo que FALTA: si ya hay URP, XR Origin, XR configurado, etc., no lo toca.
    /// Al abrir el proyecto por primera vez, sin clicks:
    ///   1) revisa URP, espacio de color, Input System, ajustes de Quest (Android) y XR (OpenXR + Meta Quest);
    ///   2) importa XRI > Starter Assets (XR Origin listo para VR) si no hay uno;
    ///   3) arma la escena DRUMIX_Bar (DRUMIX > ARMAR TODO), la agrega a Build Settings y la abre.
    /// Se hace UNA vez (estado en ProjectSettings/DRUMIX_SetupEstado.txt). Repetir: DRUMIX > Proyecto > Repetir configuración inicial.
    [InitializeOnLoad]
    public static class DrumixProyectoSetup
    {
        const string Marker = "Assets/DRUMIX_Proyecto/PROYECTO_STANDALONE.txt";
        const string StateFile = "ProjectSettings/DRUMIX_SetupEstado.txt";
        const string XriPackage = "com.unity.xr.interaction.toolkit";
        // URP: primero los del EQUIPO (Settings/Project Configuration: Performance / Quality URP Config), luego el de XRI-Examples.
        static readonly string[] UrpGuids = { "fd42a132493b1f143a4963adcd530ef1", "cf6a652e94858004a93399f0a3dac507", "19ba41d7c0026c3459d37c2fe90c55a0" };
        const int StepConfig = 0, StepSample = 1, StepBuild = 2, StepDone = 3;
        const int MaxTries = 3;

        static double startAt;
        static bool running;

        static DrumixProyectoSetup()
        {
            if (!File.Exists(Marker)) return;
            if (ReadStep() >= StepDone) return;
            startAt = EditorApplication.timeSinceStartup + 4.0; // dejar que termine de importar
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("DRUMIX/Proyecto/Repetir configuración inicial", false, 80)]
        static void Repeat()
        {
            WriteStep(StepConfig, 0);
            startAt = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("DRUMIX/Proyecto/Cambiar a Quest (Android)", false, 81)]
        static void SwitchToAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                EditorUtility.DisplayDialog("DRUMIX", "Falta el módulo Android en Unity Hub:\nInstalls > 6000.5.5f1 > ⚙ > Add modules > Android Build Support (con OpenJDK y SDK/NDK).", "OK");
                return;
            }
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) { Debug.Log("[DRUMIX] Ya estás en Android (Quest)."); return; }
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
        }

        static void Tick()
        {
            if (running || EditorApplication.timeSinceStartup < startAt) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            running = true;
            try { Advance(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("[DRUMIX] La configuración automática falló. Usa DRUMIX > ARMAR TODO (1 click) y pega este error en el chat.");
                EditorApplication.update -= Tick;
            }
            finally { running = false; }
        }

        static void Advance()
        {
            int tries;
            int step = ReadStep(out tries);
            if (tries >= MaxTries)
            {
                Debug.LogWarning("[DRUMIX] La configuración automática se intentó " + tries + " veces. Termina a mano: DRUMIX > ARMAR TODO (1 click).");
                WriteStep(StepDone, 0); EditorApplication.update -= Tick; return;
            }
            switch (step)
            {
                case StepConfig:
                    Configure();
                    WriteStep(StepSample, 0);
                    startAt = EditorApplication.timeSinceStartup + 0.5;
                    break;
                case StepSample:
                    WriteStep(StepBuild, 0);
                    if (ImportStarterAssets()) startAt = EditorApplication.timeSinceStartup + 5.0; // recompila; seguimos después
                    break;
                case StepBuild:
                    WriteStep(StepBuild, tries + 1);
                    Build();
                    WriteStep(StepDone, 0);
                    EditorApplication.update -= Tick;
                    break;
                default:
                    EditorApplication.update -= Tick; break;
            }
        }

        // ---------- 1) ajustes ----------
        static void Configure()
        {
            var log = new System.Text.StringBuilder("[DRUMIX] Configuración del proyecto:\n");

            // URP
            if (!GraphicsSettings.defaultRenderPipeline)
            {
                RenderPipelineAsset urp = null; string path = null;
                foreach (var guid in UrpGuids)
                {
                    path = AssetDatabase.GUIDToAssetPath(guid);
                    urp = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                    if (urp) break;
                }
                if (urp) { GraphicsSettings.defaultRenderPipeline = urp; log.AppendLine("  URP asignado en Graphics: " + path); }
                else log.AppendLine("  OJO: no encontré un asset URP conocido: Project Settings > Graphics.");
            }
            else log.AppendLine("  URP OK: " + GraphicsSettings.defaultRenderPipeline.name);
            int levels = QualitySettings.names.Length;
            log.AppendLine("  Calidad: " + levels + " niveles (Android = Medium).");

            // Color lineal, nombre, Quest (Android)
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany") PlayerSettings.companyName = "DRUMIX";
            if (string.IsNullOrEmpty(PlayerSettings.productName)) PlayerSettings.productName = "DRUMIX";
            if ((int)PlayerSettings.Android.minSdkVersion < 32) PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)32;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            log.AppendLine("  Color Lineal, Android min API " + (int)PlayerSettings.Android.minSdkVersion + ", ARM64, ASTC, IL2CPP (en ProjectSettings).");

            // Input: Both (2) o New (1)
            int handler = ActiveInputHandler();
            log.AppendLine("  Active Input Handling: " + (handler == 2 ? "Both" : handler == 1 ? "Input System (New)" : handler < 0 ? "?" : "Old"));
            if (handler == 0)
            {
                SetActiveInputHandler(2);
                log.AppendLine("  -> cambiado a Both. Unity pedirá reiniciar: acepta.");
            }

            // XR Plug-in Management (OpenXR + Meta Quest en Android, OpenXR en PC para Quest Link)
            UnityEngine.Object xr;
            bool hasXr = EditorBuildSettings.TryGetConfigObject("com.unity.xr.management.loader_settings", out xr) && xr;
            log.AppendLine(hasXr ? "  XR Plug-in Management: OpenXR en Android (Meta Quest Support + Touch) y PC." : "  OJO: sin XR Plug-in Management configurado (Project Settings > XR Plug-in Management > OpenXR).");

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static SerializedObject PlayerSettingsObject()
        {
            var all = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            return all != null && all.Length > 0 && all[0] ? new SerializedObject(all[0]) : null;
        }
        static int ActiveInputHandler()
        {
            var so = PlayerSettingsObject();
            var prop = so != null ? so.FindProperty("activeInputHandler") : null;
            return prop != null ? prop.intValue : -1;
        }
        static void SetActiveInputHandler(int value)
        {
            var so = PlayerSettingsObject();
            var prop = so != null ? so.FindProperty("activeInputHandler") : null;
            if (prop == null) return;
            prop.intValue = value; so.ApplyModifiedProperties();
            if (EditorUtility.DisplayDialog("DRUMIX", "Hay que reiniciar Unity para activar el Input System nuevo.", "Reiniciar", "Luego"))
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
        }

        // ---------- 2) XR Origin (XRI Starter Assets) ----------
        static bool HasXROrigin()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab XR Origin", new[] { "Assets" }))
            {
                string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                if (file.StartsWith("XR Origin", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static bool ImportStarterAssets()
        {
            if (HasXROrigin()) { Debug.Log("[DRUMIX] XR Origin ya existe en el proyecto."); return false; }
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + XriPackage);
            if (info == null) { Debug.LogWarning("[DRUMIX] XR Interaction Toolkit no está instalado: se usa el rig VR propio de DRUMIX."); return false; }
            bool found = false, ok = false;
            foreach (var sample in UnityEditor.PackageManager.UI.Sample.FindByPackage(XriPackage, info.version))
            {
                if (sample.displayName != "Starter Assets") continue;
                found = true;
                if (sample.isImported) return false;
                ok = sample.Import(UnityEditor.PackageManager.UI.Sample.ImportOptions.HideImportWindow);
                break;
            }
            if (!found) { Debug.LogWarning("[DRUMIX] No encontré XRI > Starter Assets: se usa el rig VR propio de DRUMIX."); return false; }
            Debug.Log(ok ? "[DRUMIX] Importado XRI " + info.version + " > Starter Assets (XR Origin para Quest)." : "[DRUMIX] No se pudo importar Starter Assets: se usa el rig VR propio de DRUMIX.");
            return ok;
        }

        // ---------- 3) escena ----------
        static void Build()
        {
            // Escena vacía primero: así ARMAR TODO no pregunta por guardar "Untitled".
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!DrumixBuilder.BuildAll(false)) throw new Exception("ARMAR TODO no terminó.");
            DrumixBuilder.AddSceneToBuildSettings();
            if (File.Exists(DrumixBuilder.ScenePath)) EditorSceneManager.OpenScene(DrumixBuilder.ScenePath, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();

            bool android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            bool canAndroid = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android);
            string msg = "Escena DRUMIX_Bar armada y abierta (quedó AL FINAL de Build Settings; las escenas del equipo no se tocan).\n\nDale Play: Enter para empezar, teclas A S D F G H J K.\nCon Quest Link: ponte el visor y Play.";
            if (!android && canAndroid)
            {
                if (EditorUtility.DisplayDialog("DRUMIX listo", msg + "\n\n¿Cambio ya la plataforma a Quest (Android) para compilar el APK? (tarda unos minutos; también puedes hacerlo luego en DRUMIX > Proyecto)", "Sí, a Quest", "Ahora no"))
                    EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android, BuildTarget.Android);
            }
            else
            {
                EditorUtility.DisplayDialog("DRUMIX listo", msg + (android || canAndroid ? "" : "\n\nPara el APK del Quest instala en Unity Hub el módulo Android Build Support."), "¡Dale!");
            }
        }

        // ---------- estado ----------
        static int ReadStep() { int t; return ReadStep(out t); }
        static int ReadStep(out int tries)
        {
            tries = 0;
            try
            {
                if (!File.Exists(StateFile)) return StepConfig;
                var parts = File.ReadAllText(StateFile).Trim().Split(' ');
                int step;
                if (parts.Length > 1) int.TryParse(parts[1], out tries);
                return int.TryParse(parts[0], out step) ? step : StepConfig;
            }
            catch (Exception) { return StepConfig; }
        }
        static void WriteStep(int step, int tries)
        {
            try { File.WriteAllText(StateFile, step + " " + tries + "\n"); } catch (Exception e) { Debug.LogWarning("[DRUMIX] " + e.Message); }
        }
    }
}
