using System;
using System.Collections.Generic;

// C# PURO (sin UnityEditor): decide qué archivos viejos son NUESTROS. Probado fuera de Unity.
namespace Drumix.EditorTools
{
    public static class DrumixCleanupRules
    {
        /// Carpetas que eran 100 % nuestras (zips anteriores).
        public static readonly string[] OldFolders =
        {
            "Assets/BateriaVR", "Assets/BateriaVR_Demo", "Assets/PublicoScripts",
            "Assets/ThirdParty/Grab3DDrums", "Assets/ThirdParty/KenneyCharacters", "Assets/ThirdParty/KenneyFurniture",
            "Assets/ThirdParty/KenneyFood", "Assets/ThirdParty/QuaterniusBottles", "Assets/ThirdParty/RoseZsky",
            "Assets/ThirdParty/SFXMintDrums", "Assets/ThirdParty/OGASnare", "Assets/ThirdParty/CrowdAudio",
            "Assets/ThirdParty/KenneyUI", "Assets/ThirdParty/Cinzel",
        };

        /// Scripts de nuestros paquetes viejos (BateriaVR, PublicoScripts, DemoMejorada).
        public static readonly HashSet<string> OldScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AudienceMember", "AudienceReactionManager", "DrumGameEvents", "CrowdNPC", "FullScreenUISetup", "ArmarTodoAutomatico",
            "VRDemoBuilder", "VRCodeChecks", "ThirdPartyDemoAssets", "CalibrationUI", "CrowdProjectile", "CrowdThrowPool", "CrowdThrowRules",
            "DanteDialogueUI", "DesktopDrumTester", "DialogueData", "DrumCueController", "DrumKitCalibration", "DrumStick", "GameHUD",
            "GameManager", "GameProgress", "GameSettings", "GameTypes", "HapticFeedback", "KickPedal", "LevelData", "LevelSelectUI",
            "MainMenuUI", "NextNoteUI", "NoteHighway", "ProceduralDrumAudio", "ResultsUI", "ScoreManager", "SequenceManager",
            "SettingsUI", "SongData", "StageAudio", "StageLightingRig", "TrackedNodePose", "UIAudioFeedback", "UIFactory", "UIFlow",
            "UIMicroFeedback", "UIPanelTransition", "UISoundPlayer", "UIThemeAssets", "XRCameraSwitcher", "XRCanvasPointer", "XRGameControls",
            "DrumPad",
        };

        /// Nombres del EQUIPO: solo se tocan si el contenido prueba que es nuestro.
        public static readonly HashSet<string> TeamNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DrumPad", "AudioManager", "LevelManager", "DifficultyManager", "ChangeMaterialOnGrab", "BeatManager", "ConfigurarManagers",
            // Usados en las escenas del equipo (1.NivelUno...) aunque no venían en los archivos compartidos:
            "SequenceManager", "GameManager", "TargetZoneTrigger", "AutoInstructions", "GestorInstrucciones", "NewMonoBehaviourScript",
        };

        /// Carpetas del equipo: archivos directamente dentro NUNCA se tocan (en subcarpetas, un nombre del equipo
        /// solo se toca si el contenido prueba que es de un paquete viejo nuestro).
        public static readonly string[] ProtectedDirs = { "Assets/Scripts", "Assets/Scripts/Editor", "Assets/Scripts/_Recuperados", "Assets/Editor" };

        public static bool InProtectedDir(string path)
        {
            string dir = Dir(path);
            foreach (var d in ProtectedDirs) if (string.Equals(dir, d, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static string Dir(string path)
        {
            path = path.Replace('\\', '/');
            int i = path.LastIndexOf('/');
            return i < 0 ? "" : path.Substring(0, i);
        }
        static string NameNoExt(string path)
        {
            path = path.Replace('\\', '/');
            string file = path.Substring(path.LastIndexOf('/') + 1);
            int dot = file.LastIndexOf('.');
            return dot < 0 ? file : file.Substring(0, dot);
        }

        public static bool InOldFolder(string path)
        {
            path = path.Replace('\\', '/');
            foreach (var f in OldFolders) if (path.StartsWith(f + "/", StringComparison.Ordinal)) return true;
            return false;
        }

        /// ¿Es un script viejo NUESTRO que se puede mandar a la papelera? readContent solo se llama si hace falta.
        public static bool IsOldOurs(string path, Func<string> readContent)
        {
            if (string.IsNullOrEmpty(path)) return false;
            path = path.Replace('\\', '/');
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
            if (path.StartsWith("Assets/DRUMIX/", StringComparison.Ordinal)) return false; // el paquete nuevo
            if (InOldFolder(path)) return false; // ya lo cubre la carpeta entera
            if (InProtectedDir(path)) return false; // equipo
            string name = NameNoExt(path);
            if (!OldScripts.Contains(name)) return false;
            string text = null;
            try { text = readContent != null ? readContent() : null; } catch (Exception) { text = null; }
            // Scripts NUEVOS del equipo que ya usan DrumixEvents (p. ej. el SequenceManager de Andrés) nunca se tocan.
            if (text != null && text.Contains("DrumixEvents")) return false;
            if (!TeamNames.Contains(name)) return true;
            return text != null && (text.Contains("DrumGameEvents") || text.Contains("ReportHit") || text.Contains("Samuel:"));
        }
    }
}
