using UnityEngine;
using UnityEngine.UI;

namespace KunchengRPG.EditorTools
{
    /// <summary>
    /// Small helpers for assembling legacy UI.Text hierarchies from code.
    /// Kept separate from SceneBuilder so the scene layout reads as layout, not as
    /// RectTransform boilerplate.
    /// </summary>
    public static class UIBuilder
    {
        public static readonly Color Ink = new Color(0.92f, 0.94f, 0.93f, 1f);
        public static readonly Color Accent = new Color(0.36f, 0.79f, 0.65f, 1f);
        public static readonly Color PanelBg = new Color(0.06f, 0.07f, 0.09f, 0.92f);

        public const int RefWidth = 1280;
        public const int RefHeight = 800;

        /// <summary>
        /// A scaled Canvas plus EventSystem. Scale-with-screen-size keeps the pixel
        /// layout stable across window sizes.
        /// </summary>
        public static Canvas CreateCanvas(string name, out GameObject root)
        {
            root = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.matchWidthOrHeight = 0.5f;

            // Every Text under this canvas gets a CJK-capable font at runtime.
            root.AddComponent<KunchengRPG.UI.ApplyCJKFont>();

            return canvas;
        }

        public static GameObject CreateEventSystem()
        {
            return new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        /// <summary>
        /// A full-rect child GameObject with a RectTransform.
        /// </summary>
        public static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A translucent backdrop panel filling its parent.
        /// </summary>
        public static GameObject CreatePanel(string name, Transform parent, Color? bg = null)
        {
            var go = CreateRect(name, parent);
            var img = go.AddComponent<Image>();
            img.color = bg ?? PanelBg;
            return go;
        }

        /// <summary>
        /// A Text anchored by offset from its parent's edges.
        /// </summary>
        public static Text CreateText(
            string name, Transform parent, string content, int fontSize,
            TextAnchor anchor = TextAnchor.UpperLeft, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = color ?? Ink;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;

            // Built-in Arial as a placeholder; ApplyCJKFont swaps it at runtime.
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Stretch(go.GetComponent<RectTransform>());
            return text;
        }

        /// <summary>
        /// Position a rect by pixel offsets from a single anchor point.
        /// </summary>
        public static RectTransform Place(
            GameObject go, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>
        /// A vertical list container that lays its children out top-down.
        /// Controllers instantiate choice rows into this.
        /// </summary>
        public static Transform CreateVerticalList(
            string name, Transform parent, Vector2 pos, Vector2 size, float spacing = 6f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place(go, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, size);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            return go.transform;
        }
    }
}
