using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Wer angemeldet ist, steht im Startfenster - und der Wechsel geht ohne Neustart.
    /// <para>
    /// <b>B38, B04, B06.</b> Die Anmeldung entscheidet, welche Fragen sichtbar sind und wem eine
    /// neue Frage gehört. Danach stand sie in keinem einzigen Fenster: <c>MainWindow.xaml</c>
    /// trug fünf Knöpfe und keinen Namen, <c>MainViewModel</c> erwähnte <c>Session</c> mit null
    /// Treffern, und <c>Session.SignOut</c> hatte außerhalb der Tests keinen Aufrufer. Wer sich
    /// beim Start vergriff, sah seine eigenen Fragen nicht und kam nur über Programm schließen
    /// und neu starten zurück.
    /// </para>
    /// </summary>
    [TestClass]
    public class AngemeldetAlsUnitTests
    {
        private static Player Leiter(string name) => new()
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            IsModerator = true,
        };

        [TestCleanup]
        public void TearDown()
        {
            Session.SignOut();
            MainViewModel.ResetSwitchModeratorHandler();
        }

        /// <summary>Die Zeile nennt den Namen des Angemeldeten.</summary>
        [TestMethod]
        public void TheLineNamesWhoIsSignedIn()
        {
            var anna = Leiter("Anna");

            Assert.IsTrue(Session.SignIn(anna), "Die Anmeldung ist nicht gelungen.");

            var vm = new MainViewModel();

            StringAssert.Contains(vm.AngemeldetAls, anna.CalculatedDisplayName,
                "Die Zeile nennt nicht, wer angemeldet ist: " + vm.AngemeldetAls);
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ohne sie wäre die obige auch dann grün, wenn die Zeile
        /// immer irgendeinen Namen zeigte - etwa den zuletzt geladenen aus der Datenbank.
        /// </summary>
        [TestMethod]
        public void WithoutASignInTheLineSaysSo()
        {
            Session.SignOut();

            var vm = new MainViewModel();

            Assert.AreEqual("Nicht angemeldet", vm.AngemeldetAls,
                "Ohne Anmeldung behauptet die Zeile trotzdem jemanden: " + vm.AngemeldetAls);
        }

        /// <summary>Nach einem gelungenen Wechsel steht der neue Name da.</summary>
        [TestMethod]
        public void AfterASwitchTheNewNameIsShown()
        {
            var anna = Leiter("Anna");
            var bert = Leiter("Bert");

            Session.SignIn(anna);

            var vm = new MainViewModel();
            var gemeldet = new List<string>();

            vm.PropertyChanged += (_, e) => gemeldet.Add(e.PropertyName ?? string.Empty);

            MainViewModel.SwitchModeratorHandler = () => Session.SignIn(bert);

            vm.SwitchModeratorCommand.Execute(null);

            StringAssert.Contains(vm.AngemeldetAls, bert.CalculatedDisplayName,
                "Nach dem Wechsel steht der alte Name da: " + vm.AngemeldetAls);

            CollectionAssert.Contains(gemeldet, nameof(MainViewModel.AngemeldetAls),
                "Die Zeile wurde nicht neu gemeldet - sie bliebe auf dem Bildschirm stehen, "
                + "wie sie war.");
        }

        /// <summary>
        /// <b>Ein Abbruch meldet niemanden ab.</b>
        /// <para>
        /// Die naheliegende Bauart wäre <c>Session.SignOut()</c> vor dem Anmeldefenster. Wer
        /// dann abbricht, stünde als niemand da - und <c>QuestionOwnership.IsVisible</c> lässt
        /// bei <c>CurrentModeratorId == null</c> nur noch die besitzerlosen Fragen durch,
        /// wortlos. Diese Zusicherung ist der Riegel dagegen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ACancelledSwitchKeepsTheCurrentSignIn()
        {
            var anna = Leiter("Anna");

            Session.SignIn(anna);

            var vm = new MainViewModel();

            MainViewModel.SwitchModeratorHandler = () => false;

            vm.SwitchModeratorCommand.Execute(null);

            Assert.AreEqual(anna.Id, Session.CurrentModeratorId,
                "Der abgebrochene Wechsel hat abgemeldet. Danach sieht der Spielleiter nur noch "
                + "die besitzerlosen Fragen, ohne dass irgendetwas darauf hinweist.");

            StringAssert.Contains(vm.AngemeldetAls, anna.CalculatedDisplayName,
                "Die Zeile hat den Namen verloren: " + vm.AngemeldetAls);
        }

        private static IEnumerable<DependencyObject> Alle(DependencyObject wurzel)
        {
            var anzahl = VisualTreeHelper.GetChildrenCount(wurzel);

            for (var i = 0; i < anzahl; i++)
            {
                var kind = VisualTreeHelper.GetChild(wurzel, i);

                yield return kind;

                foreach (var tiefer in Alle(kind))
                    yield return tiefer;
            }
        }

        /// <summary>
        /// <b>Beides ist im Fenster wirklich angebunden.</b>
        /// <para>
        /// Ohne diese Zusicherung wären alle obigen grün, während im Startfenster nach wie vor
        /// nichts steht - eine Eigenschaft, die niemand anzeigt, ist keine Anzeige.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheStartWindowShowsAndOffersBoth()
        {
            var pfade = new HashSet<string>(StringComparer.Ordinal);

            UiTestHost.Run(() =>
            {
                var fenster = new Quizzer.MainWindow();
                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(1000, 800));
                inhalt.Arrange(new Rect(0, 0, 1000, 800));
                inhalt.UpdateLayout();

                foreach (var knoten in Alle(inhalt))
                {
                    if (knoten is not FrameworkElement element)
                        continue;

                    foreach (var eigenschaft in new[]
                             { TextBlock.TextProperty, ButtonBase.CommandProperty })
                    {
                        if (BindingOperations.GetBinding(element, eigenschaft)
                            is { Path.Path: { Length: > 0 } pfad })
                        {
                            pfade.Add(pfad);
                        }
                    }
                }

                fenster.Close();
            });

            Assert.IsTrue(pfade.Contains(nameof(MainViewModel.AngemeldetAls)),
                "Das Startfenster zeigt nicht, wer angemeldet ist. Gefunden: "
                + string.Join(", ", pfade));

            Assert.IsTrue(pfade.Contains(nameof(MainViewModel.SwitchModeratorCommand)),
                "Das Startfenster bietet keinen Wechsel an - dann geht er weiterhin nur ueber "
                + "einen Neustart. Gefunden: " + string.Join(", ", pfade));
        }
    }
}
