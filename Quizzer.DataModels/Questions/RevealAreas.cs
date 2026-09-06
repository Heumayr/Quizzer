using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System.Text.Json;

namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Eine verdeckende Fläche über dem Bild einer Aufdeckfrage - relativ zum Bild, nicht in
    /// Bildpunkten.
    /// <para>
    /// <b>Relativ, weil dasselbe Bild in drei Größen erscheint:</b> auf dem Beamer, im
    /// Spielleiterfenster und in der Vorschau. Absolute Punkte säßen überall woanders.
    /// </para>
    /// </summary>
    /// <param name="X">Linker Rand, 0 bis 1.</param>
    /// <param name="Y">Oberer Rand, 0 bis 1.</param>
    /// <param name="W">Breite, 0 bis 1.</param>
    /// <param name="H">Höhe, 0 bis 1.</param>
    public sealed record RevealArea(double X, double Y, double W, double H);

    /// <summary>
    /// Liest und schreibt die Flächen einer Aufdeckfrage - und rechnet aus, was auf einem
    /// bestimmten Schritt zu sehen ist.
    /// <para>
    /// <b>Die Rechnung steht hier und nicht in der Ansicht.</b> Beamer, Spielleiterfenster und
    /// Vorschau zeigen dasselbe Bild; drei Stellen mit derselben Rechnung laufen auseinander.
    /// </para>
    /// </summary>
    public static class RevealAreas
    {
        private static readonly JsonSerializerOptions Options = new();

        /// <summary>
        /// Liest die Flächen. Eine unlesbare Angabe ergibt eine leere Liste statt einer Ausnahme -
        /// eine kaputte Frage soll den Spielabend nicht beenden.
        /// </summary>
        public static List<RevealArea> Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                return JsonSerializer.Deserialize<List<RevealArea>>(json, Options) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        /// <summary>Schreibt die Flächen zurück.</summary>
        public static string ToJson(IEnumerable<RevealArea> flaechen)
            => JsonSerializer.Serialize(flaechen.ToList(), Options);

        /// <summary>
        /// Welche Flächen auf diesem Schritt noch verdecken.
        /// <para>
        /// Bis einschließlich des Fragebildschirms ist alles verdeckt; danach fällt je
        /// Inhaltsschritt eine Fläche weg, und auf dem Abschluss ist das Bild frei.
        /// </para>
        /// </summary>
        /// <param name="flaechen">Alle Flächen der Frage.</param>
        /// <param name="aufgedeckt">Wie viele bereits weggefallen sind.</param>
        public static IEnumerable<RevealArea> NochVerdeckt(
            IReadOnlyList<RevealArea> flaechen, int aufgedeckt)
            => flaechen.Skip(Math.Max(aufgedeckt, 0));

        /// <summary>
        /// Wie unscharf das Bild auf diesem Schritt ist.
        /// <para>
        /// Läuft von <paramref name="start"/> auf null - der letzte Inhaltsschritt zeigt das Bild
        /// scharf. Ohne Inhaltsschritte gibt es nichts zu schärfen, dann ist es sofort scharf:
        /// ein dauerhaft unscharfes Bild wäre eine Frage, die niemand beantworten kann.
        /// </para>
        /// </summary>
        /// <param name="start">Die Unschärfe im ersten Schritt.</param>
        /// <param name="schritte">Wie viele Inhaltsschritte es gibt.</param>
        /// <param name="aufgedeckt">Wie viele davon schon gezeigt wurden.</param>
        public static double Unschaerfe(double start, int schritte, int aufgedeckt)
        {
            if (schritte <= 0 || start <= 0)
                return 0;

            var offen = Math.Clamp(schritte - aufgedeckt, 0, schritte);

            return start * offen / schritte;
        }

        /// <summary>Die Flächen einer Frage - oder eine leere Liste, wenn es keine Aufdeckfrage ist.</summary>
        public static List<RevealArea> Of(Models.QuestionBase? frage)
            => frage is RevealQuestion aufdeck ? Parse(aufdeck.AreasJson) : [];

        /// <summary>Ob diese Frage überhaupt eine Aufdeckfrage ist.</summary>
        public static bool IstAufdeckfrage(Models.QuestionBase? frage)
            => frage?.Typ == QuestionType.Reveal;
    }
}
