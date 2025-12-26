using System.IO.Compression;

namespace ShadowWarrior2ArchiveTool
{
    internal class Unpacker
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

        struct ChunkEntry
        {
            public ushort ZSize;
        }

        struct Header
        {
            public char[] Magic; // "HOGP"
            public int Version;  // 3
            public long InfoOffset;
        }


        public static void ExtractArchive(string archivePath, string outDir)
        {
            using (FileStream fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader br = new BinaryReader(fs))
            {
                // read header
                char[] magic = br.ReadChars(4);
                if (new string(magic) != "HOGP")
                {
                    throw new Exception("Not a HOGP file!");
                }

                Header header = new Header
                {
                    Magic = magic,
                    Version = br.ReadInt32(),
                    InfoOffset = br.ReadInt64()
                };

                // goto info table section
                long absoluteInfoOff = br.BaseStream.Position + header.InfoOffset;
                br.BaseStream.Seek(absoluteInfoOff, SeekOrigin.Begin);

                uint infoSize = br.ReadUInt32();
                int filesCount = br.ReadInt32();

                Console.WriteLine($"Files found: {filesCount}");

                List<FileEntry> entries = new List<FileEntry>();

                // read file entries
                for (int i = 0; i < filesCount; i++)
                {
                    short nameLen = br.ReadInt16();
                    string name = new string(br.ReadChars(nameLen)).Replace('/', '\\');
                    uint size = br.ReadUInt32();
                    uint compressedSize = br.ReadUInt32();
                    int chunkIdx = br.ReadInt32();
                    long offset = br.ReadInt64();

                    entries.Add(new FileEntry
                    {
                        Name = name,
                        Size = size,
                        CompressedSize = compressedSize,
                        ChunkIdx = chunkIdx,
                        Offset = offset
                    });
                }

                // read chunk sizes
                int totalChunks = br.ReadInt32();
                ushort[] chunkSizes = new ushort[totalChunks];
                for (int i = 0; i < totalChunks; i++)
                {
                    chunkSizes[i] = br.ReadUInt16();
                }

                // 4. Розпакування
                foreach (var entry in entries)
                {
                    if (entry.Offset == -1)
                    {
                        continue; // skip empty files
                    }

                    string fullPath = Path.Combine(outDir, entry.Name);
                    string dir = Path.GetDirectoryName(fullPath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    Console.WriteLine($"Extracting: {entry.Name}");

                    using (FileStream outFs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
                    {
                        br.BaseStream.Seek(entry.Offset, SeekOrigin.Begin);

                        if (entry.Size == entry.CompressedSize)
                        {
                            // not compressed
                            CopyData(br.BaseStream, outFs, (int)entry.Size);
                        }
                        else
                        {
                            // comressed file in chunks
                            int chunksCount = (int)((entry.Size + CHUNK_SIZE - 1) / CHUNK_SIZE);
                            for (int c = 0; c < chunksCount; c++)
                            {
                                ushort chunkCompressedSize = chunkSizes[entry.ChunkIdx + c];
                                if (chunkCompressedSize == 0)
                                {
                                    // chunk not compressed
                                    CopyData(br.BaseStream, outFs, (int)CHUNK_SIZE);
                                }
                                else
                                {
                                    // chunk compressed (Deflate/Zlib)
                                    byte[] compressedBuffer = br.ReadBytes(chunkCompressedSize);
                                    DecompressChunk(compressedBuffer, outFs);
                                }
                            }
                        }
                    }
                }
            }
            Console.WriteLine("Done!");
        }

        static void DecompressChunk(byte[] compressedData, Stream outStream)
        {
            int headerSkip = 0;
            if (compressedData.Length > 2 && compressedData[0] == 0x78)
            {
                headerSkip = 2; // skip zlib header
            }

            using (MemoryStream ms = new MemoryStream(compressedData, headerSkip, compressedData.Length - headerSkip))
            using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                ds.CopyTo(outStream);
            }
        }

        static void CopyData(Stream input, Stream output, int size)
        {
            byte[] buffer = new byte[Math.Min(size, 81920)];
            int read;
            int remaining = size;
            while (remaining > 0 && (read = input.Read(buffer, 0, Math.Min(buffer.Length, remaining))) > 0)
            {
                output.Write(buffer, 0, read);
                remaining -= read;
            }
        }
    }
}
