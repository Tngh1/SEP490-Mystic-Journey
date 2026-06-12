using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace MysticJourney.API.Utilities
{
    public static class JsonHelper
    {
        private static readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        private static readonly JsonSerializerSettings _prettySettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.Indented
        };

        public static string ToJson<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        public static string ToPrettyJson<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _prettySettings);
        }

        public static T FromJson<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default;

            try
            {
                return JsonConvert.DeserializeObject<T>(json, _settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[JsonHelper] FromJson<{typeof(T).Name}> failed: {ex.Message}\nJSON: {json}");
                return default;
            }
        }

        public static bool TryFromJson<T>(string json, out T result)
        {
            result = default;
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                result = JsonConvert.DeserializeObject<T>(json, _settings);
                return result != null;
            }
            catch
            {
                return false;
            }
        }

        public static void Log<T>(string label, T obj)
        {
            Debug.Log($"[JsonHelper] {label}:\n{ToPrettyJson(obj)}");
        }
    }
}
