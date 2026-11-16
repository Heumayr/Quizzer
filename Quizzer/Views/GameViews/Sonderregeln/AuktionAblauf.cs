using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>
    /// Auktion (M35 C3b): wer am hoechsten bietet, antwortet allein und zahlt - <see cref="AuktionRegel"/>. Bietet
    /// niemand mehr als 0, spielt die Frage normal.
    /// </summary>
    internal sealed class AuktionAblauf : SonderregelAblauf<Gebot>
    {
        private Zuschlag? zuschlag;

        public AuktionAblauf()
            : base(CellSpecialRule.Auction)
        {
        }

        public override IReadOnlyCollection<Guid>? Antwortende => zuschlag is { } z ? [z.Gewinner] : null;

        public override string Hinweis
            => zuschlag is { } z ? $"Auktion: {Grundlage.NameVon(z.Gewinner)} antwortet allein – Preis {z.Preis}" : string.Empty;

        protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
        {
            base.Uebernimm(abgaben);
            zuschlag = AuktionRegel.Ermittle(Abgaben, Grundlage.Spielweise == AuktionRegel.ZweitpreisSpielweise);
        }

        protected override Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang)
            => zuschlag is { } z && z.Gewinner == spieler ? AuktionRegel.Werte(z.Preis, eingang) : null;

        protected override string AbgabeText(Guid spieler) => Von(spieler) switch
        {
            null => "Kein Gebot",
            { Betrag: 0 } => "Gepasst",
            { } gebot when zuschlag?.Gewinner == spieler => $"Gebot {gebot.Betrag} – ersteigert für {zuschlag.Preis}",
            { } gebot => $"Gebot {gebot.Betrag} – überboten",
        };
    }
}
