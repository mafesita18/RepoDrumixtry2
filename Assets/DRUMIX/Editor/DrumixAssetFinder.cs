using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Drumix.EditorTools
{
    /// Busca assets REALES del proyecto por palabras clave del nombre del archivo.
    /// Ignora nuestras carpetas (DRUMIX, BateriaVR viejo), samples de XR, TextMesh Pro y plugins.
    public static class DrumixAssetFinder
    {
        public const string PackageRoot = "Assets/DRUMIX";
        public const string Bundled = PackageRoot + "/ThirdParty";

        public static bool IsExcluded(string path) { return DrumixNameMatch.IsExcludedPath(path); }

        public static bool Matches(string fileName, string keyword) { return DrumixNameMatch.Matches(fileName, keyword); }
        static bool HasAny(string fileName, string[] words) { return DrumixNameMatch.HasAny(fileName, words); }

        static string[] cachedModels, cachedClips;
        public static void ClearCache() { cachedModels = null; cachedClips = null; }

        static string[] AllPaths(string filter)
        {
            return AssetDatabase.FindAssets(filter, new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => !IsExcluded(p)).Distinct().ToArray();
        }

        /// Mejor prefab/modelo del proyecto que coincida. keywords en orden de preferencia (ver DrumixNameMatch.ModelScore).
        public static GameObject FindModel(string[] keywords, string[] exclude, bool needRenderer = true)
        {
            if (cachedModels == null) cachedModels = AllPaths("t:GameObject");
            foreach (var path in DrumixNameMatch.RankModels(cachedModels, keywords, exclude))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;
                if (needRenderer && go.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                return go;
            }
            return null;
        }

        /// Hasta 'max' modelos distintos (para variar el público).
        public static List<GameObject> FindModels(string[] keywords, string[] exclude, int max)
        {
            if (cachedModels == null) cachedModels = AllPaths("t:GameObject");
            var result = new List<GameObject>();
            foreach (var kw in keywords)
                foreach (var path in cachedModels)
                {
                    if (result.Count >= max) return result;
                    string file = Path.GetFileNameWithoutExtension(path);
                    if (!Matches(file, kw) || HasAny(file, exclude)) continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!go || result.Contains(go)) continue;
                    if (go.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                    Bounds b = MeasurePrefab(go);
                    if (b.size.y < 0.05f) continue;
                    result.Add(go);
                }
            return result;
        }

        public static AudioClip FindClip(string[] keywords, string[] exclude)
        {
            if (cachedClips == null) cachedClips = AllPaths("t:AudioClip");
            string path = DrumixNameMatch.FirstClip(cachedClips, keywords, exclude);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// Audio exacto por nombre de archivo (sin extensión, sin mayúsculas), p. ej. "tambor2" o "Platillo-1".
        public static AudioClip FindClipExact(string fileName)
        {
            if (cachedClips == null) cachedClips = AllPaths("t:AudioClip");
            foreach (var p in cachedClips)
                if (string.Equals(Path.GetFileNameWithoutExtension(p), fileName, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            return null;
        }

        public static T LoadBundled<T>(string relative) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(Bundled + "/" + relative);
        }

        /// Busca un hijo de un modelo cuyo nombre coincida (para kits completos tipo "DrumKit").
        public static Transform FindChild(Transform root, string[] keywords, string[] exclude, ICollection<Transform> used)
        {
            foreach (var kw in keywords)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == root || (used != null && used.Contains(t))) continue;
                    if (!Matches(t.name, kw) || HasAny(t.name, exclude)) continue;
                    if (t.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                    return t;
                }
            return null;
        }

        /// Bounds de un prefab sin instanciarlo en la escena del usuario.
        public static Bounds MeasurePrefab(GameObject prefab)
        {
            var tmp = UnityEngine.Object.Instantiate(prefab);
            tmp.hideFlags = HideFlags.HideAndDontSave;
            tmp.transform.position = Vector3.zero; tmp.transform.rotation = Quaternion.identity;
            Bounds b = DrumixUtil.RendererBounds(tmp);
            UnityEngine.Object.DestroyImmediate(tmp);
            return b;
        }
    }
}
