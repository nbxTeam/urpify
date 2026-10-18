using System;
using System.IO;
using System.Text;

namespace Urpify.Generation
{
    /// <summary>
    /// Reads and writes the user's source files (shaders, include files, scripts) without spoiling text that is not UTF-8
    /// (code review R-03: File.ReadAllText turned every byte of a Windows-1251, Shift-JIS or GBK comment or string into
    /// U+FFFD, and the file written back, even "the original is back", kept the damage). A file with a byte order mark keeps
    /// it; valid UTF-8 stays UTF-8; anything else is read one character per byte, so whatever a converter does not change is
    /// written back byte for byte. Converters change only ASCII code, so such text never passes through a code page guess.
    /// </summary>
    public static class SourceFiles
    {
        public enum Kind { Utf8, Utf8Bom, Utf16LE, Utf16BE, Bytes }

        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static string Read(string path) => Read(path, out _);

        public static string Read(string path, out Kind kind) => Decode(File.ReadAllBytes(path), out kind);

        /// <summary>How the file at <paramref name="path"/> is written; UTF-8 when there is no such file.</summary>
        public static Kind KindOf(string path)
        {
            try { return File.Exists(path) ? KindOf(File.ReadAllBytes(path)) : Kind.Utf8; }
            catch (Exception) { return Kind.Utf8; }
        }

        public static Kind KindOf(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Kind.Utf8Bom;
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Kind.Utf16LE;
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Kind.Utf16BE;
            try { StrictUtf8.GetCharCount(b); return Kind.Utf8; }
            catch (DecoderFallbackException) { return Kind.Bytes; }
        }

        public static string Decode(byte[] b, out Kind kind)
        {
            kind = KindOf(b);
            switch (kind)
            {
                case Kind.Utf8Bom: return StrictOrLoose(b, 3);
                case Kind.Utf16LE: return Encoding.Unicode.GetString(b, 2, b.Length - 2);
                case Kind.Utf16BE: return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
                case Kind.Bytes:
                    var c = new char[b.Length];
                    for (int i = 0; i < b.Length; i++) c[i] = (char)b[i];
                    return new string(c);
                default: return StrictUtf8.GetString(b);
            }
        }

        // A file with a UTF-8 mark is UTF-8 by its own word; invalid bytes in it are rare and Unity reads them the same way.
        static string StrictOrLoose(byte[] b, int skip)
        {
            try { return StrictUtf8.GetString(b, skip, b.Length - skip); }
            catch (DecoderFallbackException) { return Encoding.UTF8.GetString(b, skip, b.Length - skip); }
        }

        /// <exception cref="NotUtf8Exception">A file read one byte per character gets a character above U+00FF.</exception>
        public static byte[] Encode(string text, Kind kind, string path = "")
        {
            switch (kind)
            {
                case Kind.Utf8Bom: return Concat(new byte[] { 0xEF, 0xBB, 0xBF }, new UTF8Encoding(false).GetBytes(text));
                case Kind.Utf16LE: return Concat(new byte[] { 0xFF, 0xFE }, Encoding.Unicode.GetBytes(text));
                case Kind.Utf16BE: return Concat(new byte[] { 0xFE, 0xFF }, Encoding.BigEndianUnicode.GetBytes(text));
                case Kind.Bytes:
                    var b = new byte[text.Length];
                    for (int i = 0; i < text.Length; i++)
                    {
                        if (text[i] > 0xFF) throw new NotUtf8Exception(path);
                        b[i] = (byte)text[i];
                    }
                    return b;
                default: return new UTF8Encoding(false).GetBytes(text);
            }
        }

        static byte[] Concat(byte[] a, byte[] b)
        {
            var r = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, r, 0, a.Length);
            Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
            return r;
        }

        /// <summary>Writes <paramref name="text"/> the way <paramref name="kind"/> says (the kind the original was read with).</summary>
        public static void Write(string path, string text, Kind kind) => File.WriteAllBytes(path, Encode(text, kind, path));

        /// <summary>Writes a file made from <paramref name="like"/> (a converted copy) the way that file is written.</summary>
        public static void WriteLike(string path, string text, string like) => Write(path, text, KindOf(like));
    }

    /// <summary>A change would add characters that a file which is not UTF-8 cannot hold; the file is left as it is.</summary>
    public sealed class NotUtf8Exception : IOException
    {
        public NotUtf8Exception(string path)
            : base($"{path} is not UTF-8 (a code page such as Windows-1251 or Shift-JIS), and the change would add characters that it cannot hold, so Urpify left the file as it is.") { }
    }
}
