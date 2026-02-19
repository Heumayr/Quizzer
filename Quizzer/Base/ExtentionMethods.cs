using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Quizzer.Base
{
    public static class ExtentionMethods
    {
        // Sinnvolle Default-Settings für Newtonsoft
        private static readonly JsonSerializerSettings DefaultSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), // camelCase
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            // Bei Bedarf Referenzzyklen erlauben:
            // ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            // PreserveReferencesHandling = PreserveReferencesHandling.Objects
        };

        /// <summary>Objekt -> JSON-String</summary>
        public static string ToJson(this object? value, JsonSerializerSettings? settings = null)
            => JsonConvert.SerializeObject(value, settings ?? DefaultSettings);

        /// <summary>JSON-String -> T (wirft bei Fehlern)</summary>
        public static T? FromJson<T>(this string json, JsonSerializerSettings? settings = null)
            => JsonConvert.DeserializeObject<T>(json, settings ?? DefaultSettings);

        /// <summary>JSON-String -> Objekt zur Laufzeit (non-generic)</summary>
        public static object? FromJson(this string json, Type type, JsonSerializerSettings? settings = null)
            => JsonConvert.DeserializeObject(json, type, settings ?? DefaultSettings);

        /// <summary>Sichere Variante: false statt Exception</summary>
        public static bool TryFromJson<T>(this string json, out T? result, JsonSerializerSettings? settings = null)
        {
            try
            {
                result = JsonConvert.DeserializeObject<T>(json, settings ?? DefaultSettings);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>Deep Copy via Serialize/Deserialize</summary>
        public static T? DeepClone<T>(this T source, JsonSerializerSettings? settings = null)
        {
            if (source == null) return default!;
            var runtimeType = source.GetType();

            var json = JsonConvert.SerializeObject(source, runtimeType, settings);
            var clone = JsonConvert.DeserializeObject(json, runtimeType, settings);

            return (T)clone!;
        }

        public static T? CloneAndClearOnSave<T>(this T source, JsonSerializerSettings? settings = null)
        {
            if (source is null) return default;
            settings?.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; // Verhindert Probleme bei zyklischen Referenzen
            var clone = source.DeepClone(settings);
            if (clone is null) return default;
            var properties = typeof(T).GetProperties();
            foreach (var prop in properties)
            {
                var attrs = prop.GetCustomAttributes(typeof(Attributes.ClearOnSaveAttribute), true);
                if (attrs.Length > 0)
                {
                    if (prop.PropertyType.IsValueType)
                    {
                        var defaultValue = Activator.CreateInstance(prop.PropertyType);
                        prop.SetValue(clone, defaultValue);
                    }
                    else
                    {
                        prop.SetValue(clone, null);
                    }
                }
            }
            return clone;
        }
    }
}