using System;
using UnityEngine;
using UnityEngine.UI;

namespace Drumix
{
    /// Piezas de UI hechas por código (Image + Text, sin TextMeshPro ni sprites externos): marco "grabado" con doble borde
    /// y rayado en las esquinas, botones con micro-feedback (DrumixButtonFX), sliders, casillas y barras.
    /// Estilo tomado de la UI de BateriaVR, adaptado a DRUMIX.
    public static class DrumixUIKit
    {
        public static readonly Color Background = Hex("1A0F2E", 0.94f);
        public static readonly Color Panel = Hex("302044");
        public static readonly Color PanelHover = Hex("4A3166");
        public static readonly Color White = Hex("F5EBD8");
        public static readonly Color Muted = Hex("BEA6D5");
        public static readonly Color Accent = Hex("C6A0FF");
        public static readonly Color Gold = Hex("FFD166");
        public static readonly Color Blue = Hex("53C5FF");
        public static readonly Color Miss = Hex("FF5B78");
        public static readonly Color Line = new Color(0.75f, 0.55f, 0.95f, 0.6f);

        public static Color Hex(string v, float a = 1f) { Color c; ColorUtility.TryParseHtmlString("#" + v, out c); c.a = a; return c; }

        static readonly Color[] LaneColors = { Hex("FF8C42"), Hex("FF5B78"), Hex("FFD166"), Hex("36E3C2"), Hex("53C5FF"), Hex("8C91FF"), Hex("DA83FF"), Hex("FFFFFF") };
        public static Color LaneColor(DrumixLane l) { return LaneColors[Mathf.Clamp((int)l, 0, 7)]; }
        static readonly string[] Codes = { "KI", "SN", "HH", "T1", "T2", "FT", "CR", "RI" };
        public static string LaneCode(DrumixLane l) { return Codes[Mathf.Clamp((int)l, 0, 7)]; }

        static Font font;
        public static Font Font { get { if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); return font; } }

