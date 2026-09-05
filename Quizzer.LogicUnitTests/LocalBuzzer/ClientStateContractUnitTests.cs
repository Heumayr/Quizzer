using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Buzzer;
using System.Reflection;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Der Vertrag zwischen <see cref="ClientLayoutStateDto"/> und der Telefonseite.
    /// <para>
    /// Zwischen C# und JavaScript prüft der Übersetzer nichts. Der Hub schickt das DTO, SignalR
    /// wandelt die Namen in camelCase, und <c>index.js</c> liest sie einzeln aus - jedes Feld
    /// mit einem <c>??</c>-Rückfall. Ein umbenanntes Property meldet deshalb keinen Fehler,
    /// sondern liefert still den Standardwert: das Telefon zeigt "nicht verbunden", obwohl alles
    /// läuft. Genau dieser Fall war am 2026-09-05 gemeldet worden - damals fehlten zwei
    /// Editor-Einstellungen im <c>BuzzerKeySelector</c>.
    /// </para>
    /// </summary>
    [TestClass]
    public class ClientStateContractUnitTests
    {
        private static string ScriptFolder =>
            Path.Combine(AppContext.BaseDirectory, "wwwroot", "JS");

        private static string ReadAllScripts() =>
            string.Join("\n", Directory
                .EnumerateFiles(ScriptFolder, "*.js", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        /// <summary>SignalR wandelt <c>PlayerName</c> in <c>playerName</c>.</summary>
        private static string CamelCase(string name) =>
            char.ToLowerInvariant(name[0]) + name[1..];

        /// <summary>
        /// Jedes Feld, das der Server schickt, wird auf der Telefonseite auch gelesen.
        /// </summary>
        [TestMethod]
        public void ThePhoneReadsEveryFieldTheServerSends()
        {
            var skripte = ReadAllScripts();

            Assert.IsTrue(skripte.Length > 1000,
                $"Nur {skripte.Length} Zeichen JavaScript gefunden - der Durchlauf greift ins Leere.");

            var fehlend = typeof(ClientLayoutStateDto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => CamelCase(p.Name))
                .Where(name => !skripte.Contains(name, StringComparison.Ordinal))
                .ToList();

            Assert.AreEqual(0, fehlend.Count,
                "Diese Felder schickt der Server, aber die Telefonseite liest sie nirgends - "
                + "sie fallen still auf ihren Standardwert zurück: "
                + string.Join(", ", fehlend));
        }

        /// <summary>
        /// Die Gegenrichtung: die Telefonseite liest kein Feld aus, das es am DTO nicht gibt.
        /// Ein solcher Zugriff liefert <c>undefined</c> und schlägt in den <c>??</c>-Rückfall,
        /// ohne dass irgendwo etwas auffällt.
        /// </summary>
        [TestMethod]
        public void ThePhoneReadsNoFieldTheServerDoesNotSend()
        {
            var skripte = ReadAllScripts();

            var bekannt = typeof(ClientLayoutStateDto)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => CamelCase(p.Name))
                .ToHashSet(StringComparer.Ordinal);

            // Nur Zugriffe auf das Server-Objekt zaehlen, nicht auf den eigenen Zustand.
            var zugriffe = System.Text.RegularExpressions.Regex
                .Matches(skripte, @"serverState\.([A-Za-z_][A-Za-z0-9_]*)")
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            Assert.IsTrue(zugriffe.Count > 0,
                "Kein einziger serverState-Zugriff gefunden - das Muster greift nicht, "
                + "und die Zusicherung waere immer gruen.");

            var unbekannt = zugriffe.Where(z => !bekannt.Contains(z)).ToList();

            Assert.AreEqual(0, unbekannt.Count,
                "Die Telefonseite liest Felder, die das DTO nicht hat - sie sind immer "
                + "undefined: " + string.Join(", ", unbekannt));
        }
    }
}
