using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ShadowWarrior2ArchiveTool
{
    internal class Packer
    {
        const uint CHUNK_SIZE = 0x10000; // 64KB

        struct FileEntry
        {
            public string Name;
            public uint Size;
            public uint CompressedSize;
            public int ChunkIdx;
            public long Offset;
        }

        public static void PackArchive(string inputDir, string outArchivePath)
        {
            if (!Directory.Exists(inputDir))
            {
                throw new DirectoryNotFoundException(inputDir);
            }

            string[] allFiles = Directory.GetFiles(inputDir, "*", SearchOption.AllDirectories);

            // read pack config file if exists
            string configFileName = "unpacked_config.json";
            string configPath = Path.Combine(inputDir, configFileName);
            Dictionary<string, bool> processedFiles = new Dictionary<string, bool>();

            if (File.Exists(configPath))
            {
                allFiles = allFiles.Where(p => !Path.GetFullPath(p).Equals(Path.GetFullPath(configPath), 
                    StringComparison.OrdinalIgnoreCase)).ToArray();
                try
                {
                    string json = File.ReadAllText(configPath, Encoding.UTF8);
                    ConfigModel? cfg = JsonSerializer.Deserialize<ConfigModel>(json);
                    if (cfg != null && cfg.files != null)
                    {
                        foreach (var fileModel in cfg.files)
                        {
                            string fileName = fileModel.path.Replace('\\', '/');
                            processedFiles[fileName] = fileModel.isCompressed;
                        }
                    }
                }
                catch
                {
                   
                }
            }

            List<FileEntry> entries = new List<FileEntry>();
            List<ushort> globalChunkSizes = new List<ushort>();

            using (FileStream fs = new FileStream(outArchivePath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // Header placeholder
                bw.Write(Encoding.ASCII.GetBytes("HOGP"));
                bw.Write(3); // Version
                bw.Write(0L); // InfoOffset placeholder

                // Data Block
                foreach (string filePath in allFiles)
                {
                    string relativeName = Path.GetRelativePath(inputDir, filePath).Replace('\\', '/');
                    
                    processedFiles.TryGetValue(relativeName, out bool isCompressed);
                    byte[] fileData = File.ReadAllBytes(filePath);

                    FileEntry entry = new FileEntry
                    {
                        Name = relativeName,
                        Size = (uint)fileData.Length,
                        Offset = bw.BaseStream.Position,
                        ChunkIdx = 0 // by default
                    };

                    if (entry.Size == 0)
                    {
                        entry.CompressedSize = 0;
                        entry.ChunkIdx = -1; // 
                        entries.Add(entry);
                        continue;
                    }

                    // check if compression is beneficial
                    if (isCompressed)
                    {
                        entry.ChunkIdx = globalChunkSizes.Count;
                        uint totalCompressedSize = 0;
                        int chunksCount = (int)((entry.Size + CHUNK_SIZE - 1) / CHUNK_SIZE);

                        for (int c = 0; c < chunksCount; c++)
                        {
                            int offset = (int)(c * CHUNK_SIZE);
                            int size = (int)Math.Min(CHUNK_SIZE, entry.Size - offset);
                            byte[] chunkData = new byte[size];
                            Array.Copy(fileData, offset, chunkData, 0, size);

                            byte[] compressed = CompressData(chunkData);

                            if (size == CHUNK_SIZE && compressed.Length >= size)
                            {
                                bw.Write(chunkData);
                                globalChunkSizes.Add(0);
                                totalCompressedSize += (uint)size;
                            }
                            else
                            {
                                bw.Write(compressed);
                                globalChunkSizes.Add((ushort)compressed.Length);
                                totalCompressedSize += (uint)compressed.Length;
                            }
                        }
                        entry.CompressedSize = totalCompressedSize;
                    }
                    else
                    {
                        entry.CompressedSize = entry.Size;
                        entry.ChunkIdx = 0;
                        bw.Write(fileData);
                    }

                    Console.WriteLine($"Packed: {entry.Name} ({(isCompressed ? "Compressed" : "Flat")})");
                    entries.Add(entry);
                }

                // write info table
                long infoTableStart = bw.BaseStream.Position;
                bw.Write(0u); // Placeholder for InfoSize

                bw.Write(entries.Count);
                foreach (var entry in entries)
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Name);
                    bw.Write((short)nameBytes.Length);
                    bw.Write(nameBytes);
                    bw.Write(entry.Size);
                    bw.Write(entry.CompressedSize);
                    bw.Write(entry.ChunkIdx);
                    bw.Write(entry.Offset);
                }

                // write chusnk table
                bw.Write(globalChunkSizes.Count);
                foreach (ushort cz in globalChunkSizes)
                {
                    bw.Write(cz);
                }

                long infoTableEnd = bw.BaseStream.Position;
                uint infoTableSize = (uint)(infoTableEnd - infoTableStart);

                bw.BaseStream.Seek(infoTableStart, SeekOrigin.Begin);
                bw.Write(infoTableSize - 4);

                bw.BaseStream.Seek(8, SeekOrigin.Begin);
                bw.Write(infoTableStart - 16);
            }
            Console.WriteLine("\nSuccess!");
        }

        static byte[] CompressData(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (DeflateStream ds = new DeflateStream(ms, CompressionLevel.Optimal, true))
                {
                    ds.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }
    }
}