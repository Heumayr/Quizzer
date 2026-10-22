using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Demo;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.ViewModels;
using Quizzer.Views.GameViews;
using Quizzer.DataModels.Enumerations;
using Quizzer.Views.HelperViewModels;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static Quizzer.Views.HelperViewModels.GridBuilder;

namespace Quizzer.UnitTests.Bildschirmfotos
{
    /// <summary>
    /// Bildschirmfotos aus der CI (W1, 2026-10-22): das Demospiel „Alle Fragetypen" wird mit
    /// denselben ViewModels wie im Programm durchgespielt, und Beamer, Fragefenster und
    /// Ergebnisfenster werden je Schritt als PNG gezeichnet - dazu jedes Fenster der Anwendung.
    /// <para>
    /// <b>Warum:</b> keine der Ansichten seit M9 war je unter Windows zu sehen - weder für den
    /// Nutzer ohne Programmstart noch in den Cloud-Sitzungen, in denen WPF nicht läuft. Die CI
    /// hängt den Ordner als Artefakt „bildschirmfotos" an jeden Lauf.
    /// </para>
    /// <para>
    /// Läuft nur mit <c>QUIZZER_BILDSCHIRMFOTOS</c> (Zielordner), sonst „nicht schlüssig" - und in
    /// der CI in einem eigenen Schritt, weil er die Demodaten in die Testdatenbank schreibt.
    /// </para>
    /// </summary>
    [TestClass]
    [TestCategory(Kategorie)]
    public class BildschirmfotosUnitTests
    {
        public const string Kategorie = "Bildschirmfotos";

        private const double BeamerBreite = 1600;
        private const double BeamerHoehe = 900;

        private string ziel = null!;
        private int nummer;
        private readonly List<string> leer = new();
        private readonly List<string> ohneRichtigRahmen = new();
        private string vorsilbe = "spiel";

        [TestInitialize]
        public void SetUp()
        {
            var ordner = Environment.GetEnvironmentVariable("QUIZZER_BILDSCHIRMFOTOS");

            if (string.IsNullOrWhiteSpace(ordner))
                Assert.Inconclusive("QUIZZER_BILDSCHIRMFOTOS ist nicht gesetzt - keine Bildschirmfotos.");

            ziel = ordner!;
            Directory.CreateDirectory(ziel);

            // Der Runner laeuft auf Englisch; das Programm nimmt die Windows-Sprache. Damit die Fotos
            // zeigen, was ein deutsches Windows zeigt (sonst „11/9/1989" statt „09.11.1989").
            var deutsch = System.Globalization.CultureInfo.GetCultureInfo("de-AT");
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = deutsch;
            System.Globalization.CultureInfo.CurrentCulture = deutsch;
            UiTestHost.Run(() => System.Globalization.CultureInfo.CurrentCulture = deutsch);

            // Ein Auftritt startet bei Deckkraft 0 - fotografiert sähe alles blass aus.
            Bewegung.Aktiv = false;
            UserPrompt.Current = new RecordingUserPrompt(answer: true);
            TestEnvironment.ClearSwallowedExceptions();
        }

        [TestCleanup]
        public void TearDown() => UserPrompt.Reset();

        [TestMethod]
        public async Task TheTypeShowIsPhotographedStepByStep()
        {
            var typenschau = await DemoDataSeeder.CreateTypenschauAsync();

            Game spiel;
            using (var ctrl = new GamesController())
                spiel = (await ctrl.GetAsync(typenschau.Spiel.Id))!;

            var zellen = spiel.GameGridCoordinates
                .Where(z => z.QuestionBase != null)
                .OrderBy(z => z.X).ThenBy(z => z.Y)
                .ToList();

            var beamer = new GamePlayerViewModel { Game = spiel, GameGridVMs = Raster(spiel) };

            UiTestHost.Run(() => Fotografiere<GamePlayerView>(beamer, "raster", "beamer", BeamerBreite, BeamerHoehe));

            foreach (var zelle in zellen)
            {
                var vm = new TestableCurrentQuestionViewModel { Coordinate = zelle, GamePlayerViewModel = beamer };
                await vm.LoadForTestAsync();

                var frage = $"{zelle.X + 1}{(char)('a' + zelle.Y)}-{zelle.QuestionBase!.Typ}";
                var schritt = 0;

                await vm.StartStepCommnad!.ExecuteAsync(null);
                Schritt(vm, beamer, frage, ++schritt);

                while (vm.NextStep != null && schritt < 12)
                {
                    await vm.NextStepCommnad!.ExecuteAsync(null);
                    Schritt(vm, beamer, frage, ++schritt);
                }

                UiTestHost.Run(() =>
                {
                    Fotografiere<QuestionMasterView>(vm, frage, "spielleiter", 1400, 860);
                    Fotografiere<PlayersResultView>(vm.PlayersResultViewModel!, frage, "bewertung", 1300, 800);
                });
            }

            TestEnvironment.ThrowIfAnythingWasSwallowed();

            Assert.IsTrue(nummer >= zellen.Count * 3, $"Nur {nummer} Bilder fuer {zellen.Count} Fragen.");
            Assert.AreEqual(0, leer.Count, "Diese Bilder sind einfarbig - dort ist nichts zu sehen: " + string.Join(", ", leer));
            Assert.AreEqual(0, ohneRichtigRahmen.Count,
                "Bei dieser Multiple-Choice-Aufloesung ist die richtige Antwort nicht gruen umrahmt: " + string.Join(", ", ohneRichtigRahmen));
        }

