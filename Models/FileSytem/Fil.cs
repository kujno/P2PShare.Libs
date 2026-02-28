namespace P2PShare.Libs.Models.FileSytem
{
    public class Fil
    {
        public required string Name { get; init; }
        public required string Owner { get; init; }
        public required long Size { get; init; }
        public bool CanDelete { get; set; }
        public bool CanRename { get; set; }
        public Share[]? Shares { get; set; }
    }
}
