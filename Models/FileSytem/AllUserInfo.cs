using Newtonsoft.Json;

namespace P2PShare.Libs.Models.FileSytem
{
    public class AllUserInfo
    {
        public required User User { get; init; }
        public required Dir MyDir { get; init; }
        public required User[] Users { get; init; }

        public Dir[]? SharedDirs { get; init; }
        public Fil[]? SharedFils { get; init; }
        public Group[]? UserGroups { get; init; }

        public string ToJSON() => JsonConvert.SerializeObject(this, SerializationSettings.Settings);

        public static AllUserInfo Deserialize(string json) => JsonConvert.DeserializeObject<AllUserInfo>(json, SerializationSettings.Settings) ?? throw new FormatException("Couldn't deserialize dir json.");
    }
}
