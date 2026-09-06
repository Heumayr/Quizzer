using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.Views.QuestionTypes;
using Quizzer.DataModels.Models.QuestionTypes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Baut das Editorfenster wirklich auf. Ein gruener Build sagt nur, dass das XAML
    /// uebersetzt - ob eine Bindung ins Leere zeigt oder eine Ressource fehlt, zeigt sich
    /// erst beim Aufbau. Genau daran ist frueher der Aufruf der Anwendung gescheitert,
    /// obwohl alles gebaut hat.
    /// </summary>
    [TestClass]
    public class EditQuestionsViewRenderUnitTests
    {
        /// <summary>
        /// Fuehrt die Aktion auf einem STA-Thread mit eigener Application aus. WPF verlangt
        /// beides; MSTest liefert von sich aus keinen STA-Thread.
        /// </summary>
        /// <summary>
        /// Fuehrt die Arbeit auf dem gemeinsamen Oberflaechen-Thread aus.
        /// <para>
        /// Frueher legte diese Klasse einen eigenen STA-Thread an und fuhr dessen Dispatcher
        /// am Ende herunter. <c>Application.Current</c> ist prozessweit - danach fand keine
        /// spaetere Klasse mehr einen lebenden Dispatcher. Einzelheiten in
        /// <see cref="UiTestHost"/>.
        /// </para>
        /// </summary>
        private static void OnUiThread(Action action) => UiTestHost.Run(action);

        /// <summary>
        /// <b>Ueber das Enum gelaufen, nicht ueber eine Liste von <c>DataRow</c>.</b> Hier standen
        /// vier Zeilen fuer fuenf Fragetypen - die Aufdeckfrage fehlte, und zwar lautlos: eine
        /// nicht aufgezaehlte Zeile meldet keinen Fehler, sie laeuft einfach nicht. Eine von Hand
        /// gepflegte Liste faellt bei jedem neuen Typ wieder zurueck.
        /// </summary>
        [TestMethod]
        public void TheEditorWindow_BuildsForEveryQuestionType()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
                BaueEditorFuer(typ);
        }

        private static void BaueEditorFuer(QuestionType typ)
        {
            OnUiThread(() =>
            {
                var view = new EditQuestionsView();

                var vm = (EditQuestionViewModel)view.DataContext;
                var question = Factory.CreateNewQuestion(typ);
                question.Designation = "Testfrage";
                question.CategoryId = Guid.NewGuid();
                vm.Question = question;

                // Erzwingt Aufbau und Bindungsaufloesung, ohne das Fenster zu zeigen.
                view.Measure(new Size(900, 720));
                view.Arrange(new Rect(0, 0, 900, 720));
                view.UpdateLayout();

                Assert.AreEqual(typ, vm.Question!.Typ);
            });
        }

        /// <summary>
        /// Was der Editor ueber die Aufdeckfrage sagt, muss auch auf dem Bildschirm stehen.
        /// <para>
        /// <b>Der Anlass:</b> <c>RevealSummary</c> war geschrieben und in keiner XAML gebunden -
        /// null Treffer. Eine Eigenschaft ohne Bindung faellt nirgends auf; sie ist einfach
        /// unsichtbar. Diese Zusicherung liest den aufgebauten Baum, nicht das ViewModel.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheRevealSummaryReachesTheScreen()
        {
            OnUiThread(() =>
            {
                var view = new EditQuestionsView();

                var vm = (EditQuestionViewModel)view.DataContext;
                var frage = (RevealQuestion)Factory.CreateNewQuestion(QuestionType.Reveal);

                frage.Designation = "Wer ist das?";
                frage.CategoryId = Guid.NewGuid();
                frage.ImageFileName = "portraet.png";
                frage.Mode = RevealMode.Pixelate;
                frage.BlurStart = 40;

                vm.Question = frage;

                // Der Inhalt, nicht das Fenster: ein nie gezeigtes Fenster hat keine Vorlage
                // angewandt, und der Baum darunter ist leer.
                var inhalt = (FrameworkElement)view.Content;

                inhalt.Measure(new Size(1100, 720));
                inhalt.Arrange(new Rect(0, 0, 1100, 720));
                inhalt.UpdateLayout();

                var texte = Nachfahren<TextBlock>(inhalt).Select(t => t.Text).ToList();

                Assert.IsTrue(texte.Contains(vm.RevealSummary, StringComparer.Ordinal),
                    "Die Zusammenfassung der Aufdeckfrage steht nirgends im Fenster. Erwartet "
                    + $"war \"{vm.RevealSummary}\", gefunden wurde: "
                    + string.Join(" | ", texte.Where(t => !string.IsNullOrWhiteSpace(t))));

                var knopf = Nachfahren<Button>(inhalt)
                    .FirstOrDefault(b => b.Command == vm.RevealCommand);

                Assert.IsNotNull(knopf, "Der Knopf zum Einrichten fehlt im Fenster.");

                Assert.AreEqual(Visibility.Visible, knopf!.Visibility,
                    "Der Knopf zum Einrichten der Aufdeckfrage bleibt verborgen - dann ist der "
                    + "Editor fuer diesen Typ gar nicht erreichbar.");

                view.Close();
            });
        }

        private static IEnumerable<T> Nachfahren<T>(DependencyObject wurzel)
            where T : DependencyObject
        {
            var anzahl = VisualTreeHelper.GetChildrenCount(wurzel);

            for (var i = 0; i < anzahl; i++)
            {
                var kind = VisualTreeHelper.GetChild(wurzel, i);

                if (kind is T treffer)
                    yield return treffer;

                foreach (var tiefer in Nachfahren<T>(kind))
                    yield return tiefer;
            }
        }
    }
}
