using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.Views.QuestionTypes;
using Quizzer.Views.QuestionTypes.Typed;

namespace Quizzer.UnitTests.Views.QuestionTypes
{
    /// <summary>
    /// Der Aufdeck-Editor und die Rückfrage nach den wegfallenden Schritten.
    /// <para>
    /// <b>Eigene Klasse seit 2026-09-07</b>, weil <c>ZeileneditorUnitTests.cs</c> in einer Nacht
    /// von 215 auf 697 Codezeilen gewachsen war. Der Aufbau kommt weiter aus
    /// <c>ZeileneditorUnitTests.Mit</c> — verdoppelt wird er nicht.
    /// </para>
    /// </summary>
    [TestClass]
    public class AufdeckDialogUnitTests
    {
        private static EditQuestionViewModel Mit(QuestionType typ, Action<QuestionBase>? weiter = null)
            => ZeileneditorUnitTests.Mit(typ, weiter);

        /// <summary>
        /// <b>Ein „Nein" lässt keinen halben Stand zurück.</b>
        /// <para>
        /// <b>Gemessen 2026-09-07:</b> der Aufdeck-Editor schrieb Bild, Betriebsart, Stärke und
        /// Flächen sofort in die Frage - die Rückfrage nach den wegfallenden Schritten kam erst
        /// danach. Wer dort „Nein" wählte, um seine Texte zu behalten, bekam trotzdem die neue
        /// Flächenaufteilung: drei Aufdeckschritte, fünf Inhaltsschritte, und am Quizabend
        /// zeigten die letzten beiden Bildschirme nichts Neues mehr.
        /// </para>
        /// </summary>
        [TestMethod]
        public void ANoLeavesTheRevealSettingsUntouched()
        {
            var vm = Mit(QuestionType.Reveal, f =>
            {
                ((RevealQuestion)f).AreasJson = "[]";

                for (var i = 0; i < 4; i++)
                {
                    f.Steps.Add(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        SequenceNumber = (i + 1) * 10,
                        Designation = $"Aufdecken {i + 1}",
                        StepText = "Ein Text, den niemand verlieren will",
                    });
                }
            });

            UserPrompt.Current = new RecordingUserPrompt(answer: false);

            try
            {
                var geschrieben = 0;

                Assert.IsFalse(
                    vm.SchritteAngleichen(2, () => geschrieben++),
                    "Trotz Ablehnung wurde angeglichen.");

                Assert.AreEqual(0, geschrieben,
                    "Die Einstellungen des Aufdeck-Editors wurden trotz Ablehnung geschrieben - "
                    + "die Frage traegt dann die neue Flaechenaufteilung UND die alte "
                    + "Schrittzahl.");

                Assert.AreEqual(4, vm.Question!.Steps.Count(s => !s.IsStart && !s.IsFinish),
                    "Es wurden Schritte entfernt, obwohl abgelehnt wurde.");
            }
            finally
            {
                UserPrompt.Reset();
            }
        }

        /// <summary>
        /// <b>Die Gegenrichtung.</b> Nach einem „Ja" wird geschrieben - und zwar bevor die Maske
        /// neu gelesen wird, sonst zeigte sie den alten Stand.
        /// </summary>
        [TestMethod]
        public void AYesWritesTheRevealSettingsBeforeTheMaskIsRebuilt()
        {
            var vm = Mit(QuestionType.Reveal, f =>
            {
                for (var i = 0; i < 4; i++)
                {
                    f.Steps.Add(new QuestionStepResource
                    {
                        Id = Guid.NewGuid(),
                        SequenceNumber = (i + 1) * 10,
                        Designation = $"Aufdecken {i + 1}",
                        StepText = "Text",
                    });
                }
            });

            UserPrompt.Current = new RecordingUserPrompt(answer: true);

            try
            {
                ZeileneditorViewModel? maskeBeimSchreiben = null;

                Assert.IsTrue(
                    vm.SchritteAngleichen(2, () => maskeBeimSchreiben = vm.Zeileneditor),
                    "Das Angleichen wurde abgelehnt.");

                Assert.IsNotNull(maskeBeimSchreiben, "Es wurde gar nicht geschrieben.");

                Assert.AreNotSame(maskeBeimSchreiben, vm.Zeileneditor,
                    "Geschrieben wurde erst NACH dem Neuaufbau der Maske - sie liest damit den "
                    + "alten Stand und zeigt die alte Flaechenzahl an.");
            }
            finally
            {
                UserPrompt.Reset();
            }
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
    }
}
