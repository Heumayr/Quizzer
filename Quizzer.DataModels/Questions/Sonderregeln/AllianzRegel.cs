using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.DataModels.Questions.Sonderregeln
{
    /// <summary>
    /// Sonderregel Allianz (M35 C3b): der Ausloeser waehlt einen Partner, nur die beiden antworten. Sind beide richtig,
    /// bekommen beide die vollen Punkte; sonst wird jedes Plus der beiden 0, ein Minus bleibt, wie es ist.
    /// </summary>
    public static class AllianzRegel
    {
        public const string BeideRichtigGrund = "Allianz: beide richtig";

        public const string KeinPlusGrund = "Allianz: nicht beide richtig – kein Plus";

        /// <summary>Die Vorschlaege fuer das Paar; ein Minus bleibt der Bewertung selbst (kein Eintrag).</summary>
        public static IReadOnlyDictionary<Guid, Punktevorschlag> Werte(Guid ausloeser, Guid partner, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            ArgumentNullException.ThrowIfNull(eingaenge);

            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();
            var beide = Richtig(ausloeser, eingaenge) && Richtig(partner, eingaenge);

            foreach (var spieler in new[] { ausloeser, partner })
            {
                if (!eingaenge.TryGetValue(spieler, out var eingang))
                    continue;

                if (beide)
                    vorschlaege[spieler] = new(eingang.Grundwert, BeideRichtigGrund);
                else if (eingang.Grundwert > 0)
                    vorschlaege[spieler] = new(0, KeinPlusGrund);
            }

            return vorschlaege;
        }

        private static bool Richtig(Guid spieler, IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => eingaenge.GetValueOrDefault(spieler)?.Bewertung == RatingKind.Right;
    }
}
