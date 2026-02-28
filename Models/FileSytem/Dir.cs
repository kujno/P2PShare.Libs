using Newtonsoft.Json;

namespace P2PShare.Libs.Models.FileSytem
{
    public class Dir
    {
        public string Name { get; set; }
        public string Owner { get; }
        public List<Fil>? Fils { get; }
        public List<Dir>? Dirs { get; }
        public bool CanDelete { get; set; }
        public bool CanRename { get; set; }
        public bool CanAdd { get; set; }
        public Share[]? Shares { get; set; }

        public Dir(string path, string owner, bool canDelete, bool canRename, bool canAdd)
        {
            DirectoryInfo dirInfo = new(path);
            FileInfo[] files;
            DirectoryInfo[] directories;

            Name = dirInfo.Name;
            Owner = owner;
            CanDelete = canDelete;
            CanRename = canRename;
            CanAdd = canAdd;

            if ((files = dirInfo.GetFiles()).Length > 0)
            {
                Fils = [];

                Array.ForEach(files, x => Fils.Add(new()
                {
                    Name = x.Name,
                    Size = x.Length,
                    Owner = owner,
                    CanDelete = CanDelete,
                    CanRename = CanRename,
                    Shares = Shares
                }));
            }

            if ((directories = dirInfo.GetDirectories()).Length > 0)
            {
                Dirs = [];

                Array.ForEach(directories, x => Dirs.Add(new(x.FullName, Owner, CanDelete, CanRename, CanAdd)));
            }
        }

        [JsonConstructor]
        public Dir(string name, string owner, bool canDelete, bool canRename, bool canAdd, Fil[]? fils = null, Dir[]? dirs = null, Share[]? shares = null)
        {
            Name = name;
            Owner = owner;
            Fils = fils?.ToList();
            Dirs = dirs?.ToList();
            CanDelete = canDelete;
            CanRename = canRename;
            CanAdd = canAdd;
            Shares = shares;
        }
    }
}
