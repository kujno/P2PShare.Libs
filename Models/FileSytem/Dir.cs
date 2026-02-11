using Newtonsoft.Json;

namespace P2PShare.Libs.Models.FileSytem
{
    public class Dir
    {
        public string Name { get; }
        public List<Fil>? Fils { get; }
        public List<Dir>? Dirs { get; }
        public bool CanDelete { get; init; }
        public bool CanRename { get; init; }

        public Dir(string path)
        {
            DirectoryInfo dirInfo = new(path);
            FileInfo[] files;
            DirectoryInfo[] directories;

            Name = dirInfo.Name;

            if ((files = dirInfo.GetFiles()).Length > 0)
            {
                Fils = [];

                Array.ForEach(files, x => Fils.Add(new()
                {
                    Name = x.Name,
                    Size = x.Length
                }));
            }

            if ((directories = dirInfo.GetDirectories()).Length > 0)
            {
                Dirs = [];

                Array.ForEach(directories, x => Dirs.Add(new(x.FullName)));
            }
        }

        [JsonConstructor]
        public Dir(string name, Fil[]? fils = null, Dir[]? dirs = null)
        {
            Name = name;
            Fils = fils?.ToList();
            Dirs = dirs?.ToList();
        }
    }
}
