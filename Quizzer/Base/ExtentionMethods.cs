using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;

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
            CheckAndClear(clone);
            return clone;
        }

        private static void CheckAndClear(object? obj, HashSet<object>? visited = null)
        {
            if (obj is null) return;

            visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);

            // Leaf types nicht traversieren
            var type = obj.GetType();
            if (IsLeaf(type)) return;

            // Zyklen verhindern
            if (!visited.Add(obj)) return;

            // Collections traversieren (aber string ist auch IEnumerable -> vorher Leaf)
            if (obj is IEnumerable enumerable && obj is not IDictionary)
            {
                foreach (var item in enumerable)
                    CheckAndClear(item, visited);
                // kein return: auch Properties von Collection-Typen können interessant sein
            }

            // Dictionaries traversieren
            if (obj is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    CheckAndClear(entry.Key, visited);
                    CheckAndClear(entry.Value, visited);
                }
            }

            var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

            foreach (var prop in props)
            {
                // ClearOnSave?
                bool clear = prop.GetCustomAttributes(typeof(Quizzer.DataModels.Attributes.ClearOnSaveAttribute), true).Any();

                object? val = null;
                try { val = prop.GetValue(obj, null); }
                catch { /* ignore */ }

                if (clear && prop.CanWrite)
                {
                    SetDefault(obj, prop);
                    continue; // nicht in den alten Wert reinlaufen
                }

                // rekursiv weiter
                if (val is null) continue;
                if (IsLeaf(val.GetType())) continue;

                CheckAndClear(val, visited);
            }
        }

        private static void SetDefault(object target, PropertyInfo prop)
        {
            var t = prop.PropertyType;

            object? defaultValue;
            if (!t.IsValueType || Nullable.GetUnderlyingType(t) != null)
                defaultValue = null;
            else
                defaultValue = Activator.CreateInstance(t);

            try { prop.SetValue(target, defaultValue); }
            catch { /* ignore */ }
        }

        private static bool IsLeaf(Type t)
        {
            if (t.IsPrimitive || t.IsEnum) return true;

            return t == typeof(string)
                || t == typeof(decimal)
                || t == typeof(DateTime)
                || t == typeof(DateTimeOffset)
                || t == typeof(TimeSpan)
                || t == typeof(Guid);
        }

        // Reference-Equality comparer für visited-set
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}