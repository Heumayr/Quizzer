using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.Views.QuestionTypes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Die Maske je Fragetyp im Frageneditor.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> „überlege dir für alle fragen eine bessere
    /// mechanik damit es leichter ist sie anzulegen" - gewählt wurde die eigene Maske je Typ.
    /// Bauplan: `frageneditor-umbau.md` im Harness.
    /// </para>
    /// </summary>
    [TestClass]
    public class ZeileneditorUnitTests
    {
        private static EditQuestionViewModel Mit(QuestionType typ, Action<QuestionBase>? weiter = null)
        {
            var vm = new EditQuestionViewModel();
            var frage = Factory.CreateNewQuestion(typ);

            frage.Id = Guid.NewGuid();
            frage.Designation = "Probe";

            weiter?.Invoke(frage);

            vm.Question = frage;

            return vm;
        }

        /// <summary>
        /// <b>Kein Knopf schneidet sein Zeichen weg.</b>
        /// <para>
        /// <b>Nutzermeldung vom 2026-09-06:</b> die Knöpfe in der Antwortzeile standen als leere
        /// abgerundete Rechtecke da. Nicht die Farbe fehlte - der implizite Knopfstil setzt
        /// <c>Padding="12,6"</c>, und bei einer festen Breite von 26 blieben davon <b>null</b>
        /// Punkte für den Inhalt.
        /// </para>
        /// <para>
        /// Gemessen wird die ausgelegte Breite gegen Polsterung und Rahmen - nicht die
        /// Schriftfarbe, die war nie das Problem.
        /// </para>
        /// </summary>
        [TestMethod]
        public void NoButtonClipsItsOwnGlyph()
        {
            var eng = new List<string>();

            UiTestHost.Run(() =>
            {
                var view = new EditQuestionsView();
                var vm = (EditQuestionViewModel)view.DataContext;
                var frage = Factory.CreateNewQuestion(QuestionType.MultipleChoice);

                frage.Id = Guid.NewGuid();
                frage.Designation = "Probe";
                frage.CategoryId = Guid.NewGuid();
                vm.Question = frage;

                var inhalt = (FrameworkElement)view.Content;

                inhalt.Measure(new Size(1400, 900));
                inhalt.Arrange(new Rect(0, 0, 1400, 900));
                inhalt.UpdateLayout();

                foreach (var knopf in Nachfahren<Button>(inhalt))
                {
                    if (knopf.Content is not string text || text.Length == 0
                        || knopf.ActualWidth <= 0)
                        continue;

                    var innen = knopf.ActualWidth - knopf.Padding.Left - knopf.Padding.Right
                                - knopf.BorderThickness.Left - knopf.BorderThickness.Right;

                    if (innen < 8)
                        eng.Add($"[{text}] ausgelegt {knopf.ActualWidth:0}, innen {innen:0}");
                }

                view.Close();
            });

            Assert.AreEqual(0, eng.Count,
                "Diese Knoepfe haben keinen Platz fuer ihren Inhalt:" + Environment.NewLine
                + string.Join(Environment.NewLine, eng));
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

        /// <summary>Jeder Fragetyp bekommt eine Maske - keiner fällt durch.</summary>
        [TestMethod]
        public void EveryQuestionTypeGetsItsOwnMask()
        {
            foreach (var typ in Enum.GetValues<QuestionType>())
            {
                var vm = Mit(typ);

                Assert.IsNotNull(vm.Zeileneditor,
                    $"Fuer {typ} baut der Editor keine Maske - der Bereich bliebe leer.");
            }
        }

        /// <summary>
        /// Die Schätzfrage bekommt <b>keine</b> Zeilenliste und kein Häkchen „richtig". Bisher
        /// bekam sie ein volles Schritt-Raster samt einer Spalte, die für sie nie etwas bedeuten
        /// kann.
        /// </summary>
        [TestMethod]
        public void TheEstimateQuestionHasNoStepList()
        {
            var schaetzung = Mit(QuestionType.Appreciate).Zeileneditor!;

            Assert.AreEqual(System.Windows.Visibility.Collapsed, schaetzung.ZeilenVisibility,
                "Die Schaetzfrage zeigt eine Zeilenliste, obwohl sie keine Schritte kennt.");

            Assert.IsFalse(schaetzung.ZeigtRichtig,
                "Die Schaetzfrage zeigt ein Haekchen \"richtig\", das nie etwas bedeuten kann.");

            Assert.AreEqual(System.Windows.Visibility.Visible, schaetzung.AbschlussVisibility,
                "Auch die Schaetzfrage braucht ein Feld fuer das, was am Ende steht.");
        }

        /// <summary>
        /// Multiple Choice startet mit einem Gerüst aus vier Antwortzeilen und lässt sich nicht
        /// sortieren - das Spiel mischt ohnehin.
        /// </summary>
        [TestMethod]
        public void MultipleChoiceStartsWithFourRowsAndNoSorting()
        {
            var mc = Mit(QuestionType.MultipleChoice).Zeileneditor!;

            Assert.AreEqual(4, mc.Zeilen.Count,
                "Multiple Choice startet nicht mit vier Antwortzeilen - dann ist es wieder ein "
                + "leeres Blatt.");

            Assert.IsFalse(mc.DarfSortieren,
                "Multiple Choice bietet Sortieren an und verspricht damit eine Reihenfolge, die "
                + "das Spiel beim Mischen wegwirft.");

            Assert.IsTrue(mc.ZeigtRichtig);
        }

        /// <summary>
        /// Die Standardfrage bekommt <b>erstmals</b> ein Feld für die richtige Antwort - bisher
        /// hatte der Typ keines, und am Beamer stand am Ende nichts.
        /// </summary>
        [TestMethod]
        public void TheDefaultQuestionFinallyHasAnAnswerField()
        {
            var standard = Mit(QuestionType.Default).Zeileneditor!;

            Assert.AreEqual(System.Windows.Visibility.Visible, standard.AbschlussVisibility);
            Assert.AreEqual("Die richtige Antwort", standard.AbschlussTitel);
            Assert.IsTrue(standard.DarfSortieren,
                "Bei der Standardfrage ist die Reihenfolge der Hinweise echt - sie muss sich "
                + "verschieben lassen.");
        }

        /// <summary>
        /// Die Eigenschaftsfrage schreibt neben jede Zeile, was danach noch zu holen ist - und
        /// zwar <b>aus derselben Rechnung wie das Spiel</b>.
        /// </summary>
        [TestMethod]
        public void ThePropertyQuestionShowsTheSameScoreTheGameWillGive()
        {
            var vm = Mit(QuestionType.Properties, f => f.Points = 100);
            var leiter = vm.Zeileneditor!;

            leiter.Zeilen[0].Text = "Erster Hinweis";
            leiter.Zeilen[1].Text = "Zweiter Hinweis";
            leiter.Zeilen[2].Text = "Dritter Hinweis";

            for (var i = 0; i < 3; i++)
            {
                var erwartet = Punkteabzug.Verbleibend(100, 3, i + 1);

                StringAssert.Contains(leiter.Zeilen[i].Zusatz, erwartet.ToString(),
                    $"Neben Zeile {i + 1} steht nicht, was das Spiel danach noch gibt "
                    + $"({erwartet}), sondern: \"{leiter.Zeilen[i].Zusatz}\".");
            }
        }

        /// <summary>
        /// <b>Die Antworttasten folgen der Liste, nicht dem Zustand beim Öffnen.</b>
        /// <para>
        /// <b>Nutzermeldung vom 2026-09-06:</b> „a b c d setzt sich selbst wenn man zeilen
        /// ändert". Vergeben wurde bisher nur beim Lesen - eine angehängte Zeile trug gar keinen
        /// Buchstaben, und nach einer Entfernung stand in der Maske ein anderer als auf dem
        /// Telefon.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheAnswerKeysFollowTheRows()
        {
            var vm = Mit(QuestionType.MultipleChoice);
            var mc = vm.Zeileneditor!;

            Assert.IsTrue(mc.Zeilen.All(z => z.Taste.Length == 0),
                "Leere Geruestzeilen tragen eine Taste - die verspricht einen Buchstaben, den "
                + "das Telefon nie zeigt, weil die Zeile gar nicht geschrieben wird.");

            mc.Zeilen[0].Text = "Sydney";
            mc.Zeilen[1].Text = "Canberra";
            mc.Zeilen[2].Text = "Melbourne";

            CollectionAssert.AreEqual(new[] { "A", "B", "C", "" },
                mc.Zeilen.Select(z => z.Taste).ToArray(),
                "Die Tasten stimmen nach dem Tippen nicht: "
                + string.Join(",", mc.Zeilen.Select(z => $"'{z.Taste}'")));

            // Eine Zeile anhaengen und fuellen - sie muss die naechste Taste bekommen.
            mc.AddRowCommand.Execute(null);
            mc.Zeilen[^1].Text = "Perth";

            Assert.AreEqual("D", mc.Zeilen[^1].Taste,
                "Die angehaengte Zeile hat keine Taste bekommen.");

            // Die erste entfernen - alles rutscht nach.
            mc.RemoveRowCommand.Execute(mc.Zeilen[0]);

            CollectionAssert.AreEqual(new[] { "A", "B", "", "C" },
                mc.Zeilen.Select(z => z.Taste).ToArray(),
                "Nach dem Entfernen sind die Tasten nicht nachgerueckt: "
                + string.Join(",", mc.Zeilen.Select(z => $"'{z.Taste}'")));
        }

        /// <summary>
        /// Und die Probe darauf, dass die Maske dieselbe Vergabe zeigt wie das Spiel - nach
        /// einer Änderung, nicht nur beim Öffnen.
        /// </summary>
        [TestMethod]
        public void TheKeysStillMatchTheGameAfterEditing()
        {
            var vm = Mit(QuestionType.MultipleChoice, f => f.UseRandomSequenceOnNoneFinishSteps = false);
            var mc = vm.Zeileneditor!;

            mc.Zeilen[0].Text = "Sydney";
            mc.Zeilen[1].Text = "Canberra";
            mc.Zeilen[2].Text = "Melbourne";

            mc.RemoveRowCommand.Execute(mc.Zeilen[0]);
            mc.AddRowCommand.Execute(null);
            mc.Zeilen[^1].Text = "Perth";

            vm.UebernimmZeilen();
            vm.Question!.CalculateOrderdSteps();

            var imSpiel = vm.Question.OrderedSteps
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly)
                .ToDictionary(s => s.StepText, s => s.QuestionViewKey);

            foreach (var zeile in mc.Zeilen.Where(z => !z.IstLeer))
            {
                Assert.AreEqual(imSpiel[zeile.Text], zeile.Taste,
                    $"Fuer \"{zeile.Text}\" steht in der Maske \"{zeile.Taste}\", im Spiel "
                    + $"aber \"{imSpiel[zeile.Text]}\".");
            }
        }

        /// <summary>
        /// <b>Die richtige Antwort zu wechseln kostet einen Klick, nicht zwei.</b>
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „auch das setzten der richtigen antwort muss ein
        /// klick sein". Das <i>Setzen</i> war schon einer; das <i>Wechseln</i> kostete zwei, und
        /// der Zwischenzustand war nicht bloß umständlich, sondern falsch - mit zwei Häkchen
        /// verlangt die Frage am Telefon zwei Tastendrücke.
        /// </para>
        /// <para>
        /// <b>Gemessen wird der Verlauf, nicht der Endzustand.</b> „Am Ende ist genau eine
        /// markiert" wäre auch dann wahr, wenn das Abräumen sich selbst wieder aufriefe - genau
        /// die Fehlerfamilie, die in diesem Projekt schon einen Stapelüberlauf erzeugt hat.
        /// </para>
        /// </summary>
        [TestMethod]
        public void SwitchingTheRightAnswerCostsOneClick()
        {
            var vm = Mit(QuestionType.MultipleChoice);
            var mc = (Quizzer.Views.QuestionTypes.Typed.AntwortlisteViewModel)vm.Zeileneditor!;

            mc.Zeilen[0].Text = "Sydney";
            mc.Zeilen[1].Text = "Canberra";

            mc.Zeilen[0].IstRichtig = true;

            // Ab hier den Verlauf mitschreiben.
            var meldungen = 0;

            foreach (var zeile in mc.Zeilen)
                zeile.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(StepZeile.IstRichtig))
                        meldungen++;
                };

            mc.Zeilen[1].IstRichtig = true;

            Assert.IsFalse(mc.Zeilen[0].IstRichtig,
                "Die vorherige Antwort ist noch markiert - das Wechseln kostet weiter zwei Klicks.");

            Assert.IsTrue(mc.Zeilen[1].IstRichtig);

            Assert.AreEqual(2, meldungen,
                $"Es sind {meldungen} Meldungen gelaufen statt zwei (die gesetzte und die "
                + "geraeumte). Mehr heisst, das Abraeumen ruft sich selbst wieder auf.");

            Assert.AreEqual(1, vm.Question!.BuzzerMaxAllowedKeySelect,
                "Die Frage verlangt am Telefon mehr als einen Tastendruck.");
        }

        /// <summary>
        /// Und der Rückweg: mehrere richtige Antworten bleiben möglich, <b>ohne einen Modus</b>.
        /// Ohne diese Zusicherung wäre „einfach immer alles abräumen" grün - und die vom Bestand
        /// ausdrücklich geschützte Mehrfachlösung wäre still abgeschafft.
        /// </summary>
        [TestMethod]
        public void MultipleRightAnswersStayReachable()
        {
            var vm = Mit(QuestionType.MultipleChoice);
            var mc = (Quizzer.Views.QuestionTypes.Typed.AntwortlisteViewModel)vm.Zeileneditor!;

            mc.Zeilen[0].Text = "Wien";
            mc.Zeilen[1].Text = "Linz";

            mc.Zeilen[0].IstRichtig = true;
            mc.Zeilen[1].IstRichtig = true;

            Assert.AreEqual(System.Windows.Visibility.Visible, mc.RueckwegVisibility,
                "Nach dem Abraeumen wird kein Rueckweg angeboten - dann sind mehrere richtige "
                + "Antworten von dieser Maske aus unerreichbar.");

            StringAssert.Contains(mc.Rueckwegtext, "Wien",
                "Der Rueckweg sagt nicht, welche Antwort er zurueckholt.");

            mc.RestoreSecondSolutionCommand.Execute(null);

            Assert.IsTrue(mc.Zeilen[0].IstRichtig && mc.Zeilen[1].IstRichtig,
                "Der Rueckweg hat die zweite Antwort nicht wieder markiert.");

            Assert.AreEqual(2, vm.Question!.BuzzerMaxAllowedKeySelect,
                "Zwei Loesungen ergeben nicht zwei waehlbare Tasten.");

            Assert.AreEqual(System.Windows.Visibility.Collapsed, mc.RueckwegVisibility,
                "Das Angebot bleibt stehen, obwohl es nicht mehr gilt.");
        }

        /// <summary>
        /// <b>Das Abräumen darf beim Öffnen nie laufen.</b> Eine Bestandsfrage mit zwei Häkchen
        /// verlöre sonst allein durchs Hinsehen eine Markierung - und
        /// <c>BuzzerMaxAllowedKeySelect</c> fiele von 2 auf 1.
        /// </summary>
        [TestMethod]
        public void OpeningAQuestionNeverClearsAnExistingSecondSolution()
        {
            var vm = Mit(QuestionType.MultipleChoice, f =>
            {
                f.BuzzerMaxAllowedKeySelect = 2;

                foreach (var text in new[] { "Wien", "Linz" })
                {
                    f.Steps.Add(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        SequenceNumber = f.Steps.Count * 10 + 10,
                        Designation = text,
                        StepText = text,
                        IsResult = true,
                    });
                }
            });

            var mc = vm.Zeileneditor!;

            Assert.AreEqual(2, mc.Zeilen.Count(z => z.IstRichtig),
                "Das blosse Oeffnen hat eine Markierung abgeraeumt - stiller Datenverlust.");

            Assert.AreEqual(2, vm.Question!.BuzzerMaxAllowedKeySelect,
                "Die Zahl der waehlbaren Tasten ist beim Oeffnen gefallen.");
        }

        /// <summary>
        /// Was in der Maske steht, landet beim Speichern in der Frage - und leere Gerüstzeilen
        /// nicht.
        /// </summary>
        [TestMethod]
        public void WhatTheMaskHoldsReachesTheQuestion()
        {
            var vm = Mit(QuestionType.MultipleChoice);
            var mc = vm.Zeileneditor!;

            mc.Zeilen[0].Text = "Canberra";
            mc.Zeilen[0].IstRichtig = true;
            mc.Zeilen[1].Text = "Sydney";

            vm.UebernimmZeilen();

            Assert.AreEqual(2, vm.Question!.Steps.Count,
                "Es kommen nicht genau die gefuellten Zeilen in der Frage an.");

            Assert.AreEqual(1, vm.Question.BuzzerMaxAllowedKeySelect,
                "Die Zahl der waehlbaren Antworten folgt nicht der Zahl der Haken.");
        }

        /// <summary>
        /// <b>Die abgeleitete Tastenzahl macht das Speichern erst möglich.</b>
        /// <para>
        /// <c>KeySelectCountMismatch</c> ist ein <i>Fehler</i>. Eine Bestandsfrage mit einem
        /// Häkchen und gespeicherter 3 trüge ihn, bis der Wert stimmt - der aber erst beim
        /// Speichern gesetzt würde, das die Prüfung gerade verhindert. Deshalb leitet die Maske
        /// sofort ab und sagt, dass sie es getan hat.
        /// </para>
        /// </summary>
        [TestMethod]
        public void TheDerivedKeyCountUnblocksSavingAndSaysSo()
        {
            var vm = Mit(QuestionType.MultipleChoice, f =>
            {
                f.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(), SequenceNumber = 10,
                    Designation = "Canberra", StepText = "Canberra", IsResult = true,
                });

                f.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(), SequenceNumber = 20,
                    Designation = "Sydney", StepText = "Sydney",
                });

                f.BuzzerMaxAllowedKeySelect = 3;
            });

            Assert.AreEqual(1, vm.Question!.BuzzerMaxAllowedKeySelect,
                "Die Zahl der waehlbaren Antworten wurde beim Oeffnen nicht abgeleitet - die "
                + "Frage bliebe wegen KeySelectCountMismatch unspeicherbar.");

            Assert.IsFalse(
                QuestionValidator.Validate(vm.Question)
                    .Any(i => i.Code == QuestionValidator.KeySelectCountMismatch),
                "Die Pruefung meldet weiterhin den Widerspruch.");

            var antwortliste = (Quizzer.Views.QuestionTypes.Typed.AntwortlisteViewModel)
                vm.Zeileneditor!;

            StringAssert.Contains(antwortliste.Korrekturhinweis, "3",
                "Die Maske sagt nicht, dass sie einen gespeicherten Wert ueberschreibt. Sachlich "
                + "behebt sie einen Defekt - angewiesen hat es trotzdem niemand.");
        }

        /// <summary>
        /// <b>Ein Schritt, der von außerhalb der Maske dazukommt, überlebt.</b>
        /// <para>
        /// Der Schritt-Dialog („Erweitert …") legt weiterhin direkt in <c>Question.Steps</c> an.
        /// Schriebe die Maske ihr beim Öffnen gelesenes Bild darüber, wäre er weg - vier
        /// bestehende Zusicherungen haben genau das gemeldet, als diese Übernahme fehlte.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AStepAddedOutsideTheMaskSurvives()
        {
            var vm = Mit(QuestionType.Default);

            vm.Question!.Steps.Add(new QuestionStepResource
            {
                Id = Guid.NewGuid(),
                QuestionBaseId = vm.Question.Id,
                SequenceNumber = 10,
                Designation = "Ueber den Schritt-Dialog angelegt",
                StepText = "Ueber den Schritt-Dialog angelegt",
            });

            vm.UebernimmZeilen();

            Assert.AreEqual(1, vm.Question.Steps.Count,
                "Der ausserhalb der Maske angelegte Schritt ist verschwunden.");
        }

        /// <summary>
        /// Und die Gegenrichtung: eine in der Maske entfernte Zeile kommt <b>nicht</b> zurück.
        /// Ohne diese zweite Zusicherung wäre „nimm einfach alles aus <c>Steps</c> mit" grün -
        /// und Löschen unmöglich.
        /// </summary>
        [TestMethod]
        public void ARemovedRowDoesNotComeBack()
        {
            var vm = Mit(QuestionType.Default, f =>
            {
                f.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    SequenceNumber = 10,
                    Designation = "Hinweis",
                    StepText = "Hinweis",
                });
            });

            var leiter = vm.Zeileneditor!;

            Assert.AreEqual(1, leiter.Zeilen.Count);

            leiter.RemoveRowCommand.Execute(leiter.Zeilen[0]);

            vm.UebernimmZeilen();

            Assert.AreEqual(0, vm.Question!.Steps.Count,
                "Eine entfernte Zeile ist wieder aufgetaucht - Loeschen waere dann unmoeglich.");
        }
    }
}
