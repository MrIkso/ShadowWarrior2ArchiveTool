using ShadowWarrior2ArchiveTool;

class Program
{

    static void Main(string[] args)
    {
        try
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: ShadowWarrior2ArchiveTool <pack|unpack> [options]");
                return;
            }
            string mode = args[0].ToLower();
            string[] modeArgs = args[1..];
            switch (mode)
            {
                case "pack":
                    PackMode(modeArgs);
                    break;
                case "unpack":
                    UnpackMode(modeArgs);
                    break;
                default:
                    Console.WriteLine("Unknown mode. Use 'pack' or 'unpack'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }


    private static void PackMode(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: ShadowWarrior2ArchiveTool <input_dir> <output.hog>");
            return;
        }
        string inputDir = args[0];
        string outputFile = args[1];
        try
        {
            Packer.PackArchive(inputDir, outputFile);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void UnpackMode(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: ShadowWarrior2ArchiveTool <input.hog> <output_dir>");
            return;
        }
        string inputFile = args[0];
        string outputDir = args[1];
        try
        {
            Unpacker.ExtractArchive(inputFile, outputDir);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}