using System.Linq;
using System.Net;
using System.Text;

namespace Urpify.Reporting
{
    /// <summary>
    /// Self-contained HTML report: one file, inline styles, no scripts from the network, so it can be mailed or attached.
    /// Severity is shown by shape and text, never by colour alone.
    /// </summary>
    public static class ReportHtml
    {
        public static string Write(MigrationReport r)
        {
            var s = r.Summary;
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            sb.Append("<title>").Append(E(Loc.Get("report.title"))).Append(" - ").Append(E(r.ProjectName)).Append("</title><style>").Append(Css).Append("</style></head><body>");
            sb.Append("<header><h1>").Append(E(Loc.Get("report.title"))).Append("</h1><p class=\"meta\">")
              .Append(E(Loc.Get("report.meta", r.ProjectName, r.UnityVersion, string.IsNullOrEmpty(r.UrpVersion) ? Loc.Get("report.urpNotInstalled") : "URP " + r.UrpVersion, r.GeneratedUtc, r.KitVersion)))
              .Append("</p>");
            if (r.Cancelled) sb.Append("<p class=\"warn\">").Append(E(Loc.Get("report.cancelled"))).Append("</p>");
            sb.Append("</header><main>");

            // Lead: what happens to the scenes.
            sb.Append("<section class=\"lead\"><h2>").Append(E(Loc.Get("report.scenesHeading", s.ScenesTotal))).Append("</h2><div class=\"tiles\">");
            Tile(sb, Severity.Pink, s.ScenesPink, Loc.Get("summary.scenePink.tile"));
            Tile(sb, Severity.Silent, s.ScenesSilent, Loc.Get("summary.sceneSilent.tile"));
            Tile(sb, Severity.Degraded, s.ScenesDegraded, Loc.Get("summary.sceneDegraded.tile"));
            Tile(sb, Severity.Info, s.ScenesUnaffected, Loc.Get("summary.sceneUnaffected.tile"));
            sb.Append("</div><p class=\"note\">").Append(E(Loc.Get("ui.summary.overlap"))).Append(" ").Append(E(Loc.Get("report.severityNote"))).Append("</p>");
            sb.Append("<p>").Append(E(Loc.Get("ui.summary.assets", s.AssetsWithFindings))).Append(": ")
              .Append(E(Loc.Get("summary.assetPink", s.AssetsPink))).Append(" · ")
              .Append(E(Loc.Get("summary.assetSilent", s.AssetsSilent))).Append(" · ")
              .Append(E(Loc.Get("summary.assetDegraded", s.AssetsDegraded))).Append("</p>");
            if (r.CompareAfter.Length > 0)
                sb.Append("<p>").Append(E(Loc.Get("ui.summary.captured"))).Append(": ")
                  .Append(E(Loc.Get("summary.observedPink", s.ObservedPink))).Append(" · ")
                  .Append(E(Loc.Get("summary.observedChanged", s.ObservedChanged))).Append(" · ")
                  .Append(E(Loc.Get("summary.observedSame", s.ObservedSame))).Append("</p>");
            sb.Append("</section>");

            // Who fixes what, as the window says it (1.0.1): the JSON keeps the converters' tiers for programs.
            sb.Append("<section><h2>").Append(E(Loc.Get("report.fixHeading"))).Append("</h2><table class=\"kv\">");
            var owners = r.Assets.Where(a => a.Severity > Severity.Info || a.Tier != FixTier.None).Select(a => Edition.Owner(r, a)).ToList();
            foreach (var o in new[] { FixOwner.Unity, FixOwner.Free, FixOwner.Pro, FixOwner.Person })
                Row(sb, Loc.Get("owner." + o), owners.Count(x => x == o));
            sb.Append("</table><p>").Append(E(Loc.Get("summary.kitConverts", s.KitConvertible, s.KitNeeded))).Append("</p>");
            sb.Append("<p class=\"note\">").Append(E(Loc.Get("report.scanStats", r.FilesScanned, r.FilesFromCache, r.ScanSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), s.ParseFailures))).Append("</p></section>");

