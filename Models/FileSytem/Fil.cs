namespace P2PShare.Libs.Models.FileSytem
{
    public class Fil
    {
        public required string Name { get; init; }
        public required long Size { get; init; }
        public bool CanDelete { get; init; }
        public bool CanRename { get; init; }
    }
}
