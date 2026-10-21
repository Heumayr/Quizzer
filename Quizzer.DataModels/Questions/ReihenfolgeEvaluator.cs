using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.Base;

namespace Quizzer.DataModels.Questions
{
    /// <summary>Ein Platz in der Abgabe eines Spielers.</summary>
    /// <param name="Nummer">Der Platz, ab 1.</param>
    /// <param name="Taste">Die Taste, die der Spieler an diesen Platz gesetzt hat; leer, wenn er ihn nicht belegt hat.</param>
    /// <param name="Text">Was hinter der Taste steht.</param>
    /// <param name="Richtig">Ob das Element hier hingehört.</param>
    public sealed record ReihenfolgePlatz(int Nummer, string Taste, string Text, bool Richtig);

    /// <summary>Die Auswertung einer Abgabe.</summary>
    /// <param name="Plaetze">Jeder Platz der Lösung, in ihrer Reihenfolge.</param>
    /// <param name="RichtigePlaetze">Wie viele Elemente am richtigen Platz stehen.</param>
    /// <param name="Punkte">Was die Abgabe bringt - der Anteil der richtigen Plätze.</param>
    public sealed record ReihenfolgeErgebnis(
        IReadOnlyList<ReihenfolgePlatz> Plaetze,
        int RichtigePlaetze,
        int Punkte)
    {
        /// <summary>Wie viele Plätze es gibt.</summary>
        public int Gesamt => Plaetze.Count;

        /// <summary>Ob alles stimmt.</summary>
        public bool AllesRichtig => Gesamt > 0 && RichtigePlaetze == Gesamt;
    }

    /// <summary>
    /// Wertet eine Reihenfolgefrage aus (M12, 2026-10-21).
    /// <para>
    /// <b>Gezählt wird je Platz:</b> ein Element bringt seinen Anteil, wenn es genau dort steht,
    /// wo es hingehört. Das ist die Regel, die sich am Tisch in einem Satz erklären lässt - eine
    /// Wertung nach „fast richtig" (benachbarte Paare, längste richtige Folge) wäre gerechter,
    /// aber niemand könnte sie nachrechnen, während die Punkte auf dem Beamer stehen.
    /// </para>
    /// <para>
    /// Rein rechnend, wie <see cref="AppreciateEvaluator"/>: keine Oberfläche, keine Datenbank.
    /// Das Ergebnis ist ein Vorschlag; der Spielleiter kann jede Bewertung ändern.
    /// </para>
    /// </summary>
    public static class ReihenfolgeEvaluator
    {
        /// <summary>
        /// Die Elemente in der richtigen Reihenfolge - oder <c>null</c>, wenn die Lösung nicht
        /// eindeutig hinterlegt ist (jedes Element genau einen Platz von 1 bis n).
        /// </summary>
        public static IReadOnlyList<QuestionStepResource>? Loesung(QuestionBase frage)
        {
            ArgumentNullException.ThrowIfNull(frage);

            var elemente = (frage.Steps ?? [])
                .Where(s => !s.IsStart && !s.IsFinish && !s.IsQuestionOnly)
                .OrderBy(s => s.SolutionNumber)
                .ToList();

            if (elemente.Count == 0)
                return null;

            for (var i = 0; i < elemente.Count; i++)
            {
                if (elemente[i].SolutionNumber != i + 1)
                    return null;
            }

            return elemente;
        }

        /// <summary>
        /// Wertet eine Abgabe aus.
        /// </summary>
        /// <param name="frage">Die Frage - ihre Schritte tragen die Tasten aus <c>CalculateOrderdSteps</c>.</param>
        /// <param name="tasten">Die Tasten in der Reihenfolge, in der der Spieler sie angetippt hat.</param>
        /// <param name="punkte">Was die Frage bei vollständig richtiger Reihenfolge bringt.</param>
        /// <returns><c>null</c>, wenn die Frage keine eindeutige Lösung trägt.</returns>
        public static ReihenfolgeErgebnis? Werte(
            QuestionBase frage, IReadOnlyList<string>? tasten, int punkte)
        {
            var loesung = Loesung(frage);

            if (loesung == null)
                return null;

            tasten ??= [];

            var plaetze = new List<ReihenfolgePlatz>(loesung.Count);
            var richtige = 0;

            for (var i = 0; i < loesung.Count; i++)
            {
                var taste = i < tasten.Count ? tasten[i] ?? string.Empty : string.Empty;

                var gewaehlt = taste.Length == 0
                    ? null
                    : frage.Steps.FirstOrDefault(s => s.QuestionViewKey == taste);

                var richtig = gewaehlt != null && gewaehlt.Id == loesung[i].Id;

                if (richtig)
                    richtige++;

                plaetze.Add(new ReihenfolgePlatz(i + 1, taste, TextVon(gewaehlt), richtig));
            }

            return new ReihenfolgeErgebnis(plaetze, richtige, Anteil(punkte, richtige, loesung.Count));
        }

        /// <summary>
        /// Der Anteil der Punkte für <paramref name="richtige"/> von <paramref name="gesamt"/>
        /// Plätzen - kaufmännisch gerundet, damit 100 Punkte bei drei Elementen 33, 67 und 100 geben.
        /// </summary>
        public static int Anteil(int punkte, int richtige, int gesamt)
        {
            if (gesamt <= 0 || richtige <= 0)
                return 0;

            if (richtige >= gesamt)
                return punkte;

            return (int)Math.Round(punkte * (double)richtige / gesamt, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Die Auflösung als Text - für das Abschlussfeld der Maske.
        /// <para>
        /// <b>Ohne Tasten:</b> das Spiel mischt bei jedem Spielen neu, ein Buchstabe in der
        /// Auflösung stimmte nur zufällig.
        /// </para>
        /// </summary>
        public static string Aufloesungstext(IEnumerable<QuestionStepResource> elementeInReihenfolge)
        {
            ArgumentNullException.ThrowIfNull(elementeInReihenfolge);

            return string.Join(Environment.NewLine,
                elementeInReihenfolge.Select((s, i) => $"{i + 1}. {TextVon(s)}"));
        }

        private static string TextVon(QuestionStepResource? schritt)
        {
            if (schritt == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(schritt.StepText))
                return schritt.StepText;

            return schritt.Designation ?? string.Empty;
        }
    }
}
