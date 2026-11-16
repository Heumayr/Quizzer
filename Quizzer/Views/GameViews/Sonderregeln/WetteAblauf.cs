using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>
    /// Wette (M35 C3b): die Karte des Wettenden haengt an der Bewertung des Gewaehlten - <see cref="WetteRegel"/>.
    /// Solange der Gewaehlte nicht richtig ist, steht die Wette als verloren auf der Karte.
    /// </summary>
    internal sealed class WetteAblauf : SonderregelAblauf<Guid>
    {
        public WetteAblauf()
            : base(CellSpecialRule.Bet)
        {
        }

        private int Einsatz => WetteRegel.Einsatz(Grundlage.Feldpunkte, Grundlage.Anteil);

        private Wette WetteAuf(Guid spieler) => WetteRegel.Auf(spieler, Grundlage.NameVon(spieler), Einsatz, Grundlage.Kontostaende);

        protected override IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var (spieler, auf) in Abgaben)
            {
                if (!eingaenge.TryGetValue(spieler, out var eigener))
                    continue;

                var gewaehlt = eingaenge.GetValueOrDefault(auf)?.Bewertung ?? RatingKind.None;
                vorschlaege[spieler] = WetteRegel.Werte(WetteAuf(auf), gewaehlt, eigener);
            }

            return vorschlaege;
        }

        protected override string AbgabeText(Guid spieler)
            => Von(spieler) is { } auf
                ? $"Wette auf {Grundlage.NameVon(auf)}: Einsatz {Einsatz}, Gewinn {WetteAuf(auf).Gewinn}"
                : "Keine Wette";
    }
}
