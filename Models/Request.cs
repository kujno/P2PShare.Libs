using Newtonsoft.Json;

namespace P2PShare.Libs.Models
{
    public class Request
    {
        public required Tag Tag { get; init; }
        public string? Name { get; init; }
        public string? Surename { get; init; }
        public string? Username { get; init; }
        public string? Password { get; init; }
        public string? FileName { get; init; }
        public long FileSize { get; init; }
        public bool Encrypted { get; init; }

        public static Request Create(string requestJSON) => JsonConvert.DeserializeObject<Request>(requestJSON, SerializationSettings.Settings)!;

        public string ToJSON() => JsonConvert.SerializeObject(this, SerializationSettings.Settings);
    }
}
