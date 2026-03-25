using Quizzer.DataModels.Enumerations;
using SkiaSharp;
using System.IO;

namespace Quizzer.Base
{
    public static class FileHelper
    {
        public static (string Filename, ResourceType Type) HandleSelectedResourceFile(
            string sourceFilePath,
            string rootFolder,
            string uniqueFileName = "",
            bool allowOverride = false,
            bool cutSquare = false)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Selected file does not exist.", sourceFilePath);

            Directory.CreateDirectory(rootFolder);

            var detectedType = DetectResourceType(sourceFilePath);

            string targetFileName;
            string targetFilePath;

            if (detectedType == ResourceType.Image)
            {
                targetFileName = CreateTargetFileName(
                    rootFolder,
                    Path.GetFileNameWithoutExtension(sourceFilePath),
                    ".png",
                    uniqueFileName,
                    allowOverride);

                targetFilePath = Path.Combine(rootFolder, targetFileName);

                SaveAsPng(sourceFilePath, targetFilePath, allowOverride, cutSquare);
            }
            else
            {
                var originalFileName = Path.GetFileName(sourceFilePath);

                targetFileName = CreateTargetFileName(
                    rootFolder,
                    Path.GetFileNameWithoutExtension(originalFileName),
                    Path.GetExtension(originalFileName),
                    uniqueFileName,
                    allowOverride);

                targetFilePath = Path.Combine(rootFolder, targetFileName);

                if (File.Exists(targetFilePath) && !allowOverride)
                    throw new IOException($"File already exists: {targetFilePath}");

                File.Copy(sourceFilePath, targetFilePath, overwrite: allowOverride);
            }

            return (targetFileName, detectedType);
        }

        public static void SaveAsPng(
            string sourceFilePath,
            string targetFilePath,
            bool allowOverride = false,
            bool cutSquare = false)
        {
            if (File.Exists(targetFilePath) && !allowOverride)
                throw new IOException($"File already exists: {targetFilePath}");

            using var bitmap = LoadBitmap(sourceFilePath);

            if (bitmap == null)
                throw new InvalidOperationException("Image could not be loaded.");

            using var finalBitmap = cutSquare ? CropToMaxSquare(bitmap) : CopyBitmap(bitmap);
            SaveBitmapAsPng(finalBitmap, targetFilePath, allowOverride);
        }

        public static void SaveWebpAsPng(
            string sourceFilePath,
            string targetFilePath,
            bool allowOverride = false,
            bool cutSquare = false)
        {
            SaveAsPng(sourceFilePath, targetFilePath, allowOverride, cutSquare);
        }

        private static SKBitmap LoadBitmap(string sourceFilePath)
        {
            using var input = File.OpenRead(sourceFilePath);
            using var codec = SKCodec.Create(input);

            if (codec == null)
                throw new InvalidOperationException("Image could not be read.");

            var info = codec.Info
                .WithColorType(SKColorType.Rgba8888)
                .WithAlphaType(SKAlphaType.Unpremul);

            var bitmap = new SKBitmap(info);
            var result = codec.GetPixels(info, bitmap.GetPixels());

            if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            {
                bitmap.Dispose();
                throw new InvalidOperationException($"Image could not be decoded: {result}");
            }

            return bitmap;
        }

        private static SKBitmap CropToMaxSquare(SKBitmap source)
        {
            var size = Math.Min(source.Width, source.Height);
            var x = (source.Width - size) / 2;
            var y = (source.Height - size) / 2;

            var cropped = new SKBitmap(size, size, source.ColorType, source.AlphaType);

            using var canvas = new SKCanvas(cropped);
            var sourceRect = new SKRectI(x, y, x + size, y + size);
            var destRect = new SKRect(0, 0, size, size);

            canvas.DrawBitmap(source, sourceRect, destRect);
            canvas.Flush();

            return cropped;
        }

        private static SKBitmap CopyBitmap(SKBitmap source)
        {
            var copy = new SKBitmap(source.Width, source.Height, source.ColorType, source.AlphaType);

            using var canvas = new SKCanvas(copy);
            canvas.DrawBitmap(source, 0, 0);
            canvas.Flush();

            return copy;
        }

        private static void SaveBitmapAsPng(SKBitmap bitmap, string targetFilePath, bool allowOverride)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            using var output = new FileStream(
                targetFilePath,
                allowOverride ? FileMode.Create : FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

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

        public static string CreateTargetFileName(
            string targetFolder,
            string fileNameWithoutExtension,
            string extension,
            string uniqueFileName = "",
            bool allowOverride = false)
        {
            if (!string.IsNullOrWhiteSpace(uniqueFileName))
            {
                var preferredName = $"{uniqueFileName}{extension}";
                var preferredPath = Path.Combine(targetFolder, preferredName);

                if (allowOverride || !File.Exists(preferredPath))
                    return preferredName;
            }

            return $"{fileNameWithoutExtension}_{Guid.NewGuid()}{extension}";
        }
    }
}