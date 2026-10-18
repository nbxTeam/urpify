using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Urpify.UI
{
    /// <summary>
    /// Before/after view of one captured frame: a curtain slider (before on the left, after on the right) and a
    /// difference-map toggle. Textures are loaded from the capture PNGs and released when the view is removed.
    /// </summary>
    public sealed class CompareView : VisualElement
    {
        readonly Texture2D before, after, diff;
        readonly VisualElement frame, curtain;
        readonly Image afterImage, beforeImage, diffImage;
        float split = 0.5f;

        readonly bool ownsTextures;

        public CompareView(string beforePath, string afterPath, string diffPath)
            : this(Load(beforePath), Load(afterPath), Load(diffPath), true, null) { }

        /// <summary>Compares textures the caller owns (live previews); they are not released with the view.</summary>
        public CompareView(Texture2D before, Texture2D after, Texture2D diff, string legend = null) : this(before, after, diff, false, legend) { }

        CompareView(Texture2D before, Texture2D after, Texture2D diff, bool owns, string legend)
        {
            AddToClassList("urpify-compare");
            this.before = before;
            this.after = after;
            this.diff = diff;
            ownsTextures = owns;

            frame = new VisualElement();
            frame.AddToClassList("urpify-compare-frame");
            afterImage = new Image { image = after, scaleMode = ScaleMode.ScaleToFit };
            afterImage.AddToClassList("urpify-compare-image");
            frame.Add(afterImage);
            curtain = new VisualElement();
            curtain.AddToClassList("urpify-compare-curtain");
            beforeImage = new Image { image = before, scaleMode = ScaleMode.ScaleToFit };
            beforeImage.AddToClassList("urpify-compare-image");
            curtain.Add(beforeImage);
            frame.Add(curtain);
            diffImage = new Image { image = diff, scaleMode = ScaleMode.ScaleToFit };
            diffImage.AddToClassList("urpify-compare-image");
            diffImage.style.display = DisplayStyle.None;
            frame.Add(diffImage);
            // Height follows the width (style.aspectRatio does not exist in Unity 2022.3).
            aspect = before != null && before.width > 0 ? (float)before.height / before.width : 9f / 16f;
            frame.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            Add(frame);

            var controls = new VisualElement();
            controls.AddToClassList("urpify-compare-controls");
            // Which side is which: in the tooltip of the image, not as a line under every comparison.
            frame.tooltip = legend ?? Loc.Get("ui.compare.labels");
            var slider = new Slider(Loc.Get("ui.compare.curtain"), 0, 1) { value = split };
            slider.AddToClassList("urpify-compare-slider");
            slider.RegisterValueChangedCallback(e => { split = e.newValue; Layout(); });
            var toggle = new Toggle(Loc.Get("ui.compare.diff")) { value = false };
            toggle.SetEnabled(diff != null);
            toggle.RegisterValueChangedCallback(e =>
            {
                diffImage.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                curtain.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                afterImage.style.display = e.newValue ? DisplayStyle.None : DisplayStyle.Flex;
                slider.SetEnabled(!e.newValue);
            });
            controls.Add(slider);
            controls.Add(toggle);
            Add(controls);
            RegisterCallback<DetachFromPanelEvent>(_ => Release());
        }

        float aspect;

        void Layout()
        {
            var w = frame.contentRect.width;
            var h = Mathf.Round(w * aspect);
            if (float.IsNaN(w) || w <= 0) return;
            if (Mathf.Abs(frame.resolvedStyle.height - h) > 0.5f) frame.style.height = h;
            curtain.style.width = w * split;
            beforeImage.style.width = w;
        }

        static Texture2D Load(string path) => Verify.CaptureRunner.LoadFrame(path);

        void Release()
        {
            if (!ownsTextures) return;
            foreach (var t in new[] { before, after, diff })
                if (t != null) Object.DestroyImmediate(t);
        }
    }
}
