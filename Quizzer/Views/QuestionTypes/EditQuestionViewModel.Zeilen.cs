using Quizzer.Base;
using Quizzer.DataModels;
using Quizzer.DataModels.Questions.Schrittbau;
using Quizzer.Views.QuestionTypes.Typed;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Die Brücke zwischen der Schale und der Maske des Fragetyps.
    /// <para>
    /// <b>Nutzerentscheidung vom 2026-09-06:</b> jeder Fragetyp bekommt eine eigene Maske. Das
    /// Schrittmodell ist für alle fünf dasselbe, obwohl jeder etwas anderes damit meint - eine
    /// Nummer und ein Häkchen „Ist Lösung" bedeuten bei der Schätzfrage nichts.
    /// </para>
    /// <para>
    /// <b>Die Schale kennt keinen Fragetyp.</b> Sie holt sich hier ein
    /// <see cref="ZeileneditorViewModel"/> und zeigt es in einem <c>ContentControl</c>; welches
    /// es ist, entscheidet <see cref="Zeileneditoren"/> anhand des Typs.
    /// </para>
    /// </summary>
    public partial class EditQuestionViewModel
    {
        private ZeileneditorViewModel? zeileneditor;

        /// <summary>
        /// Die Maske des Fragetyps - oder <c>null</c>, solange keine Frage geladen ist.
        /// </summary>
        public ZeileneditorViewModel? Zeileneditor
        {
            get => zeileneditor;
            private set
            {
                zeileneditor = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Baut die Maske zur geladenen Frage neu auf. Wird von <c>OnModelChanged</c> gerufen -
        /// eine Frage ohne Maske wäre ein leerer Editor.
        /// </summary>
        private void BaueZeileneditor()
        {
            if (Question == null)
            {
                Zeileneditor = null;
                return;
            }

            var composer = StepComposers.For(Question.Typ);

            var editor = Zeileneditoren.Fuer(composer, Question, Revalidate);

            editor.MediumWaehlen = HaengeMediumAn;

            Zeileneditor = editor;
        }

        /// <summary>
        /// Hängt eine Mediendatei an eine Zeile - <b>ohne Umweg über den Schritt-Dialog</b>.
        /// <para>
        /// <b>Nutzerwunsch vom 2026-09-06:</b> „gut wäre ein button für media". Derselbe Weg wie
        /// dort: die Datei landet über <c>FileHelper</c> im Ressourcenordner und bekommt dort
        /// ihren Namen.
        /// </para>
        /// <para>
        /// <b>Der Dateityp wird hinterher gefangen, nicht vorher geprüft</b> - der Kommentar hier
        /// behauptete bis zum 2026-09-06 das Gegenteil. <c>DetectResourceType</c> <i>wirft</i> bei
        /// unbekannter Endung; der Filter bietet deshalb kein „Alle Dateien" an, und was trotzdem
        /// durchkommt, wird gefangen und gesagt statt als Ausnahmefenster gezeigt.
        /// </para>
        /// </summary>
        private static bool HaengeMediumAn(StepZeile zeile)
        {
            var quelle = FilePicker.AskForExistingFile(
                "Medium wählen",
                "Bilder|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp"
                + "|Ton|*.mp3;*.wav;*.ogg;*.flac"
                + "|Video|*.mp4;*.avi;*.mov;*.wmv;*.mkv"
                + "|Dokumente|*.pdf;*.doc;*.docx;*.txt");

            if (quelle == null)
                return false;

            try
            {
                var (dateiname, typ) = FileHelper.HandleSelectedResourceFile(
                    quelle, Settings.ResourceRootFolder);

                zeile.Schritt.ResourceFileName = dateiname;
                zeile.Schritt.ResourceTyp = typ;

                return true;
            }
            catch (Exception ex)
            {
                UserPrompt.Inform(
                    "Die Datei ließ sich nicht übernehmen:" + Environment.NewLine + ex.Message,
                    "Medium");

                return false;
            }
        }

        /// <summary>
        /// Schreibt zurück, was in der Maske steht - <b>vor</b> jedem Speichern.
        /// <para>
        /// <b>Sie schreibt nicht laufend mit.</b> Der Übersetzer räumt leere Zeilen weg und
        /// stempelt die Reihenfolge; liefe das bei jedem Tastendruck, verschwände eine Zeile
        /// unter dem Cursor, sobald man ihren Text löscht.
        /// </para>
        /// </summary>
        internal void UebernimmZeilen()
        {
            if (Question == null || Zeileneditor == null)
                return;

            Zeileneditor.SchreibZurueck(Question);
        }
    }
}