        /// <summary>Jedes Fenster der Anwendung, so wie es ohne Daten aufgeht.</summary>
        [TestMethod]
        public void EveryWindowIsPhotographed()
        {
            vorsilbe = "fenster";

            UiTestHost.Run(() =>
            {
                foreach (var typ in Views.AllWindowsBuildUnitTests.Fenster)
                {
                    using var aufraeumer = new Views.FensterAufraeumer(typ);
                    Speichere(aufraeumer.Fenster, $"fenster-{typ.Name}", 1200, 800, pruefeLeer: false);
                }
            });
        }

        private void Schritt(CurrentQuestionViewModel vm, GamePlayerViewModel beamer, string frage, int schritt)
        {
            // Der Getter schiebt den Schritt auf den Beamer - wie die Bindung im Fragefenster.
            _ = vm.CurrentStepContext;

            UiTestHost.Run(() =>
            {
                var bild = Fotografiere<GamePlayerView>(beamer, $"{frage}-{schritt:00}", "beamer", BeamerBreite, BeamerHoehe);

                // Die Gegenprobe zum ersten Fotolauf (2026-10-22): dort war die Aufloesung von
                // der Frage davor nicht zu unterscheiden.
                if (vm.Question?.Typ == QuestionType.MultipleChoice && vm.CurrentStep?.IsFinish == true && !HatRichtigGruen(bild))
                    ohneRichtigRahmen.Add($"{frage}-{schritt:00}");
            });
        }

        private static GameGridVMs Raster(Game spiel)
        {
            var raster = new GameGridVMs();

            foreach (var zelle in spiel.GameGridCoordinates.OrderBy(z => z.Y).ThenBy(z => z.X))
                raster.CellVMs.Add(new GameGridCoordinateViewModel(zelle) { CellView = CellView.PlayField });

            return raster;
        }

        private BitmapSource Fotografiere<TFenster>(object dataContext, string name, string wer, double breite, double hoehe)
            where TFenster : Window, new()
        {
            var fenster = new TFenster { DataContext = dataContext };

            try
            {
                return Speichere(fenster, $"{name}-{wer}", breite, hoehe, pruefeLeer: true);
            }
            finally
            {
                fenster.Close();
            }
        }

        private BitmapSource Speichere(Window fenster, string name, double breite, double hoehe, bool pruefeLeer)
        {
            var inhalt = (FrameworkElement)fenster.Content;

            for (var runde = 0; runde < 2; runde++)
            {
                inhalt.Measure(new Size(breite, hoehe));
                inhalt.Arrange(new Rect(0, 0, breite, hoehe));
                inhalt.UpdateLayout();

                // Bindungen, die erst nach dem Auslegen kommen, abarbeiten lassen. DataBind und nicht
                // ContextIdle: eine Daueranimation haelt den Thread nie leer, und der erste Lauf
                // hing genau hier eine halbe Stunde.
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
            }

            var bild = new RenderTargetBitmap((int)breite, (int)hoehe, 96, 96, PixelFormats.Pbgra32);

            var grund = new DrawingVisual();
            using (var dc = grund.RenderOpen())
                dc.DrawRectangle(fenster.Background ?? Brushes.Black, null, new Rect(0, 0, breite, hoehe));

            bild.Render(grund);
            bild.Render(inhalt);

            var datei = Path.Combine(ziel, $"{vorsilbe}-{++nummer:000}-{Bereinige(name)}.png");

            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bild));

            using (var strom = File.Create(datei))
                png.Save(strom);

            if (pruefeLeer && Einfarbig(bild))
                leer.Add(Path.GetFileName(datei));

            return bild;
        }

        /// <summary>Ob irgendwo der Rahmen der richtigen Antwort (#4CD964) zu sehen ist.</summary>
        private static bool HatRichtigGruen(BitmapSource bild)
        {
            var schritt = bild.PixelWidth * 4;
            var punkte = new byte[schritt * bild.PixelHeight];
            bild.CopyPixels(punkte, schritt, 0);

            var treffer = 0;

            for (var i = 0; i < punkte.Length; i += 4)
            {
                // Pbgra32: Blau, Gruen, Rot, Deckkraft.
                if (Math.Abs(punkte[i] - 0x64) < 12 && Math.Abs(punkte[i + 1] - 0xD9) < 12 && Math.Abs(punkte[i + 2] - 0x4C) < 12 && ++treffer > 200)
                    return true;
            }

            return false;
        }

        private static bool Einfarbig(BitmapSource bild)
        {
            var schritt = bild.PixelWidth * 4;
            var punkte = new byte[schritt * bild.PixelHeight];
            bild.CopyPixels(punkte, schritt, 0);

            var erstes = BitConverter.ToInt32(punkte, 0);

            for (var i = 0; i < punkte.Length; i += 4 * 97)
            {
                if (BitConverter.ToInt32(punkte, i) != erstes)
                    return false;
            }

            return true;
        }

        private static string Bereinige(string name)
            => string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ' ' ? '_' : c));
    }
}
