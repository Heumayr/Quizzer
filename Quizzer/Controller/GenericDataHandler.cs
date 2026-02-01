using Newtonsoft.Json;
using Quizzer.Base;
using Quizzer.Controller.Helper;
using Quizzer.Datamodels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quizzer.Controller
{
    public class GenericDataHandler
    {
        private static readonly string FileEnding = ".json";

        public static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new QuizzerTypeBinder()
        };

        public async Task SaveToFileAsync<T>(
            IEnumerable<T> data,
            CancellationToken cancellationToken = default)
            where T : ModelBase
        {
            try
            {
                var filePath = Path.Combine(Settings.FilePathQuizzer, $"{typeof(T).Name}{FileEnding}");

                var datatosave = new List<T>();

                foreach (var item in data)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (item.Id == Guid.Empty)
                        item.Id = Guid.NewGuid();

                    var clone = item.CloneAndClearOnSave(JsonSettings);
                    if (clone != null)
                        datatosave.Add(clone);
                }

                var json = JsonConvert.SerializeObject(datatosave, JsonSettings);

                // Ensure directory exists (optional but usually helpful)
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(filePath, json, cancellationToken)
                          .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Speichern der Datei", ex);
            }
        }

        public async Task<IEnumerable<T>> LoadFromFileAsync<T>(
            CancellationToken cancellationToken = default)
            where T : ModelBase
        {
            try
            {
                var filePath = Path.Combine(Settings.FilePathQuizzer, $"{typeof(T).Name}{FileEnding}");

                if (!File.Exists(filePath))
                    return Enumerable.Empty<T>();

                var json = await File.ReadAllTextAsync(filePath, cancellationToken)
                                     .ConfigureAwait(false);

                var result = JsonConvert.DeserializeObject<List<T>>(json, JsonSettings);

                return result ?? Enumerable.Empty<T>();
            }
            catch (Exception ex)
            {
                throw new Exception("Fehler beim Laden der Datei", ex);
            }
        }
    }
}