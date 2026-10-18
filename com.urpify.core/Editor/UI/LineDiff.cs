using System;
using System.Collections.Generic;

namespace Urpify.UI
{
    /// <summary>Line-level diff (longest common subsequence) for the shader panel's side-by-side code view.</summary>
    public static class LineDiff
    {
        /// <summary>For each line of a and b: true when it has no counterpart in the other text.</summary>
        public static (bool[] removed, bool[] added) Compare(IList<string> a, IList<string> b)
        {
            int n = a.Count, m = b.Count;
            // Trim common head and tail first: conversions change a minority of lines.
            int head = 0;
            while (head < n && head < m && a[head] == b[head]) head++;
            int tail = 0;
            while (tail < n - head && tail < m - head && a[n - 1 - tail] == b[m - 1 - tail]) tail++;
            var removed = new bool[n];
            var added = new bool[m];
            int an = n - head - tail, bm = m - head - tail;
            if (an == 0 || bm == 0)
            {
                for (int i = 0; i < an; i++) removed[head + i] = true;
                for (int j = 0; j < bm; j++) added[head + j] = true;
                return (removed, added);
            }
            if ((long)an * bm > 25_000_000)
            {
                // Too large for the table: mark the whole middle as changed.
                for (int i = 0; i < an; i++) removed[head + i] = true;
                for (int j = 0; j < bm; j++) added[head + j] = true;
                return (removed, added);
            }
            var lcs = new int[an + 1, bm + 1];
            for (int i = an - 1; i >= 0; i--)
                for (int j = bm - 1; j >= 0; j--)
                    lcs[i, j] = string.Equals(a[head + i].Trim(), b[head + j].Trim(), StringComparison.Ordinal)
                        ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            int x = 0, y = 0;
            while (x < an && y < bm)
            {
                if (string.Equals(a[head + x].Trim(), b[head + y].Trim(), StringComparison.Ordinal)) { x++; y++; }
                else if (lcs[x + 1, y] >= lcs[x, y + 1]) { removed[head + x] = true; x++; }
                else { added[head + y] = true; y++; }
            }
            for (; x < an; x++) removed[head + x] = true;
            for (; y < bm; y++) added[head + y] = true;
            return (removed, added);
        }
    }
}
