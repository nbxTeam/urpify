using UnityEngine;
using UnityEngine.UIElements;

namespace Urpify.UI
{
    /// <summary>
    /// Severity marker drawn as a vector shape so colour-blind users can tell them apart:
    /// Pink = square, Silent = triangle, Degraded = diamond, Info = hollow circle. Colour comes from the USS class.
    /// </summary>
    public sealed class SeverityIcon : VisualElement
    {
        Severity severity;

        public SeverityIcon(Severity severity)
        {
            AddToClassList("urpify-icon");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            Set(severity);
        }

        public void Set(Severity s)
        {
            RemoveFromClassList(Class(severity));
            severity = s;
            AddToClassList(Class(s));
            tooltip = Loc.Get("severity." + s);
            MarkDirtyRepaint();
        }

        public static string Class(Severity s) => "urpify-sev-" + s.ToString().ToLowerInvariant();

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            var p = ctx.painter2D;
            var c = resolvedStyle.color;
            p.fillColor = c;
            p.strokeColor = c;
            p.lineWidth = Mathf.Max(1.2f, w * 0.14f);
            p.BeginPath();
            switch (severity)
            {
                case Severity.Pink:
                    p.MoveTo(new Vector2(w * 0.08f, h * 0.08f));
                    p.LineTo(new Vector2(w * 0.92f, h * 0.08f));
                    p.LineTo(new Vector2(w * 0.92f, h * 0.92f));
                    p.LineTo(new Vector2(w * 0.08f, h * 0.92f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Severity.Silent:
                    p.MoveTo(new Vector2(w * 0.5f, h * 0.05f));
                    p.LineTo(new Vector2(w * 0.97f, h * 0.95f));
                    p.LineTo(new Vector2(w * 0.03f, h * 0.95f));
                    p.ClosePath();
                    p.Fill();
                    break;
                case Severity.Degraded:
                    p.MoveTo(new Vector2(w * 0.5f, h * 0.03f));
                    p.LineTo(new Vector2(w * 0.97f, h * 0.5f));
                    p.LineTo(new Vector2(w * 0.5f, h * 0.97f));
                    p.LineTo(new Vector2(w * 0.03f, h * 0.5f));
                    p.ClosePath();
                    p.Fill();
                    break;
                default:
                    p.Arc(new Vector2(w * 0.5f, h * 0.5f), w * 0.36f, 0f, 360f);
                    p.Stroke();
                    break;
            }
        }
    }
}
