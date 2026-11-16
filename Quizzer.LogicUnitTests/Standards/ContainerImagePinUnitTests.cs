using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.Standards
{
    /// <summary>
    /// Jede Abbildangabe im Stapel traegt einen Digest (A03, V02). Gemessen 2026-11-16: der Stapel zog
    /// <c>mssql/server:2022-latest</c> - jedes <c>up --build</c> haette still eine neue SQL-Fassung geholt.
    /// Gehoben wird bewusst, indem man den Digest aendert.
    /// </summary>
    [TestClass]
    public class ContainerImagePinUnitTests
    {
        /// <summary>Selbst gebaute Abbilder haben keinen Digest.</summary>
        private const string EigenesAbbild = "quizzer-server:";

        private static readonly Regex ImageZeile = new(@"^\s*(?:-\s*)?image:\s*(?<ref>\S+)", RegexOptions.Multiline);
        private static readonly Regex FromZeile = new(@"^\s*FROM\s+(?<ref>\S+)", RegexOptions.Multiline);
        private static readonly Regex InSkript = new(@"(?<![\w./:-])(?<ref>(?:mcr\.microsoft\.com/[\w./-]+|\bcaddy|cloudflare/cloudflared):[\w.-]+(?:@sha256:[0-9a-f]+)?)");
        private static readonly Regex Digest = new(@"@sha256:[0-9a-f]{64}$");

        private sealed record Angabe(string Datei, string Ref);

        private static string RepoRoot()
        {
            var verzeichnis = new DirectoryInfo(AppContext.BaseDirectory);

            while (verzeichnis != null && !File.Exists(Path.Combine(verzeichnis.FullName, "Quizzer.slnx")))
                verzeichnis = verzeichnis.Parent;

            Assert.IsNotNull(verzeichnis, "Quizzer.slnx nicht gefunden.");

            return verzeichnis.FullName;
        }

        private static List<Angabe> Angaben(string root)
        {
            var dateien = new List<(string Pfad, Regex Muster)>();
            dateien.AddRange(Directory.GetFiles(Path.Combine(root, "deploy"), "*.yml").Select(p => (p, ImageZeile)));
            dateien.AddRange(Directory.GetFiles(Path.Combine(root, ".github", "workflows"), "*.yml").Select(p => (p, ImageZeile)));
            dateien.AddRange(Directory.GetFiles(Path.Combine(root, "deploy"), "*.ps1").Select(p => (p, InSkript)));
            dateien.AddRange(Directory.GetFiles(Path.Combine(root, "deploy"), "*.sh").Select(p => (p, InSkript)));
            dateien.Add((Path.Combine(root, "Quizzer.Server", "Dockerfile"), FromZeile));

            return dateien
                .SelectMany(d => d.Muster.Matches(File.ReadAllText(d.Pfad))
                    .Select(m => new Angabe(Path.GetRelativePath(root, d.Pfad), m.Groups["ref"].Value)))
                .ToList();
        }

        [TestMethod]
        public void EveryContainerImageIsPinnedByDigest()
        {
            var angaben = Angaben(RepoRoot());

            // Gegenrichtung: ein Suchmuster, das nichts findet, meldet sonst Erfolg.
            foreach (var name in new[] { "mssql/server", "caddy", "dotnet/sdk", "dotnet/aspnet", "dotnet/runtime-deps", "cloudflared" })
                Assert.IsTrue(angaben.Any(a => a.Ref.Contains(name)), $"{name} nicht gefunden - greift das Suchmuster noch?");

            var ohneDigest = angaben
                .Where(a => !a.Ref.StartsWith(EigenesAbbild, StringComparison.Ordinal) && !Digest.IsMatch(a.Ref))
                .Select(a => $"{a.Datei}: {a.Ref}")
                .ToList();

            Assert.AreEqual(0, ohneDigest.Count, "Abbild ohne Digest:" + Environment.NewLine + string.Join(Environment.NewLine, ohneDigest));
        }
    }
}
