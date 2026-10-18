using UnityEngine;
using UnityEngine.UIElements;
using Urpify.Pipeline;

namespace Urpify.UI
{
    /// <summary>
    /// Step status marker, one shape per status so it never relies on colour: Done = tick, Not run = hollow circle,
    /// Running = open arc, Failed = cross, Skipped = dash, Rolled back = arrow turning back, Interrupted = pause bars.
    /// Colour comes from the USS class; the status is also written as text next to it.
    /// </summary>
    public sealed class StatusIcon : VisualElement
    {
        readonly StepStatus status;

        public StatusIcon(StepStatus status)
        {
            this.status = status;
            AddToClassList("urpify-icon");
            AddToClassList(Class(status));
            pickingMode = PickingMode.Ignore;
            tooltip = Loc.Get("status." + status);
            generateVisualContent += Draw;
        }

        public static string Class(StepStatus s)
        {
            switch (s)
            {
                case StepStatus.Done: return "urpify-status-done";
                case StepStatus.Failed: return "urpify-status-failed";
                case StepStatus.Interrupted: case StepStatus.RolledBack: case StepStatus.Running: return "urpify-status-attention";
                default: return "urpify-status-idle";
            }
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float w = r.width, h = r.height;
            if (w <= 0 || h <= 0) return;
            var p = ctx.painter2D;
            var c = resolvedStyle.color;
            p.fillColor = c;
            p.strokeColor = c;
            p.lineWidth = Mathf.Max(1.5f, w * 0.16f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            var mid = new Vector2(w * 0.5f, h * 0.5f);
            float rad = w * 0.4f;
            p.BeginPath();
            switch (status)
            {
                case StepStatus.Done:
                    p.MoveTo(new Vector2(w * 0.12f, h * 0.55f));
                    p.LineTo(new Vector2(w * 0.4f, h * 0.82f));
                    p.LineTo(new Vector2(w * 0.9f, h * 0.2f));
                    p.Stroke();
                    break;
                case StepStatus.Failed:
                    p.MoveTo(new Vector2(w * 0.18f, h * 0.18f));
                    p.LineTo(new Vector2(w * 0.82f, h * 0.82f));
                    p.MoveTo(new Vector2(w * 0.82f, h * 0.18f));
                    p.LineTo(new Vector2(w * 0.18f, h * 0.82f));
                    p.Stroke();
                    break;
                case StepStatus.Skipped:
                    p.MoveTo(new Vector2(w * 0.15f, h * 0.5f));
                    p.LineTo(new Vector2(w * 0.85f, h * 0.5f));
                    p.Stroke();
                    break;
                case StepStatus.Running:
                    p.Arc(mid, rad, 0f, 270f);
                    p.Stroke();
                    break;
                case StepStatus.RolledBack:
                    // Arc from the right round the top to the left, arrow head pointing down on the left.
                    p.Arc(new Vector2(w * 0.5f, h * 0.58f), rad * 0.9f, 0f, -180f, ArcDirection.CounterClockwise);
                    p.Stroke();
                    p.BeginPath();
                    p.MoveTo(new Vector2(w * 0.02f, h * 0.45f));
                    p.LineTo(new Vector2(w * 0.14f, h * 0.72f));
                    p.LineTo(new Vector2(w * 0.3f, h * 0.48f));
                    p.Stroke();
                    break;
                case StepStatus.Interrupted:
                    p.MoveTo(new Vector2(w * 0.32f, h * 0.15f));
                    p.LineTo(new Vector2(w * 0.32f, h * 0.85f));
                    p.MoveTo(new Vector2(w * 0.68f, h * 0.15f));
                    p.LineTo(new Vector2(w * 0.68f, h * 0.85f));
                    p.Stroke();
                    break;
                default: // Not run
                    p.Arc(mid, rad, 0f, 360f);
                    p.Stroke();
                    break;
            }
        }
    }
}
