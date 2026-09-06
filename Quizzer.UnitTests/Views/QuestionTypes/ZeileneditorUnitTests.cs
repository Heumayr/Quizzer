using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
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

        /// <summary>
        /// <b>Eine frisch ausgefüllte Maske lässt sich speichern.</b>
        /// <para>
        /// <b>Gemessen am 2026-09-06, und es war eine Regression aus dem Maskenumbau:</b> die
        /// Prüfung lief gegen <c>Question.Steps</c>, und dort steht bis zum Speichern nichts - die
        /// Maske schreibt erst in <c>SaveAsync</c> zurück. Eine neue Multiple-Choice-Frage trug
        /// damit <c>TooFewSteps</c> und <c>ResultStepMissing</c>, obwohl vier Antworten dastanden.
        /// <c>CanSave</c> blieb falsch, und weil das Zurückschreiben nur <i>innerhalb</i> des
        /// gesperrten Speicherbefehls läuft, gab es keinen Weg heraus: <b>sie war überhaupt nicht
        /// speicherbar.</b>
        /// </para>
        /// <para>
        /// Geprüft wird für die beiden Typen, die Schritte verlangen - bei den anderen wäre die
        /// Zusicherung auch ohne Behebung grün und sagte nichts.
        /// </para>
        /// </summary>
        [TestMethod]
        public void AFreshlyFilledMaskCanBeSaved()
        {
            foreach (var typ in new[] { QuestionType.MultipleChoice, QuestionType.Properties })
            {
                var vm = Mit(typ, f =>
                {
                    f.DesignationShort = "P";
                    f.CategoryId = Guid.NewGuid();
                });

                var editor = vm.Zeileneditor!;

                Assert.IsFalse(vm.CanSave,
                    $"Bei {typ} ist die leere Maske speicherbar - dann sagt der Rest nichts.");

                editor.Zeilen[0].Text = "Canberra";
                editor.Zeilen[0].IstRichtig = true;
                editor.Zeilen[1].Text = "Sydney";

                Assert.IsTrue(vm.CanSave,
                    $"Bei {typ} laesst sich eine ausgefuellte Maske nicht speichern. Offen: "
                    + string.Join(" | ", vm.Issues.Where(i => i.IsError).Select(i => i.Code)));
            }
        }

        /// <summary>
        /// Und die Prüfung schreibt dabei <b>nichts</b> in die Frage - sie rechnet auf einem Klon.
        /// Liefe sie auf der Frage selbst, räumte sie bei jedem Tastendruck leere Zeilen weg, und
        /// eine Zeile verschwände unter dem Cursor, sobald man ihren Text löscht.
        /// </summary>
        [TestMethod]
        public void ValidatingNeverWritesIntoTheQuestion()
        {
            var vm = Mit(QuestionType.MultipleChoice, f => f.CategoryId = Guid.NewGuid());
            var editor = vm.Zeileneditor!;

            editor.Zeilen[0].Text = "Canberra";
            editor.Zeilen[0].IstRichtig = true;
            editor.Zeilen[1].Text = "Sydney";

            Assert.AreEqual(0, vm.Question!.Steps.Count,
                "Die Pruefung hat in die Frage geschrieben. Dann raeumt sie bei jedem "
                + "Tastendruck leere Zeilen weg, und eine Zeile verschwindet unter dem Cursor.");

            vm.UebernimmZeilen();

            Assert.AreEqual(2, vm.Question.Steps.Count,
                "Erst das Uebernehmen schreibt - und dann genau die gefuellten Zeilen.");
        }

        /// <summary>
        /// <b>Was der Aufdeck-Editor entfernt, bleibt auch nach dem Speichern entfernt.</b>
        /// <para>
        /// <b>Gefunden 2026-09-07 nachts.</b> <c>SchritteAngleichen</c> raeumt in
        /// <c>Question.Steps</c> auf - die Typmaske haelt aber ihr <b>eigenes</b> Bild der
        /// Schritte, gelesen beim Oeffnen. Ohne Neuaufbau schreibt <c>SchreibZurueck</c> die
        /// entfernten Zeilen beim Speichern wieder hin, und die Rueckfrage
        /// „N Aufdeckschritt(e) fallen weg … Fortfahren?" ist wirkungslos.
        /// </para>
        /// <para>
        /// <b>Der Zwilling wusste es schon:</b> <c>RemoveStepCommnadAsync</c> ruft
        /// <c>BaueZeileneditor()</c> mit genau dieser Begruendung im Kommentar. Nur die
        /// Aufdeck-Stelle tat es nicht.
        /// </para>
        /// <para>
        /// Am Abend heisst das: der Spielleiter klickt nach dem letzten Aufdeckschritt noch
        /// mehrfach auf ein laengst vollstaendiges Bild, und weil die Aufdeckfrage den
        /// Punkteabzug je Schritt traegt, bekommt der Spieler den Bruchteil der falschen
        /// Schrittzahl.
        /// </para>
        /// </summary>
        [TestMethod]
        public void RevealStepsRemovedByTheEditorStayRemovedOnSave()
        {
            var vm = Mit(QuestionType.Reveal, f =>
            {
                for (var i = 0; i < 5; i++)
                {
                    f.Steps.Add(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        SequenceNumber = (i + 1) * 10,
                        Designation = $"Aufdecken {i + 1}",
                        StepText = i == 2 ? "Der Zipfel unten rechts" : string.Empty,
                    });
                }
            });

            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            try
            {
                Assert.IsTrue(vm.SchritteAngleichen(2), "Das Angleichen wurde abgelehnt.");

                Assert.AreEqual(2, vm.Question!.Steps.Count(s => !s.IsStart && !s.IsFinish),
                    "Das Angleichen selbst hat gar nicht geraeumt - dann misst diese Probe "
                    + "etwas anderes als gemeint.");

                // Genau das tut das Speichern, bevor es schreibt.
                vm.UebernimmZeilen();

                Assert.AreEqual(2, vm.Question.Steps.Count(s => !s.IsStart && !s.IsFinish),
                    "Die entfernten Aufdeckschritte sind beim Speichern zurueckgekommen - die "
                    + "Rueckfrage davor ist damit wirkungslos.");
            }
            finally
            {
                UserPrompt.Reset();
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Ein Neuaufbau, der die Schritte <i>immer</i> aus der Frage
        /// nimmt, wuerde getippten Text der Maske verwerfen. Diese Zusicherung haelt fest, dass
        /// er nur dort greift, wo von aussen geraeumt wurde.
        /// </summary>
        [TestMethod]
        public void PlainEditingStillReachesTheQuestion()
        {
            var vm = Mit(QuestionType.MultipleChoice);

            vm.Zeileneditor!.Zeilen[0].Text = "Die erste Antwort";
            vm.Zeileneditor.Zeilen[1].Text = "Die zweite Antwort";
            vm.Zeileneditor.Zeilen[1].IstRichtig = true;

            vm.UebernimmZeilen();

            var texte = vm.Question!.Steps.Select(s => s.StepText).ToList();

            CollectionAssert.Contains(texte, "Die erste Antwort",
                "Getippter Text kommt nicht mehr in der Frage an. Gefunden: "
                + string.Join(" | ", texte));

            CollectionAssert.Contains(texte, "Die zweite Antwort",
                "Getippter Text kommt nicht mehr in der Frage an.");
        }

        /// <summary>
        /// <b>Aufdeckschritte fallen nicht wortlos weg.</b>
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06 nachts:</b> vorher fragen. Solange jede Fläche ein
        /// Schritt war, trat der Fall praktisch nie ein - seit Flächen sich gruppieren lassen,
        /// sinkt die Schrittzahl regelmäßig, und mit ihr verschwand der Text der überzähligen
        /// Schritte.
        /// </para>
        /// <para>
        /// <b>Beide Richtungen:</b> abgelehnt heißt, dass wirklich nichts geschieht - und wo
        /// nichts wegfällt, wird auch nicht gefragt. Ohne die zweite Hälfte wäre „immer fragen"
        /// grün, und die Rückfrage erschiene bei jedem Übernehmen.
        /// </para>
        /// </summary>
        [TestMethod]
        public void RemovingRevealStepsAsksFirst()
        {
            var vm = Mit(QuestionType.Reveal, f =>
            {
                for (var i = 0; i < 3; i++)
                {
                    f.Steps.Add(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        SequenceNumber = (i + 1) * 10,
                        Designation = $"Aufdecken {i + 1}",
                        StepText = i == 2 ? "Der Zipfel unten rechts" : string.Empty,
                    });
                }
            });

            var abgelehnt = new RecordingUserPrompt(answer: false);

            UserPrompt.Current = abgelehnt;

            try
            {
                Assert.IsFalse(vm.SchritteAngleichen(1),
                    "Trotz Ablehnung wurde angeglichen.");

                Assert.AreEqual(3, vm.Question!.Steps.Count,
                    "Es wurden Schritte entfernt, obwohl die Rueckfrage abgelehnt wurde.");

                StringAssert.Contains(abgelehnt.Confirms[^1].Message, "eigenem Text",
                    "Die Rueckfrage sagt nicht, dass ein Schritt mit Text darunter ist: "
                    + abgelehnt.Confirms[^1].Message);

                // Wo nichts wegfaellt, wird nicht gefragt.
                var vorher = abgelehnt.Confirms.Count;

                Assert.IsTrue(vm.SchritteAngleichen(5),
                    "Beim Anlegen zusaetzlicher Schritte wurde gefragt und abgelehnt.");

                Assert.AreEqual(vorher, abgelehnt.Confirms.Count,
                    "Es wurde gefragt, obwohl nichts wegfaellt - eine solche Rueckfrage wird "
                    + "weggeklickt, ohne gelesen zu werden.");
            }
            finally
            {
                UserPrompt.Reset();
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
        /// <b>Zwei richtige Antworten bleiben zwei</b> - weder beim Öffnen noch beim Klicken
        /// räumt die Maske etwas ab.
        /// <para>
        /// <b>Nutzerrichtigstellung vom 2026-09-06:</b> „damit war gemeint das im editor die
        /// steps schöner gleich in der maske selbst den result setzen kann". Gemeint war das
        /// Setzen <i>in derselben Maske</i>, nicht ein Klick zum Wechseln. Ein automatisches
        /// Abräumen wäre eine Verhaltensänderung, die niemand angefordert hat - und eine
        /// gefährliche: der Klick auf die zweite richtige Antwort nähme die erste stillschweigend
        /// weg.
        /// </para>
        /// </summary>
        [TestMethod]
        public void MarkingASecondAnswerNeverClearsTheFirst()
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

            // Und beim Klicken ebenso wenig: eine dritte dazu laesst die beiden stehen.
            mc.Zeilen[2].Text = "Graz";
            mc.Zeilen[2].IstRichtig = true;

            Assert.AreEqual(3, mc.Zeilen.Count(z => z.IstRichtig),
                "Ein Klick auf eine weitere Antwort hat die vorherigen abgeraeumt.");

            Assert.AreEqual(3, vm.Question.BuzzerMaxAllowedKeySelect,
                "Drei Loesungen ergeben nicht drei waehlbare Tasten.");
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
