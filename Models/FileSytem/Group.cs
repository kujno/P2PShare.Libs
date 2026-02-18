namespace P2PShare.Libs.Models.FileSytem
{
    public class Group
    {
        public required string Name { get; set; }
        public required User Admin { get; set; }
        public required User[] Users { get; set; }

        public string[] GetUsersUsernames() => Users.Select(x => x.Username).ToArray();
    }
}
