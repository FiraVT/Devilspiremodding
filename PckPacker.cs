using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace DevilSpireLauncher
{
    public class PckPacker
    {
        private class FileEntry
        {
            public string Path;
            public string SourcePath;
            public long Offset;
            public long Size;
            public byte[] MD5;
        }

        public static void CreatePck(string pckPath, Dictionary<string, string> files)
        {
            List<FileEntry> entries = new List<FileEntry>();
            foreach (var kvp in files)
            {
                if (!File.Exists(kvp.Value)) continue;

                var entry = new FileEntry
                {
                    Path = kvp.Key,
                    SourcePath = kvp.Value,
                    Size = new FileInfo(kvp.Value).Length
                };

                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(kvp.Value))
                    {
                        entry.MD5 = md5.ComputeHash(stream);
                    }
                }
                entries.Add(entry);
            }

            using (var fs = File.Create(pckPath))
            using (var writer = new BinaryWriter(fs))
            {
                // Header
                writer.Write(0x43504447); // 'GDPC'
                writer.Write(2); // PCK Version 2
                writer.Write(4); // Engine Major 4
                writer.Write(0); // Engine Minor 0
                writer.Write(0); // Engine Patch 0
                writer.Write(0); // Reserved
                writer.Write(entries.Count);

                // Index (Calculate offsets later)
                long indexStart = fs.Position;
                foreach (var entry in entries)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                    writer.Write(pathBytes.Length);
                    writer.Write(pathBytes);
                    writer.Write((long)0); // Offset placeholder
                    writer.Write(entry.Size);
                    writer.Write(entry.MD5);
                }

                // Data
                long currentOffset = fs.Position;
                for (int i = 0; i < entries.Count; i++)
                {
                    // Align to 8 bytes
                    long padding = (8 - (fs.Position % 8)) % 8;
                    for (int p = 0; p < padding; p++) writer.Write((byte)0);

                    entries[i].Offset = fs.Position;
                    using (var source = File.OpenRead(entries[i].SourcePath))
                    {
                        source.CopyTo(fs);
                    }
                }

                // Back-patch index with correct offsets
                fs.Position = indexStart;
                foreach (var entry in entries)
                {
                    byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
                    fs.Position += 4 + pathBytes.Length; // Skip path
                    writer.Write(entry.Offset);
                    fs.Position += 8 + 16; // Skip size and MD5
                }
            }
        }
    }
}
