using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.RegularExpressions;

namespace Quizzer.LogicUnitTests.LocalBuzzer
{
    /// <summary>
    /// Der Vertrag innerhalb der Telefonseite: was das JavaScript benutzt, muss es auch geben.
    /// <para>
    /// Anlass 2026-09-05: das JavaScript bekam eine neue Meldungsflaeche mit den Klassen
    /// <c>layout-notice</c>, <c>notice-text</c> und <c>notice-btn</c> - in der CSS stand keine
    /// davon. Sichtbar geworden waere das erst am Telefon, und dort haette es wie ein
    /// Verbindungsfehler ausgesehen: eine unformatierte Meldung ohne erkennbaren Knopf.
    /// </para>
    /// </summary>
    [TestClass]
    public class PhonePageContractUnitTests
    {
        private static string WwwRoot => Path.Combine(AppContext.BaseDirectory, "wwwroot");

        private static IEnumerable<string> ScriptFiles() =>
            Directory.EnumerateFiles(Path.Combine(WwwRoot, "JS"), "*.js", SearchOption.AllDirectories);

        private static string ReadStyles() =>
            File.ReadAllText(Path.Combine(WwwRoot, "styles", "mainstyles.css"));

        [TestMethod]
        public void TheDeliveredFolderCarriesTheWholePage()
        {
            Assert.IsTrue(Directory.Exists(WwwRoot),
                "Ohne wwwroot im Ausgabeverzeichnis liefert der Server gar nichts aus.");

            foreach (var datei in new[] { "index.html", "styles/mainstyles.css", "JS/index.js" })
            {
                Assert.IsTrue(File.Exists(Path.Combine(WwwRoot, datei.Replace('/', Path.DirectorySeparatorChar))),
                    $"{datei} fehlt im Ausgabeverzeichnis.");
            }
        }

        /// <summary>
        /// Jede CSS-Klasse, die das JavaScript vergibt, muss in der Stilvorlage vorkommen.
        /// </summary>
        [TestMethod]
        public void EveryClassTheScriptAssignsExistsInTheStylesheet()
        {
            var styles = ReadStyles();
            var fehlend = new List<string>();

            foreach (var datei in ScriptFiles())
            {
                var inhalt = File.ReadAllText(datei);

                foreach (Match treffer in Regex.Matches(inhalt, @"className\s*=\s*""([^""]+)"""))
                {
                    foreach (var klasse in treffer.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        // Wortgrenze am Ende: sonst gilt ".notice-btn" auch dann als vorhanden,
                        // wenn dort nur ".notice-btn-etwas" steht.
                        var muster = $@"\.{Regex.Escape(klasse)}(?![\w-])";

                        if (!Regex.IsMatch(styles, muster) && !fehlend.Any(f => f.StartsWith(klasse)))
                            fehlend.Add($"{klasse} (aus {Path.GetFileName(datei)})");
                    }
                }
            }

            Assert.AreEqual(0, fehlend.Count,
                "Diese Klassen vergibt das JavaScript, aber die Stilvorlage kennt sie nicht: "
                + string.Join(", ", fehlend));
        }

        /// <summary>
        /// Die Seite laedt genau die Bausteine, die es gibt - ein vertippter Pfad faellt im
        /// Browser sonst nur in der Entwicklerkonsole auf, die auf einem Telefon niemand oeffnet.
        /// </summary>
        [TestMethod]
        public void EveryModuleImportPointsAtAnExistingFile()
        {
            var fehlend = new List<string>();

            foreach (var datei in ScriptFiles())
            {
                var ordner = Path.GetDirectoryName(datei)!;
                var inhalt = File.ReadAllText(datei);

                foreach (Match treffer in Regex.Matches(inhalt, @"from\s+""(\.[^""]+)"""))
                {
                    var ziel = Path.GetFullPath(Path.Combine(ordner, treffer.Groups[1].Value));

                    if (!File.Exists(ziel))
                        fehlend.Add($"{Path.GetFileName(datei)} -> {treffer.Groups[1].Value}");
                }
            }

            Assert.AreEqual(0, fehlend.Count,
                "Diese Verweise gehen ins Leere: " + string.Join(", ", fehlend));
        }

        /// <summary>
        /// Die Seite spricht Deutsch mit echten Umlauten. Ein umgeschriebenes "ue" faellt beim
        /// Bauen nicht auf, steht aber vor jedem Gast auf dem Tisch.
        /// </summary>
        [TestMethod]
        public void TheVisibleTextsKeepTheirUmlauts()
        {
            var seite = File.ReadAllText(Path.Combine(WwwRoot, "JS", "index.js"));

            StringAssert.Contains(seite, "Verbindung verloren");

            Assert.IsFalse(Regex.IsMatch(seite, @"\bVerbindungsabbruech|\bmoeglich|\buebermittelt"),
                "Im sichtbaren Text steht eine umgeschriebene Umlautform.");
        }
    }
}
