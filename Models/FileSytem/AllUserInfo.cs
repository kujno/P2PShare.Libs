using Newtonsoft.Json;

namespace P2PShare.Libs.Models.FileSytem
{
    public class AllUserInfo
    {
        public required Dir MyDir { get; init; }
        public required string[] Users { get; init; }
        
        public Dir[]? SharedDirs { get; init; }
        public Fil[]? SharedFils { get; init; }
        public string[]? UserGroups { get; init; }

        public string ToJSON() => JsonConvert.SerializeObject(this, SerializationSettings.Settings);

        public static Dir Deserialize(string json) => JsonConvert.DeserializeObject<Dir>(json, SerializationSettings.Settings) ?? throw new FormatException("Couldn't deserialize dir json.");
    }
}