        public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }
        public static RectTransform Stretch(Transform parent, string name)
        {
            var rt = Rect(parent, name, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            return rt;
        }
        public static Image Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var img = Rect(parent, name, pos, size).gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return img;
        }
        public static Text Label(Transform parent, string name, string text, int size, Vector2 pos, Vector2 box, Color color, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = Rect(parent, name, pos, box).gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style; t.alignment = align;
            t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        public static void Shadow(Graphic g) { var s = g.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, 0.75f); s.effectDistance = new Vector2(2, -2); }

        /// Borde de 1.5 px a "inset" px del borde del padre.
        public static void Border(RectTransform parent, float inset, Color color, float thick = 1.5f)
        {
            Edge(parent, "BordeSup", new Vector2(0, 1), new Vector2(1, 1), new Vector2(-inset * 2, thick), new Vector2(0, -inset), color);
            Edge(parent, "BordeInf", new Vector2(0, 0), new Vector2(1, 0), new Vector2(-inset * 2, thick), new Vector2(0, inset), color);
            Edge(parent, "BordeIzq", new Vector2(0, 0), new Vector2(0, 1), new Vector2(thick, -inset * 2), new Vector2(inset, 0), color);
            Edge(parent, "BordeDer", new Vector2(1, 0), new Vector2(1, 1), new Vector2(thick, -inset * 2), new Vector2(-inset, 0), color);
        }
        static void Edge(RectTransform parent, string name, Vector2 min, Vector2 max, Vector2 size, Vector2 pos, Color color)
        {
            var img = Box(parent, name, Vector2.zero, Vector2.zero, color);
            var rt = img.rectTransform; rt.anchorMin = min; rt.anchorMax = max; rt.sizeDelta = size; rt.anchoredPosition = pos;
        }

        /// Marco principal: fondo oscuro, doble borde y rayado de grabado en las esquinas de arriba.
        public static RectTransform Frame(Transform parent, string name, Vector2 pos, Vector2 size, bool hatch = true)
        {
            var img = Box(parent, name, pos, size, Background);
            img.raycastTarget = true; // bloquea clics al mundo detrás
            var rt = img.rectTransform;
            Border(rt, 9, Muted);
            Border(rt, 16, new Color(0.55f, 0.34f, 0.75f, 0.45f));
            if (hatch)
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 7; i++)
                    {
                        var h = Box(rt, "Grabado", Vector2.zero, new Vector2(26, 1.5f), Muted);
                        h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(side < 0 ? 0f : 1f, 1f);
                        h.rectTransform.anchoredPosition = new Vector2(-side * (34 + i * 7), -30);
                        h.rectTransform.localRotation = Quaternion.Euler(0, 0, side * 45f);
                    }
            return rt;
        }

        public static Button Button(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action action, int fontSize = 26)
        {
            var img = Box(parent, name, pos, size, Panel);
            img.raycastTarget = true;
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None; // el color lo maneja DrumixButtonFX
            b.targetGraphic = img;
            var fx = img.gameObject.AddComponent<DrumixButtonFX>();
            fx.normal = Panel; fx.hover = PanelHover; fx.pressed = Accent;
            Border(img.rectTransform, 3, Line);
            Label(img.rectTransform, "Texto", text, fontSize, Vector2.zero, size, White, FontStyle.Bold);
            if (Application.isPlaying && action != null) b.onClick.AddListener(() => { var fxx = b.GetComponent<DrumixButtonFX>(); if (fxx && fxx.locked) return; action(); });
            return b;
        }
        public static void SetText(Button b, string text) { if (!b) return; var t = b.GetComponentInChildren<Text>(true); if (t) t.text = text; }
        public static void SetLocked(Button b, bool locked) { if (!b) return; var fx = b.GetComponent<DrumixButtonFX>(); if (fx) fx.locked = locked; }
        public static void SetSelected(Button b, bool on) { if (!b) return; var fx = b.GetComponent<DrumixButtonFX>(); if (fx) fx.selected = on; }

        /// Barra de progreso: devuelve el relleno (se ajusta con SetProgress).
        public static RectTransform Progress(Transform parent, string name, Vector2 pos, Vector2 size, Color fill)
        {
            var bg = Box(parent, name, pos, size, Panel);
            var f = Box(bg.rectTransform, "Relleno", Vector2.zero, Vector2.zero, fill).rectTransform;
            f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(0, 1); f.pivot = new Vector2(0, 0.5f); f.sizeDelta = Vector2.zero;
            return f;
        }
        public static void SetProgress(RectTransform fill, float v) { if (fill) fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1f); }

        public static Slider Slider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, bool whole, Action<float> changed)
        {
            var area = Box(parent, name, pos, size, new Color(1, 1, 1, 0.004f)); area.raycastTarget = true;
            var s = area.gameObject.AddComponent<Slider>();
            Box(area.rectTransform, "Pista", Vector2.zero, new Vector2(size.x - 24, 8), Panel);
            var fillArea = Rect(area.rectTransform, "AreaRelleno", Vector2.zero, new Vector2(size.x - 24, 8));
            var fill = Box(fillArea, "Relleno", Vector2.zero, Vector2.zero, Accent).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.sizeDelta = Vector2.zero;
            var handleArea = Rect(area.rectTransform, "AreaManija", Vector2.zero, new Vector2(size.x - 24, size.y));
            var handle = Box(handleArea, "Manija", Vector2.zero, new Vector2(22, 34), White);
            handle.raycastTarget = true;
            s.fillRect = fill; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.wholeNumbers = whole;
            s.transition = Selectable.Transition.ColorTint;
            if (Application.isPlaying && changed != null) s.onValueChanged.AddListener(v => changed(v));
            return s;
        }

        public static Toggle Toggle(Transform parent, string name, string text, Vector2 pos, Vector2 size, Action<bool> changed)
        {
            var root = Box(parent, name, pos, size, Panel); root.raycastTarget = true;
            Border(root.rectTransform, 3, Line);
            var square = Box(root.rectTransform, "Casilla", new Vector2(-size.x / 2 + 32, 0), new Vector2(30, 30), Background);
            Border(square.rectTransform, 0, Muted);
            var mark = Box(square.rectTransform, "Marca", Vector2.zero, new Vector2(20, 20), Accent);
            Label(root.rectTransform, "Texto", text, 23, new Vector2(22, 0), new Vector2(size.x - 70, size.y), White);
            var t = root.gameObject.AddComponent<Toggle>();
            t.transition = Selectable.Transition.None; t.targetGraphic = root; t.graphic = mark;
            var fx = root.gameObject.AddComponent<DrumixButtonFX>(); fx.normal = Panel; fx.hover = PanelHover; fx.pressed = Accent;
            if (Application.isPlaying && changed != null) t.onValueChanged.AddListener(v => changed(v));
            return t;
        }

        /// Rombo (estrella simple, sin depender de caracteres especiales de la fuente).
        public static Image Diamond(Transform parent, string name, Vector2 pos, float size, Color color)
        {
            var d = Box(parent, name, pos, new Vector2(size, size), color);
            d.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            return d;
        }
    }
}
