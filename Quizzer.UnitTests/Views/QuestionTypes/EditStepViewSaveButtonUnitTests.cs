using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.QuestionTypes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Der Knopf „Speichern" im Schritt-Dialog.
    /// <para>
    /// <b>Gemessen am 2026-09-06:</b> aus dem Frageneditor heraus läuft der Dialog mit
    /// <c>PersistDirectly = false</c>, und dann kehrt <c>VMSaveAsync</c> wirkungslos zurück. Der
    /// Knopf sah aus wie eine Sicherung und war keine - wer ihn drückte und danach das Fenster
    /// schloss, hatte nichts gespeichert.
    /// </para>
    /// <para>
    /// <b>Geprüft wird der aufgebaute Baum, nicht das ViewModel.</b> Eine Sichtbarkeit, die im
    /// ViewModel richtig steht und in der XAML nicht gebunden ist, fällt sonst nirgends auf.
    /// </para>
    /// </summary>
    [TestClass]
    public class EditStepViewSaveButtonUnitTests
    {
        private static Visibility SichtbarkeitDesSpeichernknopfs(bool schreibtSelbst)
        {
            var gefunden = Visibility.Visible;

            UiTestHost.Run(() =>
            {
                var fenster = new EditStepView();
                var vm = (EditStepViewModel)fenster.DataContext;

                vm.PersistDirectly = schreibtSelbst;
                vm.Step = new QuestionStepResource { Id = Guid.NewGuid(), SequenceNumber = 10 };

                var inhalt = (FrameworkElement)fenster.Content;

                inhalt.Measure(new Size(900, 500));
                inhalt.Arrange(new Rect(0, 0, 900, 500));
                inhalt.UpdateLayout();

                var knopf = Nachfahren<Button>(inhalt)
                    .First(b => ReferenceEquals(b.Command, vm.SaveCommand));

                gefunden = knopf.Visibility;

                fenster.Close();
            });

            return gefunden;
        }

        /// <summary>Ein Knopf, der nichts tut, steht nicht da.</summary>
        [TestMethod]
        public void TheSaveButtonIsOnlyThereWhenItWrites()
        {
            Assert.AreEqual(Visibility.Collapsed, SichtbarkeitDesSpeichernknopfs(false),
                "Aus dem Frageneditor heraus schreibt \"Speichern\" nichts - dann darf der Knopf "
                + "auch nicht dastehen.");

            Assert.AreEqual(Visibility.Visible, SichtbarkeitDesSpeichernknopfs(true),
                "Wo der Dialog wirklich selbst schreibt, gehoert der Knopf hin. Sonst waere er "
                + "nur pauschal ausgeblendet und die Zusicherung sagte nichts.");
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
