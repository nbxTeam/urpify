using System;
using System.Collections.Generic;
using UnityEngine;

namespace Urpify.Verify
{
    /// <summary>
    /// Thresholds for the observed verdict, calibrated on the lab's Standard and Legacy booths converted to URP Lit by
    /// Unity's converter (TZ section 10: the reference of acceptable difference). At 48 per pixel those booths change at
    /// most 3.8 % of their frame (TER01); silent breaks change 8-21 % (C01, D01, G02, LEG03, STD04). STATUS.md, M2.
    /// </summary>
    [Serializable]
    public sealed class CompareThresholds
    {
        /// <summary>Pink share growth that counts as Pink, percent of the frame (TZ section 10: +0.02 %).</summary>
        public double PinkGrowthPct = 0.02;
        /// <summary>Largest channel difference (0..255) above which a pixel counts as changed.</summary>
        public int PixelThreshold = 48;
        /// <summary>Share of changed pixels above which the frame is Changed, percent.</summary>
        public double ChangedPixelsPct = 5.0;
        /// <summary>
        /// Largest connected changed area (8x8 blocks, a block counts when a quarter of its pixels changed) above which the
        /// frame is Changed, percent of the frame (QA-009). Calibrated 2026-10-04: frames that are the same after the
        /// migration (lab, ShaderTutorials) stay at or under 0.67 %; lost SSR on kode80SSR 3.2 %, ShaderTutorials 022 2.7 %,
        /// UnityPCSS foliage 1.44 %, ShaderTutorials 033 river glow 1.12 %, all of them visible and all under 5 % of pixels.
        /// </summary>
        public double LargestAreaPct = 1.0;
    }

    public struct CompareResult
    {
        public double PinkBeforePct, PinkAfterPct, MeanDiff, ChangedPixelsPct, LargestAreaPct;
        public int Width, Height;
        public ObservedVerdict Verdict;
    }

    /// <summary>
    /// Before/after image metrics (TZ section 10): pink share, mean difference, share of changed pixels, difference map.
    /// Works on raw pixels so it can be tested without rendering.
    /// </summary>
    public static class ImageCompare
    {
        /// <summary>
        /// The error shader's colour: it is unlit and writes exactly (1, 0, 1). A looser test (r, b above 200, g below 60, the M0
        /// probe) counted a scene's own magenta objects: ShaderTutorials 048 has balls of (207, 0, 230) and similar, and
        /// a change of their visible area read as new pink. On 37 pink scenes of the real projects the exact test gives
        /// the same verdicts.
        /// </summary>
        public static bool IsPink(Color32 c) => c.r >= 250 && c.g <= 5 && c.b >= 250;

        public static double PinkPct(Color32[] px)
        {
            if (px == null || px.Length == 0) return 0;
            int n = 0;
            for (int i = 0; i < px.Length; i++) if (IsPink(px[i])) n++;
            return 100.0 * n / px.Length;
        }

        /// <param name="diffMap">When not null and the same length, receives a heat map of the differences.</param>
        public static CompareResult Compare(Color32[] before, Color32[] after, int width, int height, CompareThresholds t, Color32[] diffMap = null)
        {
            if (before == null || after == null) throw new ArgumentNullException(before == null ? nameof(before) : nameof(after));
            if (before.Length != after.Length || before.Length != width * height)
                throw new ArgumentException($"Image sizes differ: {before.Length} vs {after.Length} pixels, expected {width}x{height}.");
            long sum = 0;
            int changed = 0, pinkB = 0, pinkA = 0;
            var mask = new bool[before.Length];
            bool map = diffMap != null && diffMap.Length == before.Length;
            for (int i = 0; i < before.Length; i++)
            {
                var a = before[i];
                var b = after[i];
                int dr = Math.Abs(a.r - b.r), dg = Math.Abs(a.g - b.g), db = Math.Abs(a.b - b.b);
                sum += dr + dg + db;
                int m = Math.Max(dr, Math.Max(dg, db));
                if (m > t.PixelThreshold) { changed++; mask[i] = true; }
                if (IsPink(a)) pinkB++;
                if (IsPink(b)) pinkA++;
                if (map) diffMap[i] = Heat(a, m, t.PixelThreshold);
            }
            var r = new CompareResult
            {
                Width = width,
                Height = height,
                MeanDiff = sum / (3.0 * before.Length),
                ChangedPixelsPct = 100.0 * changed / before.Length,
                PinkBeforePct = 100.0 * pinkB / before.Length,
                PinkAfterPct = 100.0 * pinkA / before.Length,
                LargestAreaPct = LargestArea(mask, width, height),
            };
            r.Verdict = Verdict(r, t);
            return r;
        }

