using UnityEngine;

namespace FriendshipWarriors.UI
{
    /// <summary>
    /// A framed gauge (HP, MP, CT, the timed-press track): the skin's bar frame, sliced to any width, with a fill drawn
    /// in its hollow inside. The fill always covers whole pixels (<see cref="PixelUiScale.FilledPixels"/>).
    /// </summary>
    public sealed class UiBar
    {
        private readonly RectTransform _fill;
        private readonly int _interior;
        private readonly int _left;

        public RectTransform Root { get; }

        /// <summary>The hollow inside the frame, where fills and markers go.</summary>
        public RectTransform Interior { get; }

        public UiBar(Transform parent, string barName, Color fill, int width)
        {
            var skin = UiSkin.Current;
            bool framed = skin != null && skin.Bar.Texture != null;
            int height = framed ? skin.Bar.FrameHeight : 8;
            var inset = framed ? skin.BarInterior : new Vector4(1, 1, 1, 1);
            _left = (int)inset.x;
            _interior = Mathf.Max(1, width - (int)inset.x - (int)inset.z);

            var root = new GameObject(barName, typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
            Root = (RectTransform)root.transform;
            Root.SetParent(parent, false);
            Root.sizeDelta = new Vector2(width, height);
            var element = root.GetComponent<UnityEngine.UI.LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = element.minHeight = height;

            // Back, fill, then the frame on top: the frame's hollow shows the two through it.
            Interior = Block(Root, "Interior", skin != null ? skin.BarBackColor : new Color(0f, 0f, 0f, 0.6f));
            Interior.anchorMin = Vector2.zero;
            Interior.anchorMax = Vector2.one;
            Interior.offsetMin = new Vector2(inset.x, inset.y);
            Interior.offsetMax = new Vector2(-inset.z, -inset.w);

            _fill = Block(Interior, "Fill", fill);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.offsetMin = Vector2.zero;
            _fill.offsetMax = Vector2.zero;

            var frame = new GameObject("Frame", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var frameRect = (RectTransform)frame.transform;
            frameRect.SetParent(Root, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            var image = frame.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = false;
            if (framed)
            {
                image.sprite = UiSkin.Frame(skin.Bar, 0);
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            else
            {
                image.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        public int InteriorWidth => _interior;
        public int InteriorLeft => _left;

        public void SetFraction(float fraction)
        {
            int pixels = PixelUiScale.FilledPixels(fraction, _interior);
            _fill.sizeDelta = new Vector2(pixels, 0f);
        }

        public void SetColor(Color color) => _fill.GetComponent<UnityEngine.UI.Image>().color = color;

        private static RectTransform Block(Transform parent, string blockName, Color color)
        {
            var go = new GameObject(blockName, typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }
    }
}
