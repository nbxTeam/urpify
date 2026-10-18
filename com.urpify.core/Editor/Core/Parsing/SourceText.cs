using System;
using System.Collections.Generic;

namespace Urpify.Parsing
{
    /// <summary>Immutable source text with offset-to-line mapping. Offsets are char indices into <see cref="Text"/>.</summary>
    public sealed class SourceText
    {
        public readonly string Text;
        public readonly string Path;
        readonly int[] lineStarts;

        public SourceText(string text, string path = "")
        {
            Text = text ?? "";
            Path = path ?? "";
            var starts = new List<int> { 0 };
            for (int i = 0; i < Text.Length; i++)
                if (Text[i] == '\n') starts.Add(i + 1);
            lineStarts = starts.ToArray();
        }

        public int Length => Text.Length;
        public int LineCount => lineStarts.Length;

        /// <summary>1-based line of an offset.</summary>
        public int LineOf(int offset)
        {
            if (offset <= 0) return 1;
            int i = Array.BinarySearch(lineStarts, offset);
            if (i < 0) i = ~i - 1;
            return i + 1;
        }

        /// <summary>Text of a 1-based line without the line break.</summary>
        public string LineText(int line)
        {
            if (line < 1 || line > lineStarts.Length) return "";
            int s = lineStarts[line - 1];
            int e = line < lineStarts.Length ? lineStarts[line] : Text.Length;
            return Text.Substring(s, e - s).TrimEnd('\r', '\n');
        }

        public string Slice(int start, int end) =>
            start < 0 || end > Text.Length || end < start ? "" : Text.Substring(start, end - start);
    }

    /// <summary>A half-open range [Start, End) in a <see cref="SourceText"/>.</summary>
    [Serializable]
    public struct Span
    {
        public int Start, End;
        public Span(int start, int end) { Start = start; End = end; }
        public int Length => End - Start;
        public bool Contains(int offset) => offset >= Start && offset < End;
        public override string ToString() => $"[{Start},{End})";
    }

    /// <summary>A problem found while parsing. Parsing never throws; it records these and continues.</summary>
    [Serializable]
    public sealed class ParseError
    {
        public string Message;
        public int Line;
        public ParseError(string message, int line) { Message = message; Line = line; }
        public override string ToString() => $"line {Line}: {Message}";
    }
}
