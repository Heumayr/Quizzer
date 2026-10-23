using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.Views.HelperViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Quizzer.UnitTests.Views
{
    /// <summary>
    /// Das Meldungsfenster der App: Hinweise, Fragen und Fehler im Design der übrigen Fenster.
    /// <para>
    /// <b>Nutzerwort vom 2026-10-23 (M29):</b> „bitte benachrichtigugen in einem passenen design wie
    /// der rest der app ... eigens fenster für medlungen oder fehler". Vorher: das weiße
    /// Windows-Meldungsfenster über dem dunklen Programm, und für Fehler ein ungestaltetes Fenster
    /// mit rohem Stacktrace.
    /// </para>
    /// </summary>
    [TestClass]
    public class MeldungsfensterUnitTests
    {
        /// <summary>Jede Art sagt in Worten, was sie ist - nicht nur über Farbe - und hat ihre Knöpfe.</summary>
        [TestMethod]
        public void EachKindSaysWhatItIs()
        {
            var frage = new PromptViewModel(PromptKind.Question, "Wirklich?", "Abmelden");
            Assert.AreEqual(("Frage", "?", "Ja", Visibility.Visible), (frage.Heading, frage.Symbol, frage.PrimaryText, frage.SecondaryVisibility));

            var hinweis = new PromptViewModel(PromptKind.Info, "Gespeichert.", "");
            Assert.AreEqual(("Hinweis", "i", "OK", Visibility.Collapsed), (hinweis.Heading, hinweis.Symbol, hinweis.PrimaryText, hinweis.SecondaryVisibility));
            Assert.AreEqual("Quizzer", hinweis.Caption, "Ohne Titel stünde ein leerer Fenstertitel da.");
            Assert.AreEqual(Visibility.Collapsed, hinweis.DetailsVisibility, "Ein Hinweis hat keine technischen Einzelheiten.");
        }

        /// <summary>Ein Fehler zeigt die Meldungen der ganzen Kette lesbar; das Technische steht aufklappbar daneben.</summary>
        [TestMethod]
        public void AnErrorShowsTheWholeChainAndKeepsTheDetails()
        {
            Exception ausnahme;
            try
            {
                try { throw new InvalidOperationException("Die Datei fehlt."); }
                catch (Exception innen) { throw new ApplicationException("Das Spiel ließ sich nicht öffnen.", innen); }
            }
            catch (Exception ex) { ausnahme = ex; }

            var fehler = PromptViewModel.ForException(ausnahme);

            Assert.AreEqual(("Fehler", "!", "Schließen", Visibility.Collapsed), (fehler.Heading, fehler.Symbol, fehler.PrimaryText, fehler.SecondaryVisibility));
            StringAssert.Contains(fehler.Message, "Das Spiel ließ sich nicht öffnen.");
            StringAssert.Contains(fehler.Message, "Die Datei fehlt.", "Die innere Ursache fehlt in der Meldung - sie ist meist die verständlichere.");
            Assert.IsFalse(fehler.Message.Contains(" at ", StringComparison.Ordinal), "Der Stacktrace gehört in die Einzelheiten, nicht in die Meldung.");
            StringAssert.Contains(fehler.Details, "InvalidOperationException");
            Assert.AreEqual(Visibility.Visible, fehler.DetailsVisibility);
        }

        /// <summary>Am echten Fenster: „Ja" liefert true, „Nein" false, und beide schließen.</summary>
        [TestMethod]
        public void TheButtonsAnswerAndClose()
        {
            UiTestHost.Run(() =>
            {
                foreach (var (knopf, erwartet) in new[] { ("Ja", true), ("Nein", false) })
                {
                    var inhalt = new PromptViewModel(PromptKind.Question, "Wirklich?", "Probe") { Result = !erwartet };
                    var fenster = new PromptView(inhalt);
                    var geschlossen = false;
                    fenster.Closed += (_, _) => geschlossen = true;
                    fenster.Show();

                    var ziel = Knoepfe(fenster).Single(b => Equals(b.Content, knopf));
                    ziel.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

                    Assert.AreEqual(erwartet, inhalt.Result, $"„{knopf}“ lieferte das Falsche.");
                    Assert.IsTrue(geschlossen, $"„{knopf}“ schloss das Fenster nicht.");
                }
            });
        }

        /// <summary>Im laufenden Programm ist der Regelfall das neue Fenster, nicht mehr die Windows-Meldung.</summary>
        [TestMethod]
        public void TheDefaultIsTheAppsOwnWindow()
        {
            try
            {
                UserPrompt.UseAsDefaultForTests(null);
                Assert.IsInstanceOfType(UserPrompt.Current, typeof(WindowUserPrompt));
            }
            finally
            {
                UserPrompt.UseAsDefaultForTests(new ThrowingUserPrompt());
            }
        }

        private static IEnumerable<Button> Knoepfe(DependencyObject wurzel)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(wurzel); i++)
            {
                var kind = System.Windows.Media.VisualTreeHelper.GetChild(wurzel, i);
                if (kind is Button knopf)
                    yield return knopf;
                foreach (var tiefer in Knoepfe(kind))
                    yield return tiefer;
            }
        }
    }
}
