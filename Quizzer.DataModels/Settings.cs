using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace Quizzer.DataModels
{
    public static class Settings
    {
        public static IConfiguration Configuration { get; private set; } = null!;

        public static string FilePathQuizzer { get; set; } = string.Empty;

        public static string ResourceRootFolder => Path.Combine(FilePathQuizzer, "Resources");
        public static string AudioPlaceholderFile => Path.Combine(FilePathQuizzer, "AudioPlaceholderFile.png");
        public static string BackgroundImagePath => Path.Combine(FilePathQuizzer, "Background.png");
        public static string PlaceholderPlayerImagePath => Path.Combine(FilePathQuizzer, "PlaceholderPlayer.png");

        public static string CellBackgroundImagePath => Path.Combine(FilePathQuizzer, "CellBackground.png");
        public static string CellBackgroundHoverImagePath => Path.Combine(FilePathQuizzer, "CellBackgroundHover.png");
        public static string CellBackgroundIsDoneImagePath => Path.Combine(FilePathQuizzer, "CellBackgroundIsDone.png");
        public static string HeaderColumnBackgroundImagePath => Path.Combine(FilePathQuizzer, "HeaderColumnBackground.png");
        public static string HeaderRowBackgroundImagePath => Path.Combine(FilePathQuizzer, "HeaderRowBackground.png");

        public static string ConnectionString { get; set; } = string.Empty;

        public static void LoadSettings()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            FilePathQuizzer = Configuration["AppSettings:FilePathQuizzer"]! ?? throw new InvalidOperationException("Missing FilePathQuestions");
            ConnectionString = Configuration["AppSettings:MsSqlConString"]! ?? throw new InvalidOperationException("Missing MsSqlConString");
        }
    }
}