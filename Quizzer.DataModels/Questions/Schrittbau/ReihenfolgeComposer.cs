using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Reihenfolge: die Schritte sind <b>Elemente</b>, und die Reihenfolge der Zeilen in der
    /// Maske ist die Lösung.
    /// <para>
    /// <b>Die Lösung wird gestempelt, nicht eingegeben.</b> Ein Zahlenfeld je Zeile müsste der
    /// Spielleiter lückenlos und doppelfrei halten; die Zeilenfolge ist es von selbst. Gelesen
    /// wird darum auch in der Reihenfolge der Lösung, nicht der <c>SequenceNumber</c> - die
    /// schreibt das Spiel beim Mischen neu.
    /// </para>
    /// </summary>
    public class ReihenfolgeComposer : StepComposerBase
    {
        /// <summary>So viele Zeilen stehen von Anfang an da.</summary>
        public const int Vorgabezeilen = 4;

        public override QuestionType Typ => QuestionType.Ordering;

        public override string ZeilenTitel => "Elemente in der richtigen Reihenfolge";

        public override string AbschlussTitel => "Was am Ende zusätzlich steht (optional)";

        protected override int Grundzeilen => Vorgabezeilen;

        public override Schrittbild Lies(QuestionBase frage)
        {
            var bild = base.Lies(frage);

            // Nur umsortieren, wenn die Loesung vollstaendig ist - eine umgewandelte Frage traegt
            // ueberall 0, dann gilt die bisherige Schrittfolge als Vorschlag.
            if (ReihenfolgeEvaluator.Loesung(frage) != null)
            {
                var sortiert = bild.Zeilen
                    .OrderBy(z => z.IstLeer ? int.MaxValue : z.Schritt.SolutionNumber)
                    .ToList();

                bild.Zeilen.Clear();
                bild.Zeilen.AddRange(sortiert);

                VergibTasten(frage, bild.Zeilen);
            }

            return bild;
        }

        public override void Schreib(QuestionBase frage, Schrittbild bild)
        {
            base.Schreib(frage, bild);

            var platz = 0;

            foreach (var zeile in bild.Zeilen.Where(z => !z.IstLeer))
                zeile.Schritt.SolutionNumber = ++platz;

            // Mitgefuehrte Schritte stehen in keiner Loesung.
            foreach (var schritt in bild.Mitgefuehrt)
                schritt.SolutionNumber = 0;

            if (bild.Abschluss != null)
                bild.Abschluss.Schritt.SolutionNumber = 0;

            if (bild.Start != null)
                bild.Start.Schritt.SolutionNumber = 0;

            // Gewaehlt werden muessen alle - die Zahl folgt aus den Zeilen.
            frage.BuzzerMaxAllowedKeySelect = Math.Max(platz, 1);
        }
    }

    /// <summary>
    /// Zuordnung: jede Zeile ist ein Paar - links der feste Begriff (<c>PartnerText</c>), rechts
    /// das Gegenstück. Die Zeilenfolge ist die Folge der Begriffe auf dem Beamer; gestempelt und
    /// gelesen wird genau wie bei der Reihenfolge.
    /// </summary>
    public sealed class ZuordnungComposer : ReihenfolgeComposer
    {
        public override QuestionType Typ => QuestionType.Matching;

        public override string ZeilenTitel => "Paare: Begriff links ↔ Gegenstück rechts";
    }
}
