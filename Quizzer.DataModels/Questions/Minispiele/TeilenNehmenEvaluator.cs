namespace Quizzer.DataModels.Questions.Minispiele
{
    /// <summary>Die beiden bei Teilen oder Nehmen und ihre geheime Wahl; <c>null</c> = keine Abgabe.</summary>
    /// <param name="A">Der Ausloeser.</param>
    /// <param name="NimmtA">Ob er nimmt (<c>false</c>: teilt).</param>
    /// <param name="B">Der gewaehlte Partner.</param>
    /// <param name="NimmtB">Ob er nimmt.</param>
    public sealed record TeilenNehmenPaar(Guid A, bool? NimmtA, Guid B, bool? NimmtB);

    /// <summary>Was Teilen oder Nehmen ergeben hat.</summary>
    /// <param name="Ausgang">Fuer Beamer und Spielleiter, etwa „Beide teilen – je die Hälfte".</param>
    /// <param name="Vorschlaege">Nur Plus - wer nichts bekommt, bekommt keinen Vorschlag.</param>
    public sealed record TeilenNehmenErgebnis(string Ausgang, IReadOnlyDictionary<Guid, Punktevorschlag> Vorschlaege);

    /// <summary>
    /// Teilen oder Nehmen (M35 C2), Topf = Feldpunkte: beide teilen - je die Haelfte (kaufmaennisch
    /// gerundet); einer nimmt - er alles; beide nehmen - niemand etwas. <b>Nur Plus</b>, keine Kappung.
    /// Fehlt eine Wahl (getrenntes Telefon), schlaegt nichts vor - der Spielleiter wertet von Hand.
    /// </summary>
    public static class TeilenNehmenEvaluator
    {
        public static TeilenNehmenErgebnis Werte(TeilenNehmenPaar paar, int topf, Func<Guid, string> name)
        {
            ArgumentNullException.ThrowIfNull(paar);
            ArgumentNullException.ThrowIfNull(name);

            var keine = new Dictionary<Guid, Punktevorschlag>();

            if (paar.NimmtA is not { } nimmtA || paar.NimmtB is not { } nimmtB)
            {
                var fehlt = string.Join(" und ", new[] { (paar.A, paar.NimmtA), (paar.B, paar.NimmtB) }
                    .Where(s => s.Item2 == null).Select(s => name(s.Item1)));
                return new($"Keine Abgabe von {fehlt} – von Hand werten", keine);
            }

            if (nimmtA && nimmtB)
                return new("Beide nehmen – niemand bekommt etwas", keine);

            if (!nimmtA && !nimmtB)
            {
                var haelfte = Punkteanteil.Von(topf, 50);
                return new("Beide teilen – je die Hälfte", new Dictionary<Guid, Punktevorschlag>
                {
                    [paar.A] = new(haelfte, $"Teilen oder Nehmen: mit {name(paar.B)} geteilt"),
                    [paar.B] = new(haelfte, $"Teilen oder Nehmen: mit {name(paar.A)} geteilt"),
                });
            }

            var (nehmer, teiler) = nimmtA ? (paar.A, paar.B) : (paar.B, paar.A);

            return new($"{name(nehmer)} nimmt alles", new Dictionary<Guid, Punktevorschlag>
            {
                [nehmer] = new(topf, $"Teilen oder Nehmen: genommen, {name(teiler)} hat geteilt"),
            });
        }

        /// <summary>Die Wahl im Klartext.</summary>
        public static string Describe(bool? nimmt) => nimmt switch
        {
            true => "nimmt",
            false => "teilt",
            null => "keine Abgabe",
        };
    }
}
