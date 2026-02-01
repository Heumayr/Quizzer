using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Text;

namespace Quizzer.Base
{
    public static class Settings
    {
        public static IConfiguration Configuration { get; private set; } = null!;

        public static string FilePathQuizzer { get; set; } = string.Empty;

        public static void LoadSettings()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            Configuration = builder.Build();

            FilePathQuizzer = Configuration["AppSettings:FilePathQuizzer"]! ?? throw new InvalidOperationException("Missing FilePathQuestions");
        }
    }
}