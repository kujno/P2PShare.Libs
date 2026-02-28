using Newtonsoft.Json;
using P2PShare.Libs.Models.FileSytem;

namespace P2PShare.Libs.Models.Requests
{
    public class Request
    {
        public required Tag Tag { get; init; }
        public int? ID { get; init; }
        public string? Name { get; init; }
        public string? Surename { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public string? FileName { get; set; }
        public string? NewFileName { get; init; }
        public long FileSize { get; init; }
        public Unit Unit { get; init; }
        public bool My { get; set; }
        public bool Encrypted { get; init; }
        public Group? Group { get; init; }
        public Share[]? Shares { get; init; }

        public static Request Create(string requestJSON) => JsonConvert.DeserializeObject<Request>(requestJSON, SerializationSettings.Settings)!;

        public string ToJSON() => JsonConvert.SerializeObject(this, SerializationSettings.Settings);
    }
}
