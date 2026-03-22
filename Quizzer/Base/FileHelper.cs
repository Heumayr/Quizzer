using Quizzer.DataModels.Enumerations;
using SkiaSharp;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace Quizzer.Base
{
    public static class FileHelper
    {
        public static (string Filename, ResourceType Type) HandleSelectedResourceFile(string sourceFilePath, string rootFolder, string uniqueFileName = "")
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Selected file does not exist.", sourceFilePath);

            Directory.CreateDirectory(rootFolder);

            var detectedType = DetectResourceType(sourceFilePath);

            string targetFileName;
            string targetFilePath;

            if (detectedType == ResourceType.Image)
            {
                targetFileName = CreateUniqueFileName(rootFolder, Path.GetFileNameWithoutExtension(sourceFilePath), ".png");
                targetFilePath = Path.Combine(rootFolder, targetFileName);

                SaveAsPng(sourceFilePath, targetFilePath);
            }
            else
            {
                var originalFileName = Path.GetFileName(sourceFilePath);
                targetFileName = CreateUniqueFileName(rootFolder, Path.GetFileNameWithoutExtension(originalFileName), Path.GetExtension(originalFileName));
                targetFilePath = Path.Combine(rootFolder, targetFileName);

                File.Copy(sourceFilePath, targetFilePath, overwrite: false);
            }

            return (targetFileName, detectedType);
        }

        public static void SaveAsPng(string sourceFilePath, string targetFilePath)
        {
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

            if (extension == ".webp")
            {
                SaveWebpAsPng(sourceFilePath, targetFilePath);
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(sourceFilePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = File.Create(targetFilePath);
            encoder.Save(stream);
        }

        public static void SaveWebpAsPng(string sourceFilePath, string targetFilePath)
        {
            using var input = File.OpenRead(sourceFilePath);
            using var codec = SKCodec.Create(input);

            if (codec == null)
                throw new InvalidOperationException("WEBP konnte nicht gelesen werden.");

            var info = codec.Info
                .WithColorType(SKColorType.Rgba8888)
                .WithAlphaType(SKAlphaType.Unpremul);

            using var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels());

            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                throw new InvalidOperationException($"WEBP konnte nicht dekodiert werden: {result}");

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            using var output = File.OpenWrite(targetFilePath);
            data.SaveTo(output);
        }

        public static ResourceType DetectResourceType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            return extension switch
            {
                ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp"
                    => ResourceType.Image,

                ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv"
                    => ResourceType.Video,

                ".mp3" or ".wav" or ".ogg" or ".flac"
                    => ResourceType.Audio,

                ".pdf" or ".doc" or ".docx" or ".txt"
                    => ResourceType.Document,

                _ => throw new NotSupportedException($"Dateityp '{extension}' wird nicht unterstützt.")
            };
        }

        public static string CreateUniqueFileName(string targetFolder, string fileNameWithoutExtension, string extension, string uniqueFileName = "")
        {
            if (!string.IsNullOrEmpty(uniqueFileName))
                return $"{uniqueFileName}{extension}";

            return $"{fileNameWithoutExtension}_{Guid.NewGuid()}{extension}";
        }
    }
}