using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Drumix.EditorTools
{
    /// Medición de lo que hay en la escena DRUMIX para que nada quede metido dentro de otra cosa.
    /// Lo usan ARMAR TODO (empuja u omite) y Revisar (reporta). Las reglas puras están en DrumixLayout.
    public static class DrumixOverlap
    {
        public enum Kind { Kit, Furniture, Npc, Stage, Player, Ui }

        public class Item
        {
            public string name; public Kind kind; public GameObject go; public Box3 box;
            public override string ToString() { return name; }
        }

        public const string EnvName = "Bar (escenario)";
        public const string KitName = "Kit (bateria fija)";
        static readonly string[] NotFurniture = { "Piso", "Tarima", "Pared", "Neon", "Escenario" };

        public static Box3 ToBox(Bounds b) { return new Box3(b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z); }
        public static Bounds ToBounds(Box3 b)
        {
            var bounds = new Bounds(); bounds.SetMinMax(new Vector3(b.x0, b.y0, b.z0), new Vector3(b.x1, b.y1, b.z1)); return bounds;
        }

        static bool Counts(Renderer r)
        {
            var mf = r ? r.GetComponent<MeshFilter>() : null;
            if (mf && !mf.sharedMesh) return false; // copia duplicada vaciada
            return r && r.enabled && r.gameObject.activeInHierarchy &&
                   !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer);
        }

        /// Bounds de todo lo visible bajo 'go'.
        public static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds(); bool any = false;
            if (!go) return false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(false))
            {
                if (!Counts(r)) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        /// Caja del cuerpo del jugador alrededor del origen del rig (sin pies: los pedales pueden estar a la altura del pie).
        public static Box3 PlayerBox(Vector3 origin)
        {
            return new Box3(origin.x - 0.22f, origin.y + 0.8f, origin.z - 0.25f, origin.x + 0.22f, origin.y + 1.85f, origin.z + 0.1f);
        }

        /// Huella de un NPC: alto real, pero ancho/fondo de una persona (los brazos en pose T no cuentan).
        public static Box3 NpcBox(Transform npc, Bounds b)
        {
            const float half = 0.27f;
            float hx = Mathf.Min(b.extents.x, half), hz = Mathf.Min(b.extents.z, half);
            Vector3 p = npc.position;
            return new Box3(p.x - hx, b.min.y, p.z - hz, p.x + hx, b.max.y, p.z + hz);
        }

        public static List<Item> Collect(GameObject root, Vector3 playerOrigin)
        {
            var items = new List<Item>();
            if (!root) return items;
            items.Add(new Item { name = "Jugador (cuerpo)", kind = Kind.Player, box = PlayerBox(playerOrigin) });

            var env = root.transform.Find(EnvName);
            if (env)
                foreach (Transform child in env)
                {
                    bool isStage = child.name.StartsWith("Escenario", StringComparison.Ordinal);
                    if (isStage)
                    {
                        // Piezas sueltas del escenario del equipo (paneles, columnas); la tarima gigante no (es el piso).
                        foreach (var r in child.GetComponentsInChildren<Renderer>(false))
                        {
                            if (!Counts(r)) continue;
                            var b = r.bounds;
                            if (b.size.x > 6f || b.size.z > 6f || b.size.y < 0.06f) continue;
                            items.Add(new Item { name = "Escenario/" + r.name, kind = Kind.Stage, go = r.gameObject, box = ToBox(b) });
                        }
                        continue;
                    }
                    if (NotFurniture.Any(n => child.name.StartsWith(n, StringComparison.Ordinal))) continue;
                    Bounds fb;
                    if (TryBounds(child.gameObject, out fb)) items.Add(new Item { name = child.name, kind = Kind.Furniture, go = child.gameObject, box = ToBox(fb) });
                }

            var kit = root.transform.Find(KitName);
            if (kit)
                foreach (var r in kit.GetComponentsInChildren<Renderer>(false))
                {
                    if (!Counts(r)) continue;
                    var b = r.bounds;
                    if (b.size.y < 0.06f && (b.size.x > 1f || b.size.z > 1f)) continue; // alfombra: es piso
                    items.Add(new Item { name = "Batería/" + r.name, kind = Kind.Kit, go = r.gameObject, box = ToBox(b) });
                }

            foreach (var npc in root.GetComponentsInChildren<DrumixCrowdNPC>(false))
            {
                Bounds nb;
                // Medida real de la malla (Renderer.bounds de un SkinnedMeshRenderer puede estar sin actualizar en el editor).
                nb = DrumixUtil.MeasureBounds(npc.gameObject, null);
                if (nb.size.y > 0.01f || TryBounds(npc.gameObject, out nb)) items.Add(new Item { name = npc.name, kind = Kind.Npc, go = npc.gameObject, box = NpcBox(npc.transform, nb) });
            }

            foreach (var ui in root.GetComponentsInChildren<DrumixUI>(false))
                foreach (var canvas in ui.GetComponentsInChildren<Canvas>(false))
                {
                    if (canvas.renderMode != RenderMode.WorldSpace) continue;
                    var corners = new Vector3[4];
                    ((RectTransform)canvas.transform).GetWorldCorners(corners);
                    var b = new Bounds(corners[0], Vector3.zero);
                    for (int i = 1; i < 4; i++) b.Encapsulate(corners[i]);
                    b.Expand(new Vector3(0.02f, 0.02f, 0.02f)); // grosor del panel
                    items.Add(new Item { name = "UI " + canvas.name, kind = Kind.Ui, go = canvas.gameObject, box = ToBox(b) });
                }
            return items;
        }

        /// Pares que NO deben tocarse. (Batería contra batería y escenario contra escenario son internos del modelo.)
        public static bool MustNotTouch(Kind a, Kind b)
        {
            if (a == b) return a == Kind.Npc || a == Kind.Furniture;
            if (a == Kind.Player || b == Kind.Player) return true;
            if (a == Kind.Npc || b == Kind.Npc) return true;
            if (a == Kind.Ui || b == Kind.Ui) return true;
            if (a == Kind.Furniture || b == Kind.Furniture) return true;
            return false; // Kit vs Stage: la batería va sobre la tarima del escenario
        }

        public static List<KeyValuePair<Item, Item>> FindOverlaps(List<Item> items, float tolerance = DrumixLayout.Tolerance)
        {
            var list = new List<KeyValuePair<Item, Item>>();
            for (int i = 0; i < items.Count; i++)
                for (int j = i + 1; j < items.Count; j++)
                {
                    var a = items[i]; var b = items[j];
                    if (!MustNotTouch(a.kind, b.kind)) continue;
                    if (DrumixLayout.Overlaps(a.box, b.box, tolerance)) list.Add(new KeyValuePair<Item, Item>(a, b));
                }
            return list;
        }

        /// Renderers duplicados: misma malla en la misma posición/rotación/escala -> z-fighting seguro.
        public static List<string> DuplicateRenderers(GameObject root)
        {
            return FindDuplicateRenderers(root).Select(p => p.Key.name + " = " + p.Value.name).ToList();
        }

        public static List<KeyValuePair<MeshRenderer, MeshRenderer>> FindDuplicateRenderers(GameObject root)
        {
            var seen = new Dictionary<Mesh, Dictionary<string, MeshRenderer>>(); var dup = new List<KeyValuePair<MeshRenderer, MeshRenderer>>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (!mf.sharedMesh || !r || !r.enabled) continue;
                var m = mf.transform.localToWorldMatrix;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 12; i++) sb.Append('|').Append(Mathf.RoundToInt(m[i] * 1000f));
                string key = sb.ToString();
                Dictionary<string, MeshRenderer> byPose;
                if (!seen.TryGetValue(mf.sharedMesh, out byPose)) seen[mf.sharedMesh] = byPose = new Dictionary<string, MeshRenderer>();
                MeshRenderer other;
                if (byPose.TryGetValue(key, out other)) dup.Add(new KeyValuePair<MeshRenderer, MeshRenderer>(other, r)); else byPose[key] = r;
            }
            return dup;
        }

        /// Superficies horizontales grandes (pisos, tarimas, alfombras) que quedan a la misma altura en el mismo lugar.
        public static List<string> CoplanarSurfaces(GameObject root)
        {
            var flats = new List<KeyValuePair<string, Bounds>>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(false))
            {
                if (!Counts(r)) continue;
                var b = r.bounds;
                if (b.size.x < 0.8f || b.size.z < 0.8f) continue;
                if (b.size.y < 0.08f) flats.Add(new KeyValuePair<string, Bounds>(r.name, b));
                else if (r is MeshRenderer && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh &&
                         r.GetComponent<MeshFilter>().sharedMesh.name == "Cube")
                    flats.Add(new KeyValuePair<string, Bounds>(r.name, b)); // tarima/plataformas cúbicas: su cara de arriba
            }
            var list = new List<string>();
            for (int i = 0; i < flats.Count; i++)
                for (int j = i + 1; j < flats.Count; j++)
                {
                    var a = flats[i].Value; var b = flats[j].Value;
                    if (DrumixLayout.CoplanarRisk(ToBox(a), a.max.y, ToBox(b), b.max.y))
                        list.Add(flats[i].Key + " / " + flats[j].Key + " (y=" + a.max.y.ToString("0.000") + ")");
                }
            return list;
        }

        public static int ExtraMaterialRenderers(GameObject root, List<string> names)
        {
            int count = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                var mf = r.GetComponent<MeshFilter>(); if (mf) mesh = mf.sharedMesh;
                var smr = r as SkinnedMeshRenderer; if (smr) mesh = smr.sharedMesh;
                if (!mesh) continue;
                if (DrumixLayout.ExtraMaterialSlots(r.sharedMaterials.Length, mesh.subMeshCount) > 0) { count++; if (names != null && names.Count < 8) names.Add(r.name); }
            }
            return count;
        }

        public static int PinkRenderers(GameObject root, List<string> names)
        {
            int count = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.sharedMaterials)
                    if (!m || !m.shader || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported)
                    { count++; if (names != null && names.Count < 8) names.Add(r.name); break; }
            }
            return count;
        }
    }
}
