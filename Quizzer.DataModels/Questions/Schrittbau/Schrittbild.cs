using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Was die Maske von den Schritten einer Frage sieht - und was sie ausdrücklich nicht sieht.
    /// <para>
    /// <b><see cref="Mitgefuehrt"/> ist der wichtigste Teil.</b> Dort liegt alles, was der
    /// Übersetzer nicht modelliert: ein selbst angelegter Startschritt, ein zweiter
    /// Abschlussschritt, was auch immer später dazukommt. Fiele es hier heraus,
    /// <b>löschte der nächste Speichervorgang es lautlos</b> - <c>SaveWithStepsAsync</c> räumt
    /// nach Id-Menge auf.
    /// </para>
    /// </summary>
    public sealed class Schrittbild
    {
        /// <summary>Die Zeilen, die die Maske zeigt - Antworten, Hinweise, Flächen.</summary>
        public List<StepZeile> Zeilen { get; init; } = [];

        /// <summary>
        /// Was am Ende steht. <c>null</c>, wenn der Typ keinen Abschluss kennt; sonst immer
        /// vorhanden, auch leer.
        /// </summary>
        public StepZeile? Abschluss { get; init; }

        /// <summary>
        /// Der Startbildschirm - was vor der Frage steht.
        /// <para>
        /// <b>Ein eigenes Fach, keine Zeile.</b> Eine Zeile bekommt eine Antworttaste, ein
        /// Startschritt nie; eine Zeile hat ein Häkchen „richtig", das auf einem Startschritt eine
        /// Multiple-Choice-Frage unspielbar macht; Zeilen gibt es viele, Startschritte höchstens
        /// einen. Als Fach ist dieser ganze Fehlerraum konstruktiv leer.
        /// </para>
        /// <para>
        /// <b>Nur der erste.</b> Jeder weitere <c>IsStart</c>-Schritt bleibt in
        /// <see cref="Mitgefuehrt"/> - er ist Bestand, den niemand angelegt haben sollte, und der
        /// trotzdem nicht verschwinden darf.
        /// </para>
        /// </summary>
        public StepZeile? Start { get; init; }

        /// <summary>
        /// Schritte, die die Maske nicht anzeigt und trotzdem behält. Siehe Klassenkommentar.
        /// </summary>
        public List<QuestionStepResource> Mitgefuehrt { get; init; } = [];

        /// <summary>
        /// Schreibt alles zurück in <see cref="QuestionBase.Steps"/> - mitgeführte Schritte
        /// zuerst, dann die Zeilen in ihrer Reihenfolge, zuletzt der Abschluss.
        /// <para>
        /// <b>Leere Zeilen fallen weg</b>, und ein leeres Startfach ebenso. Eine Maske, die vier
        /// Antwortzeilen anbietet, von denen zwei gefüllt sind, darf keine zwei leeren
        /// Bildschirme ins Spiel schreiben - und ein immer geschriebener leerer Startschritt
        /// machte aus einer unberührten Maske eine Frage mit einem Schritt.
        /// </para>
        /// <para>
        /// <b>Die Nummern werden hier gestempelt</b>, und zwar über die <i>ganze</i> Liste, nicht
        /// nur über die Zeilen. Nur über die Zeilen gestempelt bekäme ein Abschluss bei einem Typ
        /// ohne Zeilenliste die 10 - dieselbe Nummer wie ein mitgeführter Schritt, und die
        /// Reihenfolge hinge am Zufall. Sonst stehen die Nummern in keiner Maske:
        /// <c>CalculateOrderdSteps</c> überschreibt sie beim Ordnen ohnehin, was hier zählt ist
        /// allein die <i>Reihenfolge</i>.
        /// </para>
        /// </summary>
        public void SchreibNach(QuestionBase frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            var gesammelt = new List<QuestionStepResource>();

            // Der Startschritt zuerst - vor allem anderen. Die Rundlauf-Zusicherung vergleicht
            // nach SequenceNumber sortiert, und ein Startschritt traegt im Bestand die kleinste
            // Nummer; hinten geschrieben kippt sie bei jedem Fragetyp.
            if (Start is { IstLeer: false } start)
            {
                start.Schritt.IsStart = true;

                gesammelt.Add(start.Schritt);
            }

            gesammelt.AddRange(Mitgefuehrt);

            gesammelt.AddRange(Zeilen.Where(z => !z.IstLeer).Select(z => z.Schritt));

            if (Abschluss is { IstLeer: false } abschluss)
            {
                abschluss.Schritt.IsFinish = true;

                gesammelt.Add(abschluss.Schritt);
            }

            var nummer = 0;

            foreach (var schritt in gesammelt)
            {
                nummer += 10;

                schritt.SequenceNumber = nummer;
                schritt.QuestionBaseId = frage.Id;
            }

            frage.Steps.Clear();
            frage.Steps.AddRange(gesammelt);
        }
    }
}
