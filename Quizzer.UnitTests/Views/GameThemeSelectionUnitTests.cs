using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Themes;
using Quizzer.ViewModels;
using Quizzer.Views.StaticRessources;
using System.IO;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Wirkt die Designwahl bis dorthin, wo man sie sieht?
    /// <para>
    /// Der Weg ist lang: eine Zeile in <c>GameTheme</c>, ein Ordner auf der Platte, ein Pinsel
    /// im Zwischenspeicher, eine Property am Zell-ViewModel. Reißt er an einer Stelle, bleibt
    /// alles beim Alten - und zwar <b>lautlos</b>, weil der Auslieferungsstand der Rückfall ist.
    /// Ein Design, das nichts ändert, sähe genauso aus wie ein Design, das gar nicht ankommt.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameThemeSelectionUnitTests
    {
        private string wurzel = null!;
        private string vorher = null!;

        [TestInitialize]
        public void SetUp()
        {
            vorher = Quizzer.DataModels.Settings.FilePathQuizzer;

            wurzel = Path.Combine(Path.GetTempPath(), "QuizzerThemeUi-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(wurzel, "Themes", "Probe"));

            Quizzer.DataModels.Settings.FilePathQuizzer = wurzel;

            ThemeBrushes.Forget();
        }

        [TestCleanup]
        public void TearDown()
        {
            Quizzer.DataModels.Settings.FilePathQuizzer = vorher;

            ThemeBrushes.Forget();

            try
            {
                if (Directory.Exists(wurzel))
                    Directory.Delete(wurzel, recursive: true);
            }
            catch
            {
                // Ein liegengebliebener Temp-Ordner faerbt keinen Test rot.
            }
        }

        /// <summary>Schreibt ein winziges, gueltiges PNG an die genannte Stelle.</summary>
        private void LegeBildAn(string relativerPfad)
        {
            // Ein 1x1-PNG. Es geht nur darum, dass WPF es laden kann - der Inhalt ist gleich.
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

            var voll = Path.Combine(wurzel, relativerPfad);

            Directory.CreateDirectory(Path.GetDirectoryName(voll)!);
            File.WriteAllBytes(voll, png);
        }

        private static GameGridCoordinateViewModel BaueZelle()
        {
            var frage = new DefaultQuestion
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                DesignationShort = "P",
                Points = 100,
                Difficulty = Difficulty.Level1,
            };

            var spiel = new Game { Id = Guid.NewGuid(), Designation = "Probe", Phase = 1 };

            var zelle = new GameGridCoordinate
            {
                Id = Guid.NewGuid(),
                GameId = spiel.Id,
                Game = spiel,
                Phase = 1,
                QuestionBaseId = frage.Id,
                QuestionBase = frage,
            };

            zelle.CalculateAndSetCurrentPoints();

            return new GameGridCoordinateViewModel(zelle);
        }

        /// <summary>
        /// Ohne Design zeichnet die Zelle mit dem Auslieferungsstand - Bezugspunkt fuer alles
        /// Weitere.
        /// </summary>
        [TestMethod]
        public void WithoutAThemeTheCellUsesTheShippedBrush()
        {
            ThemeBrushes.SetCurrent(null);

            var zelle = BaueZelle();

            Assert.AreSame(ThemeBrushes.Standard.Zelle, zelle.IsDoneBrush,
                "Ohne Design muss der mitgelieferte Pinsel kommen.");
        }

        /// <summary>
        /// Der Kern: bringt das Design eine eigene Zelltextur mit, zeichnet die Zelle damit -
        /// und zwar mit einem <b>anderen</b> Pinsel als ohne Design.
        /// </summary>
        [TestMethod]
        public void AThemeWithItsOwnTextureChangesTheCellBrush()
        {
            LegeBildAn(Path.Combine("Themes", "Probe", "CellBackground.png"));

            var design = new GameTheme
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                FolderName = "Probe",
            };

            Assert.IsTrue(ThemeAssets.IsOwn("Probe", "CellBackground.png"),
                "Das Testbild liegt nicht dort, wo es liegen muss - dann misst der Rest nichts.");

            Assert.IsTrue(ThemeBrushes.SetCurrent(design),
                "Die Umstellung meldet keine Aenderung.");

            var zelle = BaueZelle();

            Assert.AreNotSame(ThemeBrushes.Standard.Zelle, zelle.IsDoneBrush,
                "Die Zelle zeichnet weiter mit dem mitgelieferten Pinsel - die Designwahl "
                + "kommt nicht an.");

            Assert.IsInstanceOfType<ImageBrush>(zelle.IsDoneBrush,
                "Die eigene Textur wurde nicht als Bildpinsel geladen.");
        }

        /// <summary>
        /// Die Gegenrichtung, und die wichtigere: was das Design <b>nicht</b> mitbringt, bleibt
        /// beim Auslieferungsstand. Genau das ist die Zusage "das bestehende Design bleibt".
        /// </summary>
        [TestMethod]
        public void WhatTheThemeLacksKeepsTheShippedBrush()
        {
            LegeBildAn(Path.Combine("Themes", "Probe", "CellBackground.png"));

            var design = new GameTheme
            {
                Id = Guid.NewGuid(),
                Designation = "Probe",
                FolderName = "Probe",
            };

            ThemeBrushes.SetCurrent(design);

            // Die Zelltextur ist eigen, der Hintergrund nicht.
            Assert.AreNotSame(ThemeBrushes.Standard.Zelle, ThemeBrushes.Current.Zelle,
                "Die eigene Zelltextur kam nicht an.");

            Assert.AreSame(ThemeBrushes.Standard.Hintergrund, ThemeBrushes.Current.Hintergrund,
                "Der Hintergrund wurde ersetzt, obwohl das Design keinen mitbringt.");

            Assert.AreSame(ThemeBrushes.Standard.SpaltenKopf, ThemeBrushes.Current.SpaltenKopf,
                "Der Spaltenkopf wurde ersetzt, obwohl das Design keinen mitbringt.");
        }

        /// <summary>
        /// Zurueck auf kein Design heisst zurueck auf den Auslieferungsstand. Ohne diese Probe
        /// waere nicht auszuschliessen, dass ein einmal gesetztes Design haengen bleibt.
        /// </summary>
        [TestMethod]
        public void SwitchingBackRestoresTheShippedDesign()
        {
            LegeBildAn(Path.Combine("Themes", "Probe", "CellBackground.png"));

            ThemeBrushes.SetCurrent(new GameTheme { Id = Guid.NewGuid(), FolderName = "Probe" });

            Assert.AreNotSame(ThemeBrushes.Standard.Zelle, ThemeBrushes.Current.Zelle);

            Assert.IsTrue(ThemeBrushes.SetCurrent(null), "Die Rueckkehr meldet keine Aenderung.");

            Assert.AreSame(ThemeBrushes.Standard.Zelle, ThemeBrushes.Current.Zelle,
                "Nach der Rueckkehr zeichnet das Programm nicht wieder mit dem "
                + "Auslieferungsstand.");
        }
    }
}
