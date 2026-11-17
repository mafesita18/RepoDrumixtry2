using System.Collections.Generic;
using System.Text;

namespace Drumix.EditorTools
{
    /// Comparación de nombres de archivo por palabras clave. C# puro (se prueba fuera de Unity).
    public static class DrumixNameMatch
    {
        /// "BassDrum_01" -> {bass, drum, 01}
        public static List<string> Tokens(string name)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            char prev = ' ';
            foreach (char ch in name ?? "")
            {
                bool alnum = char.IsLetterOrDigit(ch);
                bool split = !alnum
                    || (char.IsUpper(ch) && char.IsLower(prev))
                    || (sb.Length > 0 && char.IsLetterOrDigit(prev) && char.IsDigit(ch) != char.IsDigit(prev));
                if (split && sb.Length > 0) { tokens.Add(sb.ToString().ToLowerInvariant()); sb.Length = 0; }
                if (alnum) sb.Append(ch);
                prev = ch;
            }
            if (sb.Length > 0) tokens.Add(sb.ToString().ToLowerInvariant());
            return tokens;
        }

        public static string Compact(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in (s ?? "").ToLowerInvariant())
            {
                char c = ch;
                if (c == 'á') c = 'a'; else if (c == 'é') c = 'e'; else if (c == 'í') c = 'i'; else if (c == 'ó') c = 'o'; else if (c == 'ú') c = 'u'; else if (c == 'ñ') c = 'n';
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        /// Palabras de 5+ letras: basta con estar contenidas ("hihat" en "Hi_Hat_02").
        /// Palabras cortas ("tom", "ride", "npc", "kick"): deben ser una palabra completa
        /// (así "tom" NO coincide con "tomato" ni "bottom", ni "ride" con "override").
        public static bool Matches(string fileName, string keyword)
        {
            string kw = Compact(keyword);
            if (kw.Length == 0) return false;
            if (kw.Length >= 5) return Compact(fileName).Contains(kw);
            // Palabra completa o varias palabras seguidas: "Tom1" = {tom,1} -> "tom" y "tom1" coinciden.
            var tokens = Tokens(fileName);
            for (int i = 0; i < tokens.Count; i++)
            {
                string joined = "";
                for (int j = i; j < tokens.Count && joined.Length < kw.Length; j++)
                {
                    joined += tokens[j];
                    if (joined == kw) return true;
                }
            }
            return false;
        }

        /// Carpetas que nunca se usan como "assets del proyecto" (paquete DRUMIX, samples, plantillas XR...).
        public static readonly string[] ExcludedFolders =
        {
            "/DRUMIX/", "/DRUMIX_Proyecto/", "/BateriaVR/", "/PublicoScripts/", "/Samples/", "/TextMesh Pro/", "/Plugins/", "/XR/", "/XRI/",
            "/Oculus/", "/Meta/", "/VRTemplateAssets/", "/Editor/", "/StreamingAssets/", "/Settings/"
        };

        public static bool IsExcludedPath(string path)
        {
            string p = "/" + (path ?? "").Replace('\\', '/') + "/";
            foreach (var f in ExcludedFolders) if (p.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static string FileName(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            int slash = p.LastIndexOf('/'); string f = slash >= 0 ? p.Substring(slash + 1) : p;
            int dot = f.LastIndexOf('.'); return dot > 0 ? f.Substring(0, dot) : f;
        }
        static string Folder(string path)
        {
            string p = (path ?? "").Replace('\\', '/');
            int slash = p.LastIndexOf('/'); return slash >= 0 ? p.Substring(0, slash) : "";
        }

        /// Puntaje de un modelo (menor = mejor; -1 = no sirve). Coincide por NOMBRE de archivo; la carpeta solo desempata
        /// (p. ej. "drum-kit/source/Drum Set_sketchfab.fbx" gana a la copia en "ESCENARIO 3D/source").
        public static int ModelScore(string path, string[] keywords, string[] exclude)
        {
            if (IsExcludedPath(path)) return -1;
            string file = FileName(path);
            if (HasAny(file, exclude)) return -1;
            for (int k = 0; k < keywords.Length; k++)
            {
                if (!Matches(file, keywords[k])) continue;
                int depth = 0; foreach (char c in path) if (c == '/') depth++;
                int score = k * 20 + (path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase) ? 0 : 3) + depth;
                string folder = Folder(path);
                foreach (var kw in keywords) if (Matches(folder, kw)) { score -= 6; break; }
                return score;
            }
            return -1;
        }

        /// Rutas ordenadas de mejor a peor (solo las que coinciden).
        public static List<string> RankModels(IEnumerable<string> paths, string[] keywords, string[] exclude)
        {
            var scored = new List<KeyValuePair<int, string>>();
            foreach (var p in paths) { int s = ModelScore(p, keywords, exclude); if (s >= 0) scored.Add(new KeyValuePair<int, string>(s, p)); }
            scored.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : string.CompareOrdinal(a.Value, b.Value));
            var result = new List<string>();
            foreach (var kv in scored) result.Add(kv.Value);
            return result;
        }

        /// Primer audio que coincide (por orden de palabra clave), o null.
        public static string FirstClip(IEnumerable<string> paths, string[] keywords, string[] exclude)
        {
            var list = new List<string>();
            foreach (var p in paths) if (!IsExcludedPath(p)) list.Add(p);
            list.Sort(string.CompareOrdinal);
            foreach (var kw in keywords)
                foreach (var p in list)
                {
                    string f = FileName(p);
                    if (Matches(f, kw) && !HasAny(f, exclude)) return p;
                }
            return null;
        }

        public static bool HasAny(string fileName, string[] words)
        {
            if (words == null) return false;
            foreach (var w in words) if (Matches(fileName, w)) return true;
            return false;
        }
    }
}
