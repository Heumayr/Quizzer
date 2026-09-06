using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Transfer;
using Quizzer.Views;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Der Bedienweg für Export und Import.
    /// <para>
    /// <b>Der heikle Punkt ist der Unterschied zwischen „keine Mitspieler" und „abgebrochen".</b>
    /// Der Nutzer hat beides genannt: „die spieler können beim import gewählt werden (oder auch
    /// keine)". Würden beide gleich behandelt, legte ein Abbruch trotzdem ein Spiel an - oder
    /// „ohne Mitspieler" täte gar nichts.
    /// </para>
    /// </summary>
    [TestClass]
    public class GameTransferUiUnitTests
    {
        private RecordingUserPrompt prompt = null!;

        [TestInitialize]
        public void SetUp()
        {
            prompt = new RecordingUserPrompt(answer: true);
            UserPrompt.Current = prompt;
        }

        [TestCleanup]
        public void TearDown()
        {
            UserPrompt.Reset();
            FilePicker.Reset();
            ImportPlayerSelection.Reset();
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);

            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is T hit)
                    yield return hit;

                foreach (var deeper in Descendants<T>(child))
                    yield return deeper;
            }
        }

        /// <summary>Beide Knöpfe stehen in der Spieleübersicht und hängen an ihren Befehlen.</summary>
        [TestMethod]
        public void TheGamesWindowOffersExportAndImport()
        {
            var beschriftungen = new List<string>();
            var richtigVerdrahtet = 0;

            UiTestHost.Run(() =>
            {
                var fenster = new GamesView();
                var vm = fenster.DataContext as GamesViewModel;
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(900, 500));
                inhalt.Arrange(new Rect(0, 0, 900, 500));
                inhalt.UpdateLayout();

                foreach (var knopf in Descendants<Button>(inhalt))
                {
                    var text = knopf.Content?.ToString() ?? string.Empty;

                    beschriftungen.Add(text);

                    if (text.StartsWith("Exportieren", StringComparison.Ordinal)
                        && ReferenceEquals(knopf.Command, vm?.ExportGameCommand))
                        richtigVerdrahtet++;

                    if (text.StartsWith("Importieren", StringComparison.Ordinal)
                        && ReferenceEquals(knopf.Command, vm?.ImportGameCommand))
                        richtigVerdrahtet++;
                }

                fenster.Close();
            });

            Assert.AreEqual(2, richtigVerdrahtet,
                "Exportieren und Importieren stehen nicht beide da und haengen nicht beide am "
                + "richtigen Befehl. Gefunden wurde: " + string.Join(" | ", beschriftungen));
        }

        /// <summary>
        /// Ohne gewähltes Spiel wird nicht exportiert, sondern gesagt warum.
        /// </summary>
        [TestMethod]
        public async Task WithoutASelectedGameTheExportSaysSo()
        {
            var gefragt = false;

            FilePicker.Current = new RecordingFilePicker(
                saveTarget: () => { gefragt = true; return null; },
                openTarget: () => null);

            var vm = new GamesViewModel();

            await TestEnvironment.RunCommandAsync(vm.ExportGameCommand);

            Assert.IsFalse(gefragt,
                "Es wurde nach einem Speicherort gefragt, obwohl kein Spiel gewaehlt ist.");

            Assert.AreEqual(1, prompt.Informs.Count,
                "Der Nutzer erfaehrt nicht, warum nichts geschieht.");
        }

        /// <summary>
        /// <b>Abgebrochen ist nicht dasselbe wie „ohne Mitspieler".</b> Bricht die
        /// Mitspielerauswahl ab, wird nichts angelegt.
        /// </summary>
        [TestMethod]
        public async Task CancellingThePlayerSelectionImportsNothing()
        {
            var datei = Path.Combine(Path.GetTempPath(), "gibtesnicht" + GameExporter.Extension);

            FilePicker.Current = new RecordingFilePicker(
                saveTarget: () => null,
                openTarget: () => datei);

            // null heisst abgebrochen.
            ImportPlayerSelection.Handler = () => null;

            var vm = new GamesViewModel();

            await TestEnvironment.RunCommandAsync(vm.ImportGameCommand);

            Assert.AreEqual(0, prompt.Informs.Count,
                "Nach einem Abbruch wurde etwas gemeldet - dann ist der Import gelaufen. "
                + "Gelesen wurde: " + string.Join(" | ", prompt.Informs.Select(i => i.Message)));
        }

        /// <summary>
        /// Ein Dateiname entsteht aus der Bezeichnung, auch wenn sie Zeichen trägt, die Windows
        /// nicht mag.
        /// </summary>
        [TestMethod]
        public void ADisplayNameBecomesAUsableFileName()
        {
            Assert.AreEqual("Quizabend 1", GamesViewModel.SaubererDateiname("Quizabend 1"));

            var heikel = GamesViewModel.SaubererDateiname("Quiz: Musik/Film?");

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                Assert.IsFalse(heikel.Contains(c),
                    $"Der Dateiname traegt noch das Zeichen '{c}': {heikel}");
            }

            Assert.AreEqual("Spiel", GamesViewModel.SaubererDateiname("   "),
                "Eine leere Bezeichnung ergibt keinen Dateinamen.");
        }

        /// <summary>Ein Dateiwähler mit vorgegebenen Antworten.</summary>
        private sealed class RecordingFilePicker(Func<string?> saveTarget, Func<string?> openTarget)
            : IFilePicker
        {
            public string? AskForSaveTarget(string titel, string vorschlag, string filter)
                => saveTarget();

            public string? AskForExistingFile(string titel, string filter)
                => openTarget();
        }
    }
}
