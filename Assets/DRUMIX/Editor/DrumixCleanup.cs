using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Drumix.EditorTools
{
    /// DRUMIX > Limpiar paquete viejo: lista (y si confirmas manda a la papelera) SOLO archivos de
    /// nuestros zips anteriores. Nunca toca Assets/Scripts/ (raíz) ni Assets/Scripts/Editor/ del equipo,
    /// ni clases del equipo (DrumPad, AudioManager, LevelManager, DifficultyManager, ChangeMaterialOnGrab, BeatManager).
    public static class DrumixCleanup
    {
        public static List<string> FindOldFiles()
        {
            var result = new List<string>();
            foreach (var f in DrumixCleanupRules.OldFolders) if (AssetDatabase.IsValidFolder(f)) result.Add(f + "/  (carpeta)");
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (DrumixCleanupRules.IsOldOurs(path, () => File.ReadAllText(path))) result.Add(path);
            }
            return result;
        }

        [MenuItem("DRUMIX/Limpiar paquete viejo", false, 30)]
        public static void Clean()
        {
            var list = FindOldFiles();
            if (list.Count == 0) { EditorUtility.DisplayDialog("DRUMIX", "No hay nada viejo nuestro. Todo limpio. 👌", "OK"); return; }
            string shown = string.Join("\n", list.Take(25).ToArray()) + (list.Count > 25 ? "\n... y " + (list.Count - 25) + " más (ver Console)" : "");
            Debug.Log("[DRUMIX] Archivos viejos nuestros:\n" + string.Join("\n", list.ToArray()));
            if (!EditorUtility.DisplayDialog("DRUMIX · Limpiar paquete viejo",
                "Estos son SOLO de nuestros zips anteriores (no toco Assets/Scripts del equipo):\n\n" + shown +
                "\n\nSe mandan a la PAPELERA (se pueden recuperar, y Git también los tiene).\n¿Borrar?", "Sí, borrar", "Cancelar")) return;
            var paths = list.Select(p => p.Replace("/  (carpeta)", "")).ToArray();
            var failed = new List<string>();
            AssetDatabase.StartAssetEditing();
            try { foreach (var p in paths) if (!AssetDatabase.MoveAssetToTrash(p)) failed.Add(p); }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }
            EditorUtility.DisplayDialog("DRUMIX", (paths.Length - failed.Count) + " elemento(s) a la papelera." +
                (failed.Count > 0 ? "\nNo pude: " + string.Join(", ", failed.ToArray()) : "") +
                "\n\nSi una escena del equipo usaba esos scripts, verás 'Missing Script' ahí: quita esos componentes o avísame.", "OK");
        }
    }
}
