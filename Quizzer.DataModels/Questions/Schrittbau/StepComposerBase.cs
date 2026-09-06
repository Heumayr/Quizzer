using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Das Aufteilen der Schritte, das alle Typen teilen: was in die Zeilen geht, was der
    /// Abschluss ist - und was nur mitfährt.
    /// <para>
    /// <b>Mitgeführt wird großzügig, nicht sparsam.</b> Alles, wofür es in der Maske kein Feld
    /// gibt, bleibt am Objekt hängen; das kostet nichts und ist der einzige Schutz davor, dass
    /// <c>SaveWithStepsAsync</c> es beim nächsten Speichern wegräumt.
    /// </para>
    /// </summary>
    public abstract class StepComposerBase : IStepComposer
    {
        public abstract QuestionType Typ { get; }

        public virtual string ZeilenTitel => string.Empty;

        public virtual string AbschlussTitel => string.Empty;

        /// <summary>Ob dieser Typ überhaupt ein Abschlussfeld zeigt.</summary>
        protected virtual bool HatAbschluss => !string.IsNullOrEmpty(AbschlussTitel);

        /// <summary>Ob dieser Typ eine Zeilenliste zeigt.</summary>
        protected virtual bool HatZeilen => !string.IsNullOrEmpty(ZeilenTitel);

        /// <summary>
        /// Wie viele leere Zeilen mindestens dastehen sollen. <b>Das ist das „Gerüst statt leeres
        /// Blatt"</b> - der Typ weiß aus seinem Profil, wie viele Schritte er braucht, und heute
        /// benutzt er das Wissen ausschließlich zum Meckern.
        /// </summary>
        protected virtual int Grundzeilen => 0;

        public virtual Schrittbild Lies(QuestionBase frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            var mitgefuehrt = new List<QuestionStepResource>();
            var zeilen = new List<StepZeile>();

            QuestionStepResource? abschluss = null;

            foreach (var schritt in frage.Steps.OrderBy(s => s.SequenceNumber))
            {
                // Nur der erste Abschluss ist der Abschluss. Weitere sind Bestand, den niemand
                // angelegt haben sollte - und der trotzdem nicht verschwinden darf.
                if (schritt.IsFinish && HatAbschluss && abschluss == null)
                    abschluss = schritt;

                // Ein selbst angelegter Startschritt hat in keiner Typmaske ein Feld - er faehrt
                // mit. Bei Multiple Choice loescht er sonst still eine Antwort vom Telefon.
                else if (schritt.IsStart || schritt.IsFinish || !HatZeilen)
                    mitgefuehrt.Add(schritt);

                else
                    zeilen.Add(new StepZeile(schritt));
            }

            while (HatZeilen && zeilen.Count < Grundzeilen)
                zeilen.Add(NeueZeile());

            VergibTasten(frage, zeilen);

            return new Schrittbild
            {
                Zeilen = zeilen,
                Abschluss = HatAbschluss ? new StepZeile(abschluss ?? NeuerSchritt()) : null,
                Mitgefuehrt = mitgefuehrt,
            };
        }

        public virtual void Schreib(QuestionBase frage, Schrittbild bild)
        {
            ArgumentNullException.ThrowIfNull(frage);
            ArgumentNullException.ThrowIfNull(bild);

            bild.SchreibNach(frage);
        }

        /// <summary>Eine leere Zeile für das Gerüst.</summary>
        public StepZeile NeueZeile() => new(NeuerSchritt());

        /// <summary>
        /// Ein frischer Schritt. <b>Ohne Platzhaltertext</b> - das Telefon zeigt bei Multiple
        /// Choice die <c>Designation</c> auf der Taste, dort stünde sonst „Antwort 1".
        /// </summary>
        private static QuestionStepResource NeuerSchritt()
            => new() { Id = Guid.NewGuid() };

        /// <summary>
        /// Schreibt auf jede Zeile die Taste, die im Spiel darauf liegt - nur zur Anzeige.
        /// <para>
        /// <b>Dieselbe Vergabe wie <c>CalculateOrderdSteps</c></b>, sonst steht in der Maske ein
        /// anderer Buchstabe als auf dem Telefon. Die Spalte „Taste" im heutigen Raster ist immer
        /// leer, weil das Ordnen auf der Editorstrecke nie läuft.
        /// </para>
        /// </summary>
        protected static void VergibTasten(QuestionBase frage, List<StepZeile> zeilen)
        {
            var taste = Helpers.Helper.GetNextViewKey(string.Empty, frage.QuestionViewKeyType);

            foreach (var zeile in zeilen)
            {
                zeile.Taste = taste;
                taste = Helpers.Helper.GetNextViewKey(taste, frage.QuestionViewKeyType);
            }
        }
    }
}
