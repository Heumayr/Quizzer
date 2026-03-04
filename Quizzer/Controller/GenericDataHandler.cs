//using Newtonsoft.Json;
//using Quizzer.Base;
//using Quizzer.Controller.Helper;
//using Quizzer.DataModels;
//using Quizzer.DataModels.Models;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace Quizzer.Controller
//{
//    public class GenericDataHandler
//    {
//        private const string FileEnding = ".json";

//        // One lock per file path to prevent concurrent writes from multiple windows/VMs
//        private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new();

//        public static JsonSerializerSettings JsonSettings { get; } = new JsonSerializerSettings
//        {
//            Formatting = Formatting.Indented,
//            NullValueHandling = NullValueHandling.Ignore,
//            TypeNameHandling = TypeNameHandling.Auto,
//            SerializationBinder = new QuizzerTypeBinder()
//        };

//        private static SemaphoreSlim GetLock(string filePath) =>
//            FileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1));

//        // Sharing/lock violations on Windows (file currently used)
//        private static bool IsSharingOrLockViolation(IOException ex)
//        {
//            int hr = ex.HResult;
//            return hr == unchecked((int)0x80070020) // ERROR_SHARING_VIOLATION
//                || hr == unchecked((int)0x80070021); // ERROR_LOCK_VIOLATION
//        }

//        private static string GetFilePath<T>() where T : ModelBase =>
//            Path.Combine(Settings.FilePathQuizzer, $"{typeof(T).Name}{FileEnding}");

//        private static void EnsureDirectory(string filePath)
//        {
//            var dir = Path.GetDirectoryName(filePath);
//            if (!string.IsNullOrWhiteSpace(dir))
//                Directory.CreateDirectory(dir);
//        }

//        public async Task SaveToFileAsync<T>(IEnumerable<T> data, CancellationToken cancellationToken = default)
//            where T : ModelBase
//        {
//            if (data == null) throw new ArgumentNullException(nameof(data));

//            var filePath = GetFilePath<T>();
//            EnsureDirectory(filePath);

//            var fileLock = GetLock(filePath);
//            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);

//            // Serialize outside lock? If 'data' can be modified concurrently, serialize inside the lock.
//            // Here we take a snapshot list first to avoid "collection modified" issues.
//            var sourceSnapshot = data.ToList();

//            try
//            {
//                var datatosave = new List<T>(sourceSnapshot.Count);

//                foreach (var item in sourceSnapshot)
//                {
//                    cancellationToken.ThrowIfCancellationRequested();

//                    if (item.Id == Guid.Empty)
//                        item.Id = Guid.NewGuid();

//                    var clone = item.CloneAndClearOnSave(JsonSettings);
//                    if (clone != null)
//                        datatosave.Add(clone);
//                }

//                var json = JsonConvert.SerializeObject(datatosave, JsonSettings);

//                // Atomic write: write to temp and then replace target
//                var tmpPath = filePath + ".tmp";

//                // Small retry loop for transient sharing violations (e.g., AV scanner, parallel close)
//                const int maxAttempts = 6;
//                for (int attempt = 1; ; attempt++)
//                {
//                    cancellationToken.ThrowIfCancellationRequested();

//                    try
//                    {
//                        await File.WriteAllTextAsync(tmpPath, json, cancellationToken).ConfigureAwait(false);

//#if NET6_0_OR_GREATER
//                        File.Move(tmpPath, filePath, overwrite: true);
//#else
//                        if (File.Exists(filePath))
//                            File.Delete(filePath);
//                        File.Move(tmpPath, filePath);
//#endif
//                        break;
//                    }
//                    catch (IOException ex) when (IsSharingOrLockViolation(ex) && attempt < maxAttempts)
//                    {
//                        // backoff: 80, 160, 240, ...
//                        await Task.Delay(80 * attempt, cancellationToken).ConfigureAwait(false);
//                    }
//                    finally
//                    {
//                        // If move failed, try to clean up tmp on next run (best effort)
//                        // Don't throw if cleanup fails.
//                        try
//                        {
//                            if (File.Exists(tmpPath) && attempt == maxAttempts)
//                                File.Delete(tmpPath);
//                        }
//                        catch { }
//                    }
//                }
//            }
//            catch (OperationCanceledException)
//            {
//                throw;
//            }
//            catch (Exception ex)
//            {
//                throw new IOException($"Fehler beim Speichern der Datei '{filePath}'.", ex);
//            }
//            finally
//            {
//                fileLock.Release();
//            }
//        }

//        public async Task<IEnumerable<T>> LoadFromFileAsync<T>(CancellationToken cancellationToken = default)
//            where T : ModelBase
//        {
//            var filePath = GetFilePath<T>();

//            if (!File.Exists(filePath))
//                return Enumerable.Empty<T>();

//            var fileLock = GetLock(filePath);
//            await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);

//            try
//            {
//                // If a previous crash left a temp file, we ignore it.
//                var json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);

//                if (string.IsNullOrWhiteSpace(json))
//                    return Enumerable.Empty<T>();

//                var result = JsonConvert.DeserializeObject<List<T>>(json, JsonSettings);
//                return result ?? Enumerable.Empty<T>();
//            }
//            catch (OperationCanceledException)
//            {
//                throw;
//            }
//            catch (Exception ex)
//            {
//                throw new IOException($"Fehler beim Laden der Datei '{filePath}'.", ex);
//            }
//            finally
//            {
//                fileLock.Release();
//            }
//        }
//    }
//}