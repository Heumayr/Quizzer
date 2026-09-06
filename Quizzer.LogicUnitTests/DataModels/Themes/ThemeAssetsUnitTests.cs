using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.DataModels.Themes;

namespace Quizzer.LogicUnitTests.DataModels.Themes
{
    /// <summary>
    /// Die Rückfall-Regel eines Designs: was der Design-Ordner nicht mitbringt, kommt aus dem
    /// Auslieferungsstand.
    /// <para>
    /// Daran hängt die Zusage, dass das bestehende Design bleibt. Fiele sie aus, sähe ein Spiel
    /// mit einem halb gefüllten Design schwarz aus statt wie bisher - und zwar erst auf dem
    /// Beamer, vor Gästen.
    /// </para>
    /// </summary>
    [TestClass]
    public class ThemeAssetsUnitTests
    {
        private string wurzel = null!;
        private string vorher = null!;

        [TestInitialize]
        public void SetUp()
        {
            vorher = Settings.FilePathQuizzer;

            wurzel = Path.Combine(Path.GetTempPath(), "QuizzerThemeTest-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(wurzel);

            Settings.FilePathQuizzer = wurzel;
        }

        [TestCleanup]
        public void TearDown()
        {
            Settings.FilePathQuizzer = vorher;

            try
            {
                if (Directory.Exists(wurzel))
                    Directory.Delete(wurzel, recursive: true);
            }
            catch
            {
                // Ein liegengebliebener Temp-Ordner ist kein Grund, einen Test rot zu faerben.
            }
        }

        private void LegeDateiAn(string relativerPfad)
        {
            var voll = Path.Combine(wurzel, relativerPfad);

            Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
            File.WriteAllText(voll, "x");
        }

        /// <summary>Ohne Design kommt alles aus dem Datenverzeichnis.</summary>
        [TestMethod]
        public void WithoutAThemeEverythingComesFromTheShippedFolder()
        {
            foreach (var textur in ThemeAssets.Texturen)
            {
                Assert.AreEqual(Path.Combine(wurzel, textur.Dateiname),
                    ThemeAssets.Resolve(null, textur.Dateiname),
                    $"{textur.Beschriftung} wird nicht aus dem Auslieferungsstand geholt.");

                Assert.IsFalse(ThemeAssets.IsOwn(null, textur.Dateiname),
                    "Ohne Design kann keine Textur eigen sein.");
            }
        }

        /// <summary>Was das Design mitbringt, gewinnt.</summary>
        [TestMethod]
        public void AnOwnTextureWins()
        {
            LegeDateiAn(Path.Combine("Themes", "Probe", "Background.png"));

            Assert.AreEqual(Path.Combine(wurzel, "Themes", "Probe", "Background.png"),
                ThemeAssets.Resolve("Probe", "Background.png"),
                "Die eigene Textur wurde nicht genommen.");

            Assert.IsTrue(ThemeAssets.IsOwn("Probe", "Background.png"));
        }

        /// <summary>
        /// Der Kern: was das Design <b>nicht</b> mitbringt, kommt weiter aus dem
        /// Auslieferungsstand. Ohne diese Zusicherung wäre ein unvollständiges Design nicht von
        /// einem vollständigen zu unterscheiden.
        /// </summary>
        [TestMethod]
        public void WhatTheThemeLacksStillComesFromTheShippedFolder()
        {
            LegeDateiAn(Path.Combine("Themes", "Probe", "Background.png"));
            LegeDateiAn("CellBackground.png");

            Assert.AreEqual(Path.Combine(wurzel, "CellBackground.png"),
                ThemeAssets.Resolve("Probe", "CellBackground.png"),
                "Eine fehlende Textur muss aus dem Auslieferungsstand kommen.");

            Assert.IsFalse(ThemeAssets.IsOwn("Probe", "CellBackground.png"),
                "Diese Textur bringt das Design nicht mit.");

            // Und die Gegenrichtung im selben Design, damit die Aussage nicht bloss
            // \"alles kommt aus der Wurzel\" lautet.
            Assert.IsTrue(ThemeAssets.IsOwn("Probe", "Background.png"));
        }

        /// <summary>
        /// Ein Design-Ordner, den es gar nicht gibt, ist kein Fehler - es bleibt beim
        /// Auslieferungsstand. Sonst risse ein von Hand gelöschter Ordner den Spielabend ab.
        /// </summary>
        [TestMethod]
        public void AMissingThemeFolderIsHarmless()
        {
            Assert.AreEqual(Path.Combine(wurzel, "Background.png"),
                ThemeAssets.Resolve("GibtEsNicht", "Background.png"));

            Assert.IsFalse(ThemeAssets.IsOwn("GibtEsNicht", "Background.png"));
        }

        /// <summary>
        /// Die Liste der Texturen ist vollständig und widerspruchsfrei - jeder Schlüssel und
        /// jeder Dateiname kommt genau einmal vor.
        /// </summary>
        [TestMethod]
        public void TheTextureListIsConsistent()
        {
            var texturen = ThemeAssets.Texturen;

            Assert.IsTrue(texturen.Count >= 10,
                $"Nur {texturen.Count} Texturen - dann fehlt etwas.");

            Assert.AreEqual(texturen.Count, texturen.Select(t => t.Schluessel).Distinct().Count(),
                "Zwei Texturen teilen sich einen Schluessel.");

            Assert.AreEqual(texturen.Count, texturen.Select(t => t.Dateiname).Distinct().Count(),
                "Zwei Texturen teilen sich einen Dateinamen - eine wuerde die andere ueberschreiben.");

            foreach (var t in texturen)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Beschriftung),
                    $"{t.Schluessel} hat keine Beschriftung - die Zeile im Editor bliebe leer.");

                Assert.IsFalse(string.IsNullOrWhiteSpace(t.Erklaerung),
                    $"{t.Schluessel} sagt nicht, wo die Textur auftaucht.");

                StringAssert.EndsWith(t.Dateiname, ".png",
                    $"{t.Schluessel} zeigt nicht auf eine PNG-Datei.");
            }
        }
    }
}
