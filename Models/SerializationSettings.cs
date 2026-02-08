using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace P2PShare.Libs.Models
{
    public static class SerializationSettings
    {
        private static JsonSerializerSettings? _settings = null;

        public static JsonSerializerSettings Settings
        {
            get
            {
                if (_settings is null)
                {
                    _settings = new JsonSerializerSettings()
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                    };
                }

                return _settings;
            }
        }
    }
}
