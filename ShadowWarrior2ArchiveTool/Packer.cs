using System.IO.Compression;
using System.Text;

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

                    List<byte[]> compressedChunks = new List<byte[]>();
                    uint totalZSize = 0;
                    bool shouldCompress = false;

                    int chunksCount = (int)((entry.Size + CHUNK_SIZE - 1) / CHUNK_SIZE);

                    for (int c = 0; c < chunksCount; c++)
                    {
                        int offset = (int)(c * CHUNK_SIZE);
                        int size = (int)Math.Min(CHUNK_SIZE, entry.Size - offset);
                        byte[] chunk = new byte[size];
                        Array.Copy(fileData, offset, chunk, 0, size);

                        byte[] compressed = CompressData(chunk);

                        // if compression not effective, store uncompressed
                        if (compressed.Length >= size)
                        {
                            compressedChunks.Add(chunk);
                            totalZSize += (uint)size;
                        }
                        else
                        {
                            compressedChunks.Add(compressed);
                            totalZSize += (uint)compressed.Length;
                            shouldCompress = true;
                        }
                    }

                    // check if compression is beneficial
                    if (shouldCompress)
                    {
                        entry.ChunkIdx = globalChunkSizes.Count;
                        entry.CompressedSize = totalZSize;

                        for (int i = 0; i < compressedChunks.Count; i++)
                        {
                            byte[] dataToWrite = compressedChunks[i];
                            int originalChunkSize = (int)Math.Min(CHUNK_SIZE, entry.Size - (i * CHUNK_SIZE));

                            bw.Write(dataToWrite);

                            // if chunk is uncompressed, write 0
                            if (dataToWrite.Length == originalChunkSize)
                            {
                                globalChunkSizes.Add(0);
                            }
                            else
                            {
                                globalChunkSizes.Add((ushort)dataToWrite.Length);
                            }
                        }
                    }
                    else
                    {
                        // write uncompressed
                        entry.CompressedSize = entry.Size;
                        entry.ChunkIdx = 0; // 
                        bw.Write(fileData);
                    }

                    Console.WriteLine($"Packed: {entry.Name} ({(shouldCompress ? "Compressed" : "Flat")})");
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