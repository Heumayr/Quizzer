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