using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.Views.QuestionTypes;
using Quizzer.Views.QuestionTypes.Typed;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Alles zu einem Schritt steht in <b>einer</b> Maske.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06 nachts:</b> „generell wäre schön wenn man alles in einer
    /// maske steuert und nicht für details pro step in die andere maske muss".
    /// </para>
    /// <para>
    /// <b>Gemessen wird über Bindungen, nicht über Größen.</b> Ein Bedienelement in einem
    /// zugeklappten Bereich hat <c>ActualWidth == 0</c> - eine Prüfung über Maße sähe es nie und
    /// bliebe grün, während es fehlt.
    /// </para>
    /// </summary>
    [TestClass]
    public class AllesInEinerMaskeUnitTests
    {
        /// <summary>
        /// Feld am Schritt → Bindungspfad oder Befehlsname, unter dem es in der Maske erreichbar
        /// ist.
        /// </summary>
        private static readonly Dictionary<string, string[]> Landkarte = new()
        {
            [nameof(QuestionStepResource.StepText)] = ["Text"],
            [nameof(QuestionStepResource.IsResult)] = ["IstRichtig"],
            [nameof(QuestionStepResource.ResourceFileName)] = ["MediaCommand", "RemoveMediaCommand"],
            [nameof(QuestionStepResource.IsStart)] = ["Start.Text"],
            [nameof(ModelBase.Designation)] = ["Kurzform"],
        };

        /// <summary>
        /// Was <b>nicht</b> in die Maske gehört - jedes mit seinem Grund im Klartext. Ein neues
        /// Feld am Schritt, das in keiner der beiden Listen steht, macht die Zusicherung rot.
        /// </summary>
        private static readonly Dictionary<string, string> Ausnahmen = new()
        {
            [nameof(QuestionStepResource.SequenceNumber)] =
                "wird von Schrittbild.SchreibNach und CalculateOrderdSteps gestempelt - eine "
                + "getippte Zahl erreicht das Spiel nie",
            [nameof(QuestionStepResource.IsFinish)] =
                "das Abschlussfeld setzt ihn; ein Haken je Zeile erzeugt nur MultipleFinishSteps",
            [nameof(QuestionStepResource.ResourceTyp)] =
                "folgt der Dateiendung; frei gewaehlt erzeugt er nur ResourceWithoutType",
            [nameof(QuestionStepResource.QuestionViewKey)] =
                "vergibt VergibTasten bzw. CalculateOrderdSteps",
            [nameof(QuestionStepResource.IsQuestionOnly)] =
                "NotMapped, nur auf einem vom Modell erfundenen Schritt gesetzt",
            [nameof(QuestionStepResource.QuestionBaseId)] = "Persistenz",
            [nameof(ModelBase.Id)] = "Persistenz",
            [nameof(ModelBase.RowVersion)] = "Persistenz",
        };

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
        /// Sammelt alle Bindungspfade und Befehlsnamen aus dem aufgebauten Fenster - mit
        /// <b>aufgeklappter</b> erster Zeile, sonst sind die Einzelheiten nicht im Baum.
        /// </summary>
        private static HashSet<string> Erreichbar(bool zeileAufklappen = true)
        {
            var gefunden = new HashSet<string>(StringComparer.Ordinal);

            UiTestHost.Run(() =>
            {
                var view = new EditQuestionsView();
                var vm = (EditQuestionViewModel)view.DataContext;
                var frage = Factory.CreateNewQuestion(QuestionType.MultipleChoice);

                frage.Id = Guid.NewGuid();
                frage.Designation = "Probe";
                frage.CategoryId = Guid.NewGuid();
                vm.Question = frage;

                var editor = vm.Zeileneditor!;

                if (zeileAufklappen)
                    editor.ToggleDetailsCommand.Execute(editor.Zeilen[0]);

                var inhalt = (FrameworkElement)view.Content;

                inhalt.Measure(new Size(1400, 1400));
                inhalt.Arrange(new Rect(0, 0, 1400, 1400));
                inhalt.UpdateLayout();

                foreach (var knoten in Alle(inhalt))
                {
                    if (knoten is not FrameworkElement element)
                        continue;

                    foreach (var eigenschaft in new[]
                    {
                        TextBox.TextProperty, TextBlock.TextProperty,
                        ToggleButton.IsCheckedProperty, ButtonBase.CommandProperty,
                        UIElement.VisibilityProperty, UIElement.IsEnabledProperty,
                    })
                    {
                        if (BindingOperations.GetBinding(element, eigenschaft)
                            is { Path.Path: { Length: > 0 } pfad })
                        {
                            gefunden.Add(pfad);
                            gefunden.Add(pfad.Split('.')[^1]);
                        }
                    }
                }

                view.Close();
            });

            return gefunden;
        }

        /// <summary>
        /// <b>Jedes Feld am Schritt ist entweder in der Maske erreichbar oder mit Grund
        /// ausgenommen.</b> Ein neues Feld am Modell macht sie rot, weil es in keiner Liste steht.
        /// </summary>
        [TestMethod]
        public void EveryStepFieldIsReachableInTheOneMask()
        {
            var erreichbar = Erreichbar();

            var felder = typeof(QuestionStepResource)
                .GetProperties()
                .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true)
                .Select(p => p.Name)
                .ToList();

            var unbekannt = felder
                .Where(f => !Landkarte.ContainsKey(f) && !Ausnahmen.ContainsKey(f))
                .ToList();

            Assert.AreEqual(0, unbekannt.Count,
                "Diese Felder am Schritt stehen weder in der Landkarte noch in den Ausnahmen - "
                + "entweder gehoeren sie in die Maske, oder der Grund dagegen gehoert "
                + "aufgeschrieben: " + string.Join(", ", unbekannt));

            var fehlend = Landkarte
                .Where(e => !e.Value.Any(erreichbar.Contains))
                .Select(e => $"{e.Key} (erwartet: {string.Join(" oder ", e.Value)})")
                .ToList();

            Assert.AreEqual(0, fehlend.Count,
                "Diese Felder sind in der Maske nicht erreichbar - fuer sie muesste man wieder in "
                + "ein zweites Fenster: " + string.Join(", ", fehlend));
        }

        /// <summary>
        /// <b>Die Messung findet nicht einfach alles.</b>
        /// <para>
        /// Ohne diese Zusicherung wäre die obige auch dann grün, wenn die Sammlung jeden Namen
        /// zurückgäbe, den man ihr vorhält.
        /// </para>
        /// <para>
        /// <b>Gemessen und richtiggestellt:</b> die naheliegende Gegenprobe - „zugeklappt darf die
        /// Kurzform nicht gefunden werden" - ist <i>falsch</i>. Das <c>ItemsControl</c> baut alle
        /// Zeilen auf; der zugeklappte Bereich ist nur <c>Collapsed</c>, seine Bindungen stehen
        /// trotzdem im Baum. Das Aufklappen ändert die Erreichbarkeit gar nicht, nur die
        /// Sichtbarkeit - und genau deshalb misst diese Zusicherung über Bindungen und nicht über
        /// Größen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheProbeDoesNotSimplyFindEverything()
        {
            var erreichbar = Erreichbar();

            Assert.IsFalse(erreichbar.Contains("GibtEsNichtUndDarfNieGefundenWerden"),
                "Die Sammlung liefert Namen, die es gar nicht gibt - dann sagt sie nichts.");

            Assert.IsTrue(erreichbar.Count is > 5 and < 300,
                $"Die Sammlung hat {erreichbar.Count} Namen gefunden. Zu wenige heisst, sie misst "
                + "gar nicht; zu viele heisst, sie greift wahllos.");
        }

        /// <summary>
        /// Kein Bedienelement führt aus der Maske heraus. Diese Zusicherung wird rot, sobald
        /// jemand den Weg in ein zweites Fenster wieder einbaut.
        /// </summary>
        [TestMethod]
        public void NothingLeadsOutOfTheMask()
        {
            var erreichbar = Erreichbar();

            foreach (var verboten in new[]
                     { "AdvancedCommand", "OpenStepCommand", "AddStepCommnad", "Erweitert" })
            {
                Assert.IsFalse(erreichbar.Contains(verboten),
                    $"\"{verboten}\" ist wieder an die Maske gebunden - damit fuehrt ein "
                    + "Bedienelement in ein zweites Fenster.");
            }
        }
    }
}
