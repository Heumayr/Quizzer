using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Die Spieleübersicht: der Doppelklick <b>startet</b>, in den Aufbau geht es über den Knopf
    /// in der Zeile.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> „doppelklick auf spiel startet ... links daneben
    /// ein button für edit ... also nicht mit doppelklick in den edit". Vorher führte der
    /// Doppelklick in den Spielaufbau - am Quizabend ist das der seltenere Griff.
    /// </para>
    /// <para>
    /// <b>Was hier nicht geprüft wird:</b> dass das Starten wirklich ein Spielfenster öffnet. Das
    /// braucht ein echtes Fenster mit geladenem Spiel; die Zusicherungen dafür stehen in den
    /// Durchspiel-Tests.
    /// </para>
    /// </summary>
    [TestClass]
    public class GamesViewInteractionUnitTests
    {
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

        /// <summary>
        /// Die hereingereichte Zeile schlägt die Auswahl - sonst trifft ein Doppelklick auf eine
        /// nicht ausgewählte Zeile das falsche Spiel.
        /// </summary>
        [TestMethod]
        public void TheClickedRowWinsOverTheSelection()
        {
            var ausgewaehlt = new Game { Id = Guid.NewGuid(), Designation = "Ausgewaehlt" };
            var angeklickt = new Game { Id = Guid.NewGuid(), Designation = "Angeklickt" };

            var vm = new GamesViewModel();

            vm.SelectedGames.Add(ausgewaehlt);

            Assert.AreSame(angeklickt, vm.WelchesSpiel(angeklickt),
                "Die angeklickte Zeile wurde uebergangen. Ein Doppelklick auf eine nicht "
                + "ausgewaehlte Zeile traefe damit das falsche Spiel.");

            Assert.AreSame(ausgewaehlt, vm.WelchesSpiel(null),
                "Ohne Zeile muss die Auswahl gelten - die Knoepfe links reichen nichts herein.");

            var leer = new GamesViewModel();

            Assert.IsNull(leer.WelchesSpiel(null),
                "Ohne Zeile und ohne Auswahl darf kein Spiel herauskommen.");
        }

        /// <summary>
        /// In der Zeile steht ein Knopf, der in den Aufbau führt - und er hängt am
        /// <c>EditGameCommand</c>, nicht am Doppelklick-Befehl.
        /// </summary>
        [TestMethod]
        public void EveryRowCarriesAnEditButton()
        {
            var beschriftungen = new List<string>();
            var befehle = new List<bool>();

            UiTestHost.Run(() =>
            {
                var fenster = new GamesView();

                if (fenster.DataContext is GamesViewModel vm)
                    vm.Games = [new Game { Id = Guid.NewGuid(), Designation = "Probe" }];

                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(900, 500));
                inhalt.Arrange(new Rect(0, 0, 900, 500));
                inhalt.UpdateLayout();

                var vmDaten = fenster.DataContext as GamesViewModel;

                foreach (var knopf in Descendants<Button>(inhalt))
                {
                    var text = knopf.Content?.ToString() ?? string.Empty;

                    beschriftungen.Add(text);

                    if (text.Contains("Bearbeiten", StringComparison.OrdinalIgnoreCase))
                        befehle.Add(ReferenceEquals(knopf.Command, vmDaten?.EditGameCommand));
                }

                fenster.Close();
            });

            Assert.IsTrue(beschriftungen.Any(b => b.Contains("Bearbeiten", StringComparison.OrdinalIgnoreCase)),
                "Es gibt keinen Knopf zum Bearbeiten. Dann kommt niemand mehr in den Spielaufbau, "
                + "seit der Doppelklick startet. Gefunden wurde: "
                + string.Join(" | ", beschriftungen));

            Assert.IsTrue(befehle.Count > 0 && befehle.All(b => b),
                "Der Knopf haengt nicht am EditGameCommand. Dann fuehrt er irgendwohin, nur nicht "
                + "in den Aufbau.");
        }
    }
}
