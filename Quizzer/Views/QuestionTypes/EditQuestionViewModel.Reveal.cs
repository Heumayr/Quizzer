using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Einrichtung einer Aufdeckfrage - Bild, Flaechen, Unschaerfe.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06.</b> Der eigentliche Editor ist ein eigenes Fenster
    /// (<see cref="RevealEditorView"/>), weil das Zeichnen ueber dem Bild eine Flaeche braucht,
    /// die in die Feldmaske nicht passt.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        /// <summary>Ob diese Frage ueberhaupt eine Aufdeckfrage ist.</summary>
        public bool IsReveal => Question is RevealQuestion;

        /// <summary>Der Knopf erscheint nur bei einer Aufdeckfrage.</summary>
        public Visibility RevealVisibility => IsReveal ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Was eingerichtet ist - eine Zeile fuer die Maske.</summary>
        public string RevealSummary
        {
            get
            {
                if (Question is not RevealQuestion frage)
                    return string.Empty;

                if (string.IsNullOrWhiteSpace(frage.ImageFileName))
                    return "Noch kein Bild hinterlegt.";

                return frage.Mode switch
                {
                    RevealMode.Blur => $"Unschärfe von {frage.BlurStart:0} an, je Schritt schärfer.",
                    RevealMode.Pixelate => $"Raster von {frage.BlurStart:0} an, je Schritt feiner.",
                    _ => $"{RevealAreas.Parse(frage.AreasJson).Count} Fläche(n) über dem Bild.",
                };
            }
        }

        private RelayCommand? revealCommand;

        public ICommand RevealCommand => revealCommand ??= new RelayCommand(
            _ => RevealHandler(this),
            _ => IsReveal);

        /// <summary>
        /// Wie der Aufdeck-Editor geoeffnet wird. Gekapselt aus demselben Grund wie
        /// <c>IUserPrompt</c>: ein Fenster bliebe im Testlauf stehen.
        /// </summary>
        public static Action<EditQuestionViewModel> RevealHandler { get; set; } = Standard;

        /// <summary>Setzt auf das echte Fenster zurueck.</summary>
        public static void ResetRevealHandler() => RevealHandler = Standard;

        private static void Standard(EditQuestionViewModel vm)
        {
            if (vm.Question is not RevealQuestion frage)
                return;

            var fenster = new RevealEditorView();

            fenster.Zeige(frage);
            fenster.ShowDialog();

            if (!fenster.Uebernommen)
                return;

            if (!vm.SchritteAngleichen(fenster.GewuenschteSchritte))
                return;

            vm.MeldeRevealGeaendert();
        }

        /// <summary>
        /// Legt so viele Inhaltsschritte an, wie die Einstellung verlangt - und entfernt
        /// ueberzaehlige.
        /// <para>
        /// <b>Der Schritt ist die Einheit des Aufdeckens.</b> Ohne diese Angleichung haette eine
        /// Frage mit fuenf Flaechen zwei Schritte, und drei Flaechen fielen nie. Bei Unschaerfe
        /// und Verpixelung bestimmt der Schieberegler im Editor die Zahl.
        /// </para>
        /// </summary>
        internal bool SchritteAngleichen(int gebraucht)
        {
            if (Question is not RevealQuestion frage)
                return false;

            gebraucht = Math.Max(gebraucht, 0);

            // Erst das Getippte in die Frage holen. Sonst zaehlt die Rueckfrage unten Text nicht
            // mit, den der Spielleiter gerade erst eingegeben hat - und der Neuaufbau am Ende
            // wuerfe ihn weg.
            UebernimmZeilen();

            var vorhanden = frage.Steps
                .Where(s => !s.IsStart && !s.IsFinish)
                .OrderBy(s => s.SequenceNumber)
                .ToList();

            if (!DarfSchritteEntfernen(vorhanden, gebraucht))
                return false;

            for (var i = vorhanden.Count; i < gebraucht; i++)
            {
                frage.Steps.Add(new QuestionStepResource
                {
                    Id = Guid.NewGuid(),
                    QuestionBaseId = frage.Id,
                    SequenceNumber = (i + 1) * 10,
                    Designation = $"Aufdecken {i + 1}",
                    StepText = string.Empty,
                });
            }

            for (var i = vorhanden.Count - 1; i >= gebraucht; i--)
                frage.Steps.Remove(vorhanden[i]);

            // Die Typmaske haelt ihr eigenes Bild der Schritte, gelesen beim Oeffnen. Ohne
            // Neuaufbau schreibt SchreibZurueck die gerade entfernten Zeilen beim Speichern
            // wieder hin - und diese Rueckfrage waere wirkungslos. Derselbe Griff wie in
            // RemoveStepCommnadAsync, und aus demselben Grund.
            BaueZeileneditor();

            Revalidate();

            return true;
        }

        /// <summary>
        /// Fragt, bevor Aufdeckschritte wegfallen.
        /// <para>
        /// <b>Nutzerentscheidung vom 2026-09-06 nachts:</b> vorher fragen. Solange jede Fläche ein
        /// Schritt war, trat der Fall praktisch nie ein - seit Flächen sich zu einem Schritt
        /// gruppieren lassen, sinkt die Schrittzahl beim Gruppieren regelmäßig, und mit ihr
        /// verschwand bis hierher <b>wortlos</b> der Text der überzähligen Schritte.
        /// </para>
        /// <para>
        /// <b>Gefragt wird nur, wenn wirklich etwas wegfällt</b> - eine Rückfrage, die auch bei
        /// „nichts passiert" erscheint, wird weggeklickt, ohne gelesen zu werden.
        /// </para>
        /// </summary>
        private static bool DarfSchritteEntfernen(
            IReadOnlyList<QuestionStepResource> vorhanden, int gebraucht)
        {
            var fallenWeg = vorhanden.Count - gebraucht;

            if (fallenWeg <= 0)
                return true;

            var mitText = vorhanden
                .Skip(gebraucht)
                .Count(s => !string.IsNullOrWhiteSpace(s.StepText));

            var satz = $"{fallenWeg} Aufdeckschritt(e) fallen weg";

            if (mitText > 0)
                satz += $", davon {mitText} mit eigenem Text";

            return UserPrompt.Confirm(
                satz + "." + Environment.NewLine + Environment.NewLine + "Fortfahren?",
                "Aufdeckfrage");
        }

        private void MeldeRevealGeaendert()
        {
            OnPropertyChanged(nameof(RevealSummary));
            OnPropertyChanged(nameof(IsReveal));
            OnPropertyChanged(nameof(RevealVisibility));
        }
    }
}
