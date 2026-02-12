using Newtonsoft.Json;

namespace P2PShare.Libs.Models.FileSytem
{
    public class Dir
    {
        public string Name { get; }
        public string Owner { get; }
        public List<Fil>? Fils { get; }
        public List<Dir>? Dirs { get; }
        public bool CanDelete { get; init; } = true;
        public bool CanRename { get; init; } = true;
        public bool CanAdd { get; init; } = true;

        public Dir(string path, string owner)
        {
            DirectoryInfo dirInfo = new(path);
            FileInfo[] files;
            DirectoryInfo[] directories;

            Name = dirInfo.Name;
            Owner = owner;

            if ((files = dirInfo.GetFiles()).Length > 0)
            {
                Fils = [];

                Array.ForEach(files, x => Fils.Add(new()
                {
                    Name = x.Name,
                    Size = x.Length,
                    Owner = owner,
                }));
            }

            if ((directories = dirInfo.GetDirectories()).Length > 0)
            {
                Dirs = [];

                Array.ForEach(directories, x => Dirs.Add(new(x.FullName, Owner)));
            }
        }

        [JsonConstructor]
        public Dir(string name, string owner, Fil[]? fils = null, Dir[]? dirs = null)
        {
            Name = name;
            Owner = owner;
            Fils = fils?.ToList();
            Dirs = dirs?.ToList();
        }
    }
}
