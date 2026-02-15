namespace P2PShare.Libs.Models.FileSytem
{
    public class Group
    {
        public required string Name { get; set; }
        public required string Admin { get; set; }
        public required string[] Users { get; set; }
    }
}
