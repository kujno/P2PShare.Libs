using Newtonsoft.Json;

namespace P2PShare.Libs.Models.FileSytem
{
    public class Dir
    {
        public string Name { get; }
        public List<Fil>? Fils { get; }
        public List<Dir>? Dirs { get; }

        public Dir(string name)
        {
            DirectoryInfo dirInfo = new(name);
            FileInfo[] files;
            DirectoryInfo[] directories;

            Name = name;

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

                Array.ForEach(directories, x => Dirs.Add(new($"{Name}\\{x.Name}")));
            }
        }

        [JsonConstructor]
        public Dir(string name, Fil[]? files, Dir[]? dirs)
        {
            Name = name;
            Fils = files?.ToList();
            Dirs = dirs?.ToList();
        }

        public string ToJSON() => JsonConvert.SerializeObject(this, SerializationSettings.Settings);

        public static Dir Deserialize(string json) => JsonConvert.DeserializeObject<Dir>(json, SerializationSettings.Settings) ?? throw new FormatException("Couldn't deserialize dir json.");
    }
}
