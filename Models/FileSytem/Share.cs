namespace P2PShare.Libs.Models.FileSytem
{
    public class Share
    {
        public User? User { get; init; }
        public Group? Group { get; init; }
        public required Unit Type { get; init; }
        public required bool CanDelete { get; init; }
        public required bool CanRename { get; init; }
        public bool CanAdd { get; init; }
    }
}
