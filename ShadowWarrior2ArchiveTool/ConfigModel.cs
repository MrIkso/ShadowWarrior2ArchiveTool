namespace ShadowWarrior2ArchiveTool
{
    public class FileModel
    {
        public string path { get; set; }
        public bool isCompressed { get; set; }
    }

    public class ConfigModel
    {
        public int fileCount { get; set; }
        public List<FileModel> files { get; set; }
    }
}
