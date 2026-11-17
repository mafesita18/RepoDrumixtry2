using System;
using System.Collections.Generic;
using UnityEngine;

namespace Drumix
{
    /// Utilidades sin dependencias duras: buscar tipos por nombre (XRI, Input System, scripts del equipo).
    public static class DrumixUtil
    {
        static readonly Dictionary<string, Type> cache = new Dictionary<string, Type>();

        public static Type FindType(string fullName)
        {
            Type found;
            if (cache.TryGetValue(fullName, out found)) return found;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { found = assembly.GetType(fullName, false); } catch { found = null; }
                if (found != null) break;
            }
            cache[fullName] = found;
            return found;
        }

        public static T FindAny<T>() where T : UnityEngine.Object
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }

        public static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_6000_5_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>();
#elif UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>();
#endif
        }

        public static UnityEngine.Object[] FindAll(Type type)
        {
            if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type)) return new UnityEngine.Object[0];
#if UNITY_6000_5_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(type);
#elif UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType(type);
#endif
        }

        /// Quita (o desactiva si no se puede) cualquier interactable de XRI: evita que una pieza
        /// de la batería sea agarrable y salga volando/se mueva al centro.
        public static int NeutralizeGrab(GameObject go, bool destroy)
        {
            int count = 0;
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!behaviour) continue;
                if (!IsInteractable(behaviour.GetType())) continue;
                count++;
                behaviour.enabled = false;
                if (destroy && !IsRequiredByOthers(behaviour))
                {
                    try
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(behaviour);
                        else UnityEngine.Object.DestroyImmediate(behaviour);
                    }
                    catch (Exception) { /* otro componente lo requiere: queda desactivado */ }
                }
            }
            return count;
        }

        /// ¿Otro componente del mismo objeto tiene [RequireComponent] de este? (p. ej. ChangeMaterialOnGrab)
        /// En ese caso NO se borra (Unity tiraría error rojo): solo queda desactivado.
        public static bool IsRequiredByOthers(Component target)
        {
            Type type = target.GetType();
            foreach (var other in target.GetComponents<Component>())
            {
                if (!other || other == target) continue;
                foreach (RequireComponent req in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                {
                    if (req.m_Type0 != null && req.m_Type0.IsAssignableFrom(type)) return true;
                    if (req.m_Type1 != null && req.m_Type1.IsAssignableFrom(type)) return true;
                    if (req.m_Type2 != null && req.m_Type2.IsAssignableFrom(type)) return true;
                }
            }
            return false;
        }

        public static bool IsInteractable(Type type)
        {
            for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                string n = t.Name;
                if (n == "XRGrabInteractable" || n == "XRBaseInteractable" || n == "XRSimpleInteractable" ||
                    n.EndsWith("GrabInteractable", StringComparison.Ordinal) || n == "Grabbable" || n == "HandGrabInteractable")
                    return true;
            }
            return false;
        }

        /// True si en el objeto hay un DrumPad del equipo (clase global "DrumPad").
        public static bool HasTeamDrumPad(GameObject go)
        {
            foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                if (behaviour && behaviour.GetType().Name == "DrumPad" && string.IsNullOrEmpty(behaviour.GetType().Namespace))
                    return true;
            return false;
        }

        public static void ForceVisible(GameObject go)
        {
            if (!go.activeSelf) go.SetActive(true);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
                renderer.forceRenderingOff = false;
                var skinned = renderer as SkinnedMeshRenderer;
                if (skinned) skinned.updateWhenOffscreen = true;
            }
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// Bounds REALES en el espacio de 'space' (null = mundo).
        /// - MeshFilter: las 8 esquinas de sharedMesh.bounds con la matriz completa del objeto.
        /// - SkinnedMeshRenderer: pose de reposo = sharedMesh.bounds × localToWorld del renderer (no depende de BakeMesh ni de
        ///   Renderer.bounds, que en el editor puede estar sin actualizar). En el editor además se compara con BakeMesh(false)
        ///   (pose actual, ya incluye la escala del Transform) y se usa la pose actual solo si las dos medidas coinciden en tamaño.
        ///   OJO: BakeMesh(mesh, true) devuelve la malla SIN la escala del Transform; con FBX de Blender (escala 100) eso medía
        ///   100 veces menos y dejaba al público de 170 m (por eso "no se veía nadie").
        public static Bounds MeasureBounds(GameObject go, Transform space)
        {
            bool has = false; Bounds b = new Bounds();
            Action<Vector3> add = w =>
            {
                Vector3 p = space ? space.InverseTransformPoint(w) : w;
                if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
            };
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!r || !r.enabled || (!r.gameObject.activeInHierarchy && Application.isPlaying)) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                var skinned = r as SkinnedMeshRenderer;
                Mesh shared = skinned ? skinned.sharedMesh : null;
                if (!skinned) { var f = r.GetComponent<MeshFilter>(); shared = f ? f.sharedMesh : null; }
                if (!shared) continue;
                Bounds rest = WorldBox(shared.bounds, r.transform.localToWorldMatrix);
                if (skinned && (!Application.isPlaying || shared.isReadable))
                {
                    Bounds pose;
                    if (TryBakedBounds(skinned, out pose) && SameSize(pose.size.y, rest.size.y) && SameSize(Mathf.Max(pose.size.x, pose.size.z), Mathf.Max(rest.size.x, rest.size.z), 3f))
                        rest = pose;
                }
                if (!Finite(rest)) continue;
                for (int c = 0; c < 8; c++)
                    add(new Vector3((c & 1) == 0 ? rest.min.x : rest.max.x, (c & 2) == 0 ? rest.min.y : rest.max.y, (c & 4) == 0 ? rest.min.z : rest.max.z));
            }
            if (!has) return new Bounds(space ? space.InverseTransformPoint(go.transform.position) : go.transform.position, Vector3.zero);
            return b;
        }
        static bool Finite(Bounds x) { var s = x.size; return !(float.IsNaN(s.x) || float.IsNaN(s.y) || float.IsNaN(s.z) || float.IsInfinity(s.x) || float.IsInfinity(s.y) || float.IsInfinity(s.z)); }
        static bool SameSize(float a, float b, float tol = 1.6f) { if (!(a > 1e-5f) || !(b > 1e-5f)) return false; float k = a / b; return k < tol && k > 1f / tol; }
        /// Caja en mundo de una caja local (8 esquinas).
        public static Bounds WorldBox(Bounds local, Matrix4x4 m)
        {
            Bounds w = new Bounds(m.MultiplyPoint3x4(local.min), Vector3.zero);
            for (int c = 1; c < 8; c++)
                w.Encapsulate(m.MultiplyPoint3x4(new Vector3((c & 1) == 0 ? local.min.x : local.max.x, (c & 2) == 0 ? local.min.y : local.max.y, (c & 4) == 0 ? local.min.z : local.max.z)));
            return w;
        }
        /// Pose actual con BakeMesh(false): vértices relativos a posición/rotación del renderer, YA escalados por su Transform.
        static bool TryBakedBounds(SkinnedMeshRenderer skinned, out Bounds world)
        {
            world = new Bounds();
            var baked = new Mesh();
            try
            {
                skinned.BakeMesh(baked, false);
                var verts = baked.vertices;
                if (verts == null || verts.Length == 0) return false;
                var m = Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                int step = Mathf.Max(1, verts.Length / 4000);
                world = new Bounds(m.MultiplyPoint3x4(verts[0]), Vector3.zero);
                for (int i = 0; i < verts.Length; i += step) world.Encapsulate(m.MultiplyPoint3x4(verts[i]));
                return Finite(world);
            }
            catch (Exception) { return false; }
            finally { if (Application.isPlaying) UnityEngine.Object.Destroy(baked); else UnityEngine.Object.DestroyImmediate(baked); }
        }

        /// Baja los brazos de un modelo en T-pose (huesos tipo "LeftArm"/"UpperArm_R"), ~75° hacia abajo.
        /// Si ya están abajo no hace nada (se puede llamar varias veces). Devuelve cuántos brazos bajó.
        public static int LowerArms(Transform model, Vector3 up, float amount = 0.85f)
        {
            int n = 0;
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                string name = bone.name.ToLowerInvariant();
                bool arm = (name.Contains("arm") || name.Contains("brazo")) && !name.Contains("fore") && !name.Contains("lower") &&
                           !name.Contains("hand") && !name.Contains("twist") && !name.Contains("ctrl") && !name.Contains("end") && !name.Contains("antebrazo") && !name.Contains("armature");
                if (!arm || bone.childCount == 0) continue;
                Transform child = null;
                foreach (Transform c in bone) { string cn = c.name.ToLowerInvariant(); if (cn.Contains("fore") || cn.Contains("lower") || cn.Contains("elbow") || cn.Contains("hand") || cn.Contains("antebrazo")) { child = c; break; } }
                if (!child) child = bone.GetChild(0);
                Vector3 dir = child.position - bone.position;
                if (dir.sqrMagnitude < 1e-8f) continue;
                dir.Normalize();
                if (Mathf.Abs(Vector3.Dot(dir, up)) > 0.6f) continue; // ya está abajo (o arriba): no tocar
                Vector3 target = Vector3.Slerp(dir, -up, amount).normalized;
                bone.rotation = Quaternion.FromToRotation(dir, target) * bone.rotation;
                n++;
            }
            return n;
        }
    }
}