            if (r.Scenes.Count > 0)
            {
                sb.Append("<section><h2>").Append(E(Loc.Get("report.scenesTable"))).Append("</h2><div class=\"scroll\"><table><thead><tr><th>")
                  .Append(E(Loc.Get("column.scene"))).Append("</th><th>").Append(E(Loc.Get("column.verdict"))).Append("</th><th>")
                  .Append(E(Loc.Get("column.fix"))).Append("</th><th>").Append(E(Loc.Get("column.causes"))).Append("</th></tr></thead><tbody>");
                foreach (var sc in r.Scenes.OrderByDescending(x => x.Severity).ThenBy(x => x.Path))
                {
                    sb.Append("<tr><td class=\"path\">").Append(E(sc.Path)).Append("</td><td>").Append(Badge(sc.Severity)).Append("</td><td>")
                      .Append(E(sc.Severity == Severity.Info || Edition.Owner(sc.Tier, "") == FixOwner.None ? "" : Loc.Get("owner." + Edition.Owner(sc.Tier, "")))).Append("</td><td class=\"path\">")
                      .Append(E(string.Join(", ", sc.Causes.Take(6)))).Append(sc.Causes.Length > 6 ? E(Loc.Get("report.andMore", sc.Causes.Length - 6)) : "")
                      .Append("</td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }

            if (!string.IsNullOrEmpty(r.PackageFile)) PackageSection(sb, r);

            sb.Append("<section><h2>").Append(E(Loc.Get("report.findingsHeading", r.Findings.Count))).Append("</h2>");
            foreach (var group in r.Findings.GroupBy(f => f.AssetPath).OrderByDescending(g => g.Max(f => f.Severity)).ThenBy(g => g.Key))
            {
                var worst = group.Max(f => f.Severity);
                sb.Append("<details").Append(worst >= Severity.Silent ? " open" : "").Append("><summary>").Append(Badge(worst)).Append(" <span class=\"path\">")
                  .Append(E(group.Key)).Append("</span> <span class=\"count\">").Append(group.Count()).Append("</span></summary><ul>");
                foreach (var f in group)
                {
                    sb.Append("<li>").Append(Badge(f.Severity)).Append(" <strong>").Append(E(f.Title)).Append("</strong>");
                    if (f.Line > 0) sb.Append(" <span class=\"line\">").Append(E(Loc.Get("report.line", f.Line))).Append("</span>");
                    var owner = Edition.Owner(f);
                    if (owner != FixOwner.None) sb.Append(" <span class=\"tier\">").Append(E(Loc.Get("owner." + owner))).Append("</span>");
                    sb.Append("<p>").Append(E(f.Explanation)).Append("</p>");
                    if (!string.IsNullOrEmpty(f.ManualSteps)) sb.Append("<p class=\"manual\">").Append(E(f.ManualSteps)).Append("</p>");
                    if (!string.IsNullOrEmpty(f.Evidence)) sb.Append("<code>").Append(E(f.Evidence)).Append("</code>");
                    if (f.UsedBy.Length > 0)
                        sb.Append("<p class=\"note\">").Append(E(Loc.Get("report.usedBy", string.Join(", ", f.UsedBy.Take(8)) + (f.UsedBy.Length > 8 ? Loc.Get("report.andMore", f.UsedBy.Length - 8) : "")))).Append("</p>");
                    sb.Append("<p class=\"rule\">").Append(E(f.RuleId)).Append("</p></li>");
                }
                sb.Append("</ul></details>");
            }
            sb.Append("</section></main><footer>").Append(E(Loc.Get("report.footer", r.KitVersion, r.SchemaVersion))).Append("</footer></body></html>");
            return sb.ToString();
        }

        /// <summary>Scope = .unitypackage: the package, and what the converted export contains (Pro).</summary>
        static void PackageSection(StringBuilder sb, MigrationReport r)
        {
            var pe = r.PackageExport ?? new PackageExportSummary();
            sb.Append("<section><h2>").Append(E(Loc.Get("report.package.heading"))).Append("</h2><p class=\"path\">").Append(E(r.PackageFile)).Append("</p>");
            if (string.IsNullOrEmpty(pe.File)) { sb.Append("<p class=\"note\">").Append(E(Loc.Get("report.package.notExported"))).Append("</p></section>"); return; }
            int converted = pe.Shaders.Count(c => c.Outcome == "Auto" || c.Outcome == "AutoApprox" || c.Outcome == "LooksDifferent");
            sb.Append("<p>").Append(E(Loc.Get("report.package.exported", converted, pe.Shaders.Count, Loc.Get("ui.output." + pe.Output)))).Append("</p>")
              .Append("<p class=\"path\">").Append(E(pe.File)).Append("</p>")
              .Append("<p class=\"note\">").Append(E(Loc.Get("report.package.files", pe.Replaced, pe.Added))).Append("</p>");
            if (pe.UrpSettings.Count > 0)
            {
                sb.Append("<p>").Append(E(Loc.Get("report.package.urpSettings"))).Append("</p><ul>");
                foreach (var s in pe.UrpSettings) sb.Append("<li>").Append(E(Loc.Get("report.package.setting." + s))).Append("</li>");
                sb.Append("</ul>");
            }
            sb.Append("<div class=\"scroll\"><table><thead><tr><th>").Append(E(Loc.Get("column.shader"))).Append("</th><th>")
              .Append(E(Loc.Get("column.result"))).Append("</th><th>").Append(E(Loc.Get("column.notes"))).Append("</th></tr></thead><tbody>");
            foreach (var c in pe.Shaders.OrderBy(c => c.Outcome == "Auto" ? 3 : c.Outcome == "AutoApprox" ? 2 : c.Outcome == "LooksDifferent" ? 1 : 0).ThenBy(c => c.Path))
            {
                var notes = c.Notes.Where(n => !n.StartsWith("Info", System.StringComparison.Ordinal)).Take(3).ToList();
                sb.Append("<tr><td class=\"path\">").Append(E(c.Path)).Append("</td><td>").Append(E(Loc.Get("report.package.outcome." + c.Outcome))).Append("</td><td>")
                  .Append(E(string.Join(" ", notes))).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div></section>");
        }

        static void Tile(StringBuilder sb, Severity sev, int n, string label) =>
            sb.Append("<div class=\"tile sev-").Append(sev.ToString().ToLowerInvariant()).Append("\">").Append(Icon(sev))
              .Append("<span class=\"num\">").Append(n).Append("</span><span class=\"lbl\">").Append(E(label)).Append("</span></div>");

        static void Row(StringBuilder sb, string k, int v) => sb.Append("<tr><th>").Append(E(k)).Append("</th><td>").Append(v).Append("</td></tr>");

        static string Badge(Severity sev) =>
            "<span class=\"badge sev-" + sev.ToString().ToLowerInvariant() + "\">" + Icon(sev) + E(Loc.Get("severity." + sev)) + "</span>";

        /// <summary>Distinct shapes per severity: square, triangle, diamond, circle.</summary>
        static string Icon(Severity sev)
        {
            switch (sev)
            {
                case Severity.Pink: return "<svg class=\"ico\" viewBox=\"0 0 12 12\" aria-hidden=\"true\"><rect x=\"1\" y=\"1\" width=\"10\" height=\"10\" rx=\"1\" fill=\"currentColor\"/></svg>";
                case Severity.Silent: return "<svg class=\"ico\" viewBox=\"0 0 12 12\" aria-hidden=\"true\"><path d=\"M6 1 11 11H1Z\" fill=\"currentColor\"/></svg>";
                case Severity.Degraded: return "<svg class=\"ico\" viewBox=\"0 0 12 12\" aria-hidden=\"true\"><path d=\"M6 1 11 6 6 11 1 6Z\" fill=\"currentColor\"/></svg>";
                default: return "<svg class=\"ico\" viewBox=\"0 0 12 12\" aria-hidden=\"true\"><circle cx=\"6\" cy=\"6\" r=\"4.25\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\"/></svg>";
            }
        }

        static string E(string s) => WebUtility.HtmlEncode(s ?? "");

        const string Css = @"
:root{--bg:#f6f6f6;--fg:#1d1d1f;--muted:#5a5a60;--line:#d4d4d8;--card:#fff;--pink:#b0126a;--silent:#9a5b00;--degraded:#3d5a99;--info:#4b5563;}
@media (prefers-color-scheme:dark){:root{--bg:#1e1e1e;--fg:#e8e8ea;--muted:#a8a8b0;--line:#3a3a40;--card:#262629;--pink:#ff7cc3;--silent:#f5b041;--degraded:#8fb0ff;--info:#b4b8c0;}}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--fg);font:15px/1.5 system-ui,-apple-system,'Segoe UI',Inter,sans-serif}
header,main,footer{max-width:1100px;margin:0 auto;padding:16px}header h1{font-size:40px;line-height:1.15;margin:24px 0 4px;font-weight:700}
h2{font-size:20px;margin:0 0 12px;font-weight:600}.meta,.note{color:var(--muted)}.warn{color:var(--silent);font-weight:600}
section{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:20px;margin:0 0 16px}
.lead h2{font-size:24px}.tiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,180px),1fr));gap:12px}
.tile{border:1px solid var(--line);border-radius:6px;padding:14px;display:grid;grid-template-columns:auto 1fr;column-gap:10px;align-items:center}
.tile .num{font-size:36px;font-weight:700;line-height:1}.tile .lbl{grid-column:1/-1;color:var(--muted)}
.tile.sev-pink .num,.sev-pink{color:var(--pink)}.sev-silent{color:var(--silent)}.sev-degraded{color:var(--degraded)}.sev-info{color:var(--info)}
.ico{width:14px;height:14px;vertical-align:-2px;margin-right:4px;flex:none}.tile .ico{width:22px;height:22px}
.badge{white-space:nowrap;font-weight:600;font-size:13px}table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:6px 8px;border-bottom:1px solid var(--line);vertical-align:top}
td.path:first-child{min-width:min(34ch,40vw)}.kv{width:auto}.kv td{text-align:right;font-variant-numeric:tabular-nums}.scroll{overflow-x:auto}
.path{font-family:ui-monospace,'JetBrains Mono',Consolas,monospace;font-size:13px;overflow-wrap:anywhere}
details{border-top:1px solid var(--line);padding:8px 0}summary{cursor:pointer;display:flex;gap:8px;align-items:baseline;flex-wrap:wrap}
summary:focus-visible{outline:2px solid var(--degraded);outline-offset:2px}.count{color:var(--muted);font-size:13px}
ul{list-style:none;padding:0 0 0 12px;margin:8px 0}li{padding:8px 0;border-bottom:1px dashed var(--line)}li p{margin:4px 0;max-width:75ch}
code{display:block;font-family:ui-monospace,Consolas,monospace;font-size:12px;background:var(--bg);padding:6px 8px;border-radius:4px;overflow-wrap:anywhere;white-space:pre-wrap}
.line,.tier,.rule{color:var(--muted);font-size:13px}.rule{font-family:ui-monospace,Consolas,monospace;font-size:11px}.manual{border-left:3px solid var(--silent);padding-left:8px}
footer{color:var(--muted);font-size:13px;padding-bottom:32px}";
    }
}