        public static ObservedVerdict Verdict(CompareResult r, CompareThresholds t)
        {
            if (r.PinkAfterPct - r.PinkBeforePct > t.PinkGrowthPct) return ObservedVerdict.Pink;
            if (r.ChangedPixelsPct > t.ChangedPixelsPct || r.LargestAreaPct > t.LargestAreaPct) return ObservedVerdict.Changed;
            return ObservedVerdict.Same;
        }

        public const int Block = 8;

        /// <summary>
        /// The largest 4-connected area of changed 8x8 blocks, percent of the frame's blocks. A block counts when a quarter
        /// of its pixels changed, so noise (dithering, grain) spread over the frame does not add up to an area.
        /// </summary>
        public static double LargestArea(bool[] changed, int width, int height)
        {
            int gw = width / Block, gh = height / Block;
            if (gw == 0 || gh == 0) return 0;
            var blocks = new bool[gw * gh];
            for (int by = 0; by < gh; by++)
                for (int bx = 0; bx < gw; bx++)
                {
                    int n = 0;
                    for (int y = by * Block; y < by * Block + Block; y++)
                        for (int x = bx * Block; x < bx * Block + Block; x++)
                            if (changed[y * width + x]) n++;
                    blocks[by * gw + bx] = n > Block * Block / 4;
                }
            var seen = new bool[blocks.Length];
            var stack = new Stack<int>();
            int best = 0;
            for (int s = 0; s < blocks.Length; s++)
            {
                if (!blocks[s] || seen[s]) continue;
                int size = 0;
                seen[s] = true; stack.Push(s);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(); size++;
                    int cx = c % gw, cy = c / gw;
                    if (cx > 0 && blocks[c - 1] && !seen[c - 1]) { seen[c - 1] = true; stack.Push(c - 1); }
                    if (cx < gw - 1 && blocks[c + 1] && !seen[c + 1]) { seen[c + 1] = true; stack.Push(c + 1); }
                    if (cy > 0 && blocks[c - gw] && !seen[c - gw]) { seen[c - gw] = true; stack.Push(c - gw); }
                    if (cy < gh - 1 && blocks[c + gw] && !seen[c + gw]) { seen[c + gw] = true; stack.Push(c + gw); }
                }
                if (size > best) best = size;
            }
            return 100.0 * best / blocks.Length;
        }

        /// <summary>Dimmed grey copy of the "before" frame with changed pixels in yellow-to-red by magnitude.</summary>
        static Color32 Heat(Color32 before, int diff, int threshold)
        {
            if (diff <= threshold)
            {
                byte g = (byte)((before.r + before.g + before.b) / 9);
                return new Color32(g, g, g, 255);
            }
            float k = Mathf.Clamp01((diff - threshold) / (255f - threshold));
            return new Color32(255, (byte)(220 * (1 - k)), 0, 255);
        }

        /// <summary>
        /// Share of the frame, percent, in its most common colour (3 bits per channel): 100 for a frame of one flat colour. A
        /// frame a broken effect blacked out or blanked is above <see cref="FlatPct"/> (QA-030 black, QA-025 white); a scene
        /// of sky and ground is far under it.
        /// </summary>
        public static double FlatShare(Color32[] px)
        {
            if (px == null || px.Length == 0) return 0;
            var bins = new int[512];
            int best = 0;
            for (int i = 0; i < px.Length; i++)
            {
                int b = ((px[i].r >> 5) << 6) | ((px[i].g >> 5) << 3) | (px[i].b >> 5);
                if (++bins[b] > best) best = bins[b];
            }
            return 100.0 * best / px.Length;
        }

        /// <summary>A frame at least this flat after the migration, and not flat before, is a frame something blanked out.</summary>
        public const double FlatPct = 97.0;
        /// <summary>The frame before must be under this to call the change "went flat".</summary>
        public const double NotFlatBeforePct = 90.0;

        public static bool WentFlat(double before, double after) => after >= FlatPct && before < NotFlatBeforePct;

        /// <summary>Worst of several verdicts (Pink over Changed over Same).</summary>
        public static ObservedVerdict Worst(ObservedVerdict a, ObservedVerdict b) => (int)a > (int)b ? a : b;
    }
}
