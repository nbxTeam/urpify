using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Urpify.Reporting
{
    /// <summary>
    /// Writes <see cref="MigrationReport"/> as JSON with enums as strings. The schema is documented in
    /// Documentation~/report-schema.md; bump <see cref="MigrationReport.CurrentSchemaVersion"/> on any breaking change.
    /// </summary>
    public static class ReportJson
    {
        public static string Write(MigrationReport r)
        {
            var w = new Writer();
            w.Open('{');
            w.Prop("schemaVersion", r.SchemaVersion);
            w.Prop("generatedUtc", r.GeneratedUtc);
            w.Prop("unityVersion", r.UnityVersion);
            w.Prop("urpVersion", r.UrpVersion);
            w.Prop("kitVersion", r.KitVersion);
            w.Prop("projectName", r.ProjectName);
            w.Prop("scope", r.Scope);
            w.Prop("scopePaths", r.ScopePaths);
            w.Prop("scanSeconds", r.ScanSeconds);
            w.Prop("filesScanned", r.FilesScanned);
            w.Prop("filesFromCache", r.FilesFromCache);
            w.Prop("cancelled", r.Cancelled);
            w.Prop("timings", r.Timings.ToArray());
            w.Prop("compareBefore", r.CompareBefore);
            w.Prop("compareAfter", r.CompareAfter);
            w.Prop("packageFile", r.PackageFile);
            var pe = r.PackageExport ?? new PackageExportSummary();
            w.Key("packageExport"); w.Open('{');
            w.Prop("file", pe.File); w.Prop("output", pe.Output); w.Prop("replaced", pe.Replaced); w.Prop("added", pe.Added);
            w.Prop("urpSettings", pe.UrpSettings.ToArray());
            w.Key("shaders"); w.Open('[');
            foreach (var c in pe.Shaders)
            {
                w.Open('{');
                w.Prop("path", c.Path); w.Prop("converter", c.Converter); w.Prop("outcome", c.Outcome); w.Prop("notes", c.Notes.ToArray());
                w.Close('}');
            }
            w.Close(']');
            w.Close('}');

            var s = r.Summary;
            w.Key("summary"); w.Open('{');
            w.Key("scenes"); w.Open('{');
            w.Prop("total", s.ScenesTotal); w.Prop("pink", s.ScenesPink); w.Prop("silent", s.ScenesSilent);
            w.Prop("degraded", s.ScenesDegraded); w.Prop("unaffected", s.ScenesUnaffected);
            w.Close('}');
            w.Key("assets"); w.Open('{');
            w.Prop("withFindings", s.AssetsWithFindings); w.Prop("pink", s.AssetsPink); w.Prop("silent", s.AssetsSilent);
            w.Prop("degraded", s.AssetsDegraded); w.Prop("info", s.AssetsInfo);
            w.Close('}');
            w.Key("fixTiers"); w.Open('{');
            w.Prop("unityConverter", s.TierUnityConverter); w.Prop("auto", s.TierAuto); w.Prop("autoApprox", s.TierAutoApprox); w.Prop("review", s.TierReview); w.Prop("notSupportedYet", s.TierNotSupportedYet); w.Prop("manual", s.TierManual);
            w.Close('}');
            w.Key("observed"); w.Open('{');
            w.Prop("pink", s.ObservedPink); w.Prop("changed", s.ObservedChanged); w.Prop("same", s.ObservedSame); w.Prop("notCaptured", s.ObservedNotCaptured);
            w.Close('}');
            w.Prop("kitConvertible", s.KitConvertible);
            w.Prop("kitNeeded", s.KitNeeded);
            w.Prop("parseFailures", s.ParseFailures);
            w.Close('}');

            w.Key("scenes"); w.Open('[');
            foreach (var sc in r.Scenes)
            {
                w.Open('{');
                w.Prop("path", sc.Path); w.Prop("severity", sc.Severity.ToString()); w.Prop("fixTier", sc.Tier.ToString());
                w.Prop("causes", sc.Causes); w.Prop("rules", sc.Rules); w.Prop("breaks", sc.Breaks);
                w.Prop("observed", sc.Observed.ToString()); w.Prop("pinkNotInFrame", sc.PinkNotInFrame);
                w.Prop("observedNote", sc.ObservedNote ?? ""); w.Prop("renderErrors", sc.RenderErrors ?? Array.Empty<string>());
                w.Key("viewpoints"); w.Open('[');
                foreach (var v in sc.Viewpoints)
                {
                    w.Open('{');
                    w.Prop("name", v.Name); w.Prop("verdict", v.Verdict.ToString());
                    w.Prop("pinkBeforePct", v.PinkBeforePct); w.Prop("pinkAfterPct", v.PinkAfterPct);
                    w.Prop("meanDiff", v.MeanDiff); w.Prop("changedPixelsPct", v.ChangedPixelsPct); w.Prop("largestAreaPct", v.LargestAreaPct);
                    w.Prop("before", v.Before); w.Prop("after", v.After); w.Prop("diff", v.Diff);
                    w.Close('}');
                }
                w.Close(']');
                w.Close('}');
            }
            w.Close(']');

            w.Key("assets"); w.Open('[');
            foreach (var a in r.Assets)
            {
                w.Open('{');
                w.Prop("path", a.Path); w.Prop("kind", a.Kind.ToString()); w.Prop("severity", a.Severity.ToString());
                w.Prop("fixTier", a.Tier.ToString()); w.Prop("findingCount", a.FindingCount); w.Prop("usedBy", a.UsedBy);
                w.Close('}');
            }
            w.Close(']');

            w.Key("findings"); w.Open('[');
            foreach (var f in r.Findings)
            {
                w.Open('{');
                w.Prop("ruleId", f.RuleId); w.Prop("severity", f.Severity.ToString()); w.Prop("fixTier", f.Tier.ToString());
                w.Prop("kind", f.Kind.ToString()); w.Prop("assetPath", f.AssetPath); w.Prop("line", f.Line);
                w.Prop("title", f.Title); w.Prop("explanation", f.Explanation); w.Prop("manualSteps", f.ManualSteps);
                w.Prop("evidence", f.Evidence); w.Prop("converterId", f.ConverterId); w.Prop("catalogId", f.CatalogId);
                w.Prop("usedBy", f.UsedBy);
                w.Close('}');
            }
            w.Close(']');
            w.Close('}');
            return w.ToString();
        }

        sealed class Writer
        {
            readonly StringBuilder sb = new StringBuilder();
            readonly Stack<bool> first = new Stack<bool>();
            int indent;
            bool afterKey;

            public void Open(char c)
            {
                Separator();
                sb.Append(c);
                first.Push(true);
                indent++;
            }

            public void Close(char c)
            {
                indent--;
                bool empty = first.Pop();
                if (!empty) NewLine();
                sb.Append(c);
            }

            public void Key(string k)
            {
                Separator();
                Str(k);
                sb.Append(": ");
                afterKey = true;
            }

            void Separator()
            {
                if (afterKey) { afterKey = false; return; }
                if (first.Count == 0) return;
                if (!first.Peek()) sb.Append(',');
                first.Pop(); first.Push(false);
                NewLine();
            }

            void NewLine() { sb.Append('\n'); sb.Append(' ', indent * 2); }

            public void Prop(string k, string v) { Key(k); Value(v); }
            public void Prop(string k, int v) { Key(k); Raw(v.ToString(CultureInfo.InvariantCulture)); }
            public void Prop(string k, double v) { Key(k); Raw(v.ToString("0.###", CultureInfo.InvariantCulture)); }
            public void Prop(string k, bool v) { Key(k); Raw(v ? "true" : "false"); }

            public void Prop(string k, string[] v)
            {
                Key(k);
                afterKey = false;
                sb.Append('[');
                for (int i = 0; i < (v?.Length ?? 0); i++) { if (i > 0) sb.Append(", "); Str(v[i]); }
                sb.Append(']');
            }

            void Value(string v) { afterKey = false; Str(v); }
            void Raw(string v) { afterKey = false; sb.Append(v); }

            void Str(string s)
            {
                sb.Append('"');
                foreach (var c in s ?? "")
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default:
                            if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                            else sb.Append(c);
                            break;
                    }
                }
                sb.Append('"');
            }

            public override string ToString() => sb.ToString() + "\n";
        }
    }
}
