using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Urpify.Packaging
{
    /// <summary>One asset of a .unitypackage: its GUID folder holds "pathname", "asset", "asset.meta" and maybe "preview.png".</summary>
    public sealed class UnityPackageEntry
    {
        public string Guid = "";
        /// <summary>Project path the asset is imported to ("Assets/Shaders/Toon.shader").</summary>
        public string Path = "";
        /// <summary>File contents; null for a folder.</summary>
        public byte[] Asset;
        public byte[] Meta;
        public byte[] Preview;

        public bool IsFolder => Asset == null;
        public string Text => Asset == null ? null : Encoding.UTF8.GetString(Asset);
    }

    /// <summary>
    /// Reads and writes .unitypackage files: a gzip-compressed tar with one folder per asset GUID. Written without Unity's
    /// AssetDatabase.ExportPackage, so the result keeps the original GUIDs and paths even for assets that are not in the
    /// open project. Reads ustar, GNU long names and pax path records.
    /// </summary>
    public static class UnityPackageArchive
    {
        public static List<UnityPackageEntry> Read(string file)
        {
            var byGuid = new Dictionary<string, UnityPackageEntry>(StringComparer.OrdinalIgnoreCase);
            using (var fs = File.OpenRead(file))
            using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            {
                foreach (var (name, data) in ReadTar(gz))
                {
                    var n = name.Replace('\\', '/').TrimStart('.', '/');
                    var slash = n.IndexOf('/');
                    if (slash <= 0) continue;
                    var guid = n.Substring(0, slash);
                    var part = n.Substring(slash + 1);
                    if (!byGuid.TryGetValue(guid, out var e)) byGuid[guid] = e = new UnityPackageEntry { Guid = guid };
                    switch (part)
                    {
                        case "pathname":
                            // First line only: old exporters add a second line with the GUID of a folder.
                            e.Path = Encoding.UTF8.GetString(data).Split('\n')[0].Trim('\r', ' ', '\0').Replace('\\', '/');
                            break;
                        case "asset": e.Asset = data; break;
                        case "asset.meta": e.Meta = data; break;
                        case "preview.png": e.Preview = data; break;
                    }
                }
            }
            return byGuid.Values.Where(e => e.Path.Length > 0).OrderBy(e => e.Path, StringComparer.Ordinal).ToList();
        }

        public static void Write(string file, IEnumerable<UnityPackageEntry> entries)
        {
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = file + ".tmp";
            using (var fs = File.Create(tmp))
            using (var gz = new GZipStream(fs, CompressionLevel.Optimal))
            {
                foreach (var e in entries.OrderBy(x => x.Path, StringComparer.Ordinal))
                {
                    WriteEntry(gz, e.Guid + "/", null);
                    if (e.Asset != null) WriteEntry(gz, e.Guid + "/asset", e.Asset);
                    if (e.Meta != null) WriteEntry(gz, e.Guid + "/asset.meta", e.Meta);
                    WriteEntry(gz, e.Guid + "/pathname", Encoding.UTF8.GetBytes(e.Path));
                    if (e.Preview != null) WriteEntry(gz, e.Guid + "/preview.png", e.Preview);
                }
                gz.Write(new byte[1024], 0, 1024); // two zero blocks end the archive
            }
            if (File.Exists(file)) File.Delete(file);
            File.Move(tmp, file);
        }

        // ------------------------------------------------------------------------------------------ tar

        static IEnumerable<(string name, byte[] data)> ReadTar(Stream s)
        {
            var header = new byte[512];
            string longName = null;
            while (true)
            {
                if (!ReadFull(s, header, 512)) yield break;
                if (header.All(b => b == 0)) yield break;
                var name = Str(header, 0, 100);
                var prefix = Str(header, 345, 155);
                if (prefix.Length > 0 && Str(header, 257, 5) == "ustar") name = prefix + "/" + name;
                long size = Octal(header, 124, 12);
                char type = (char)header[156];
                var data = new byte[size];
                if (!ReadFull(s, data, (int)size)) yield break;
                var pad = (512 - size % 512) % 512;
                if (pad > 0 && !ReadFull(s, new byte[pad], (int)pad)) yield break;
                if (type == 'L') { longName = Encoding.UTF8.GetString(data).TrimEnd('\0'); continue; }
                if (type == 'x' || type == 'g')
                {
                    foreach (var line in Encoding.UTF8.GetString(data).Split('\n'))
                    {
                        var sp = line.IndexOf(' ');
                        var kv = sp < 0 ? line : line.Substring(sp + 1);
                        if (kv.StartsWith("path=", StringComparison.Ordinal) && type == 'x') longName = kv.Substring(5);
                    }
                    continue;
                }
                if (longName != null) { name = longName; longName = null; }
                if (type == '5') continue; // directory
                yield return (name, data);
            }
        }

        static void WriteEntry(Stream s, string name, byte[] data)
        {
            var nameBytes = Encoding.UTF8.GetBytes(name);
            if (nameBytes.Length > 99)
            {
                // GNU long name record first.
                var ln = new byte[nameBytes.Length + 1];
                Array.Copy(nameBytes, ln, nameBytes.Length);
                WriteHeaderAndData(s, "././@LongLink", ln, 'L');
                nameBytes = nameBytes.Take(99).ToArray();
            }
            WriteHeaderAndData(s, Encoding.UTF8.GetString(nameBytes), data ?? new byte[0], data == null ? '5' : '0');
        }

        static void WriteHeaderAndData(Stream s, string name, byte[] data, char type)
        {
            var h = new byte[512];
            Put(h, 0, name, 100);
            Put(h, 100, "0000644", 8);
            Put(h, 108, "0000000", 8);
            Put(h, 116, "0000000", 8);
            Put(h, 124, Convert.ToString(data.Length, 8).PadLeft(11, '0'), 12);
            Put(h, 136, Convert.ToString(1600000000L, 8).PadLeft(11, '0'), 12);
            for (int i = 148; i < 156; i++) h[i] = (byte)' ';
            h[156] = (byte)type;
            Put(h, 257, "ustar", 6);
            h[263] = (byte)'0'; h[264] = (byte)'0';
            long sum = 0;
            foreach (var b in h) sum += b;
            Put(h, 148, Convert.ToString(sum, 8).PadLeft(6, '0'), 7);
            h[155] = (byte)' ';
            s.Write(h, 0, 512);
            if (data.Length == 0) return;
            s.Write(data, 0, data.Length);
            var pad = (512 - data.Length % 512) % 512;
            if (pad > 0) s.Write(new byte[pad], 0, pad);
        }

        static void Put(byte[] h, int at, string value, int max)
        {
            var b = Encoding.ASCII.GetBytes(value);
            Array.Copy(b, 0, h, at, Math.Min(b.Length, max));
        }

        static string Str(byte[] h, int at, int len)
        {
            int end = at;
            while (end < at + len && h[end] != 0) end++;
            return Encoding.UTF8.GetString(h, at, end - at);
        }

        static long Octal(byte[] h, int at, int len)
        {
            if ((h[at] & 0x80) != 0)
            {
                // GNU base-256 for large files.
                long v = 0;
                for (int i = at + 1; i < at + len; i++) v = (v << 8) | h[i];
                return v;
            }
            var s = Str(h, at, len).Trim(' ', '\0');
            return s.Length == 0 ? 0 : Convert.ToInt64(s, 8);
        }

        static bool ReadFull(Stream s, byte[] buf, int count)
        {
            int read = 0;
            while (read < count)
            {
                int n = s.Read(buf, read, count - read);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }
    }
}
