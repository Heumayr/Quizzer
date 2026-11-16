using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>
    /// Allianz (M35 C3b): der Ausloeser waehlt einen Partner, nur die beiden antworten - <see cref="AllianzRegel"/>.
    /// Ohne Ausloeser keine Vorphase und keine Allianz: die Frage spielt normal. Waehlt er niemanden, ebenso.
    /// </summary>
    internal sealed class AllianzAblauf : SonderregelAblauf<Guid>
    {
        public AllianzAblauf()
            : base(CellSpecialRule.Alliance)
        {
        }

        /// <summary>Ohne Ausloeser gibt es niemanden, der waehlt - dann keine Vorphase.</summary>
        public override bool HatVorphase => Grundlage.Ausloeser != null;

        private Guid? Partner => Grundlage.Ausloeser is { } a ? Von(a) : null;

        public override IReadOnlyCollection<Guid>? Antwortende
            => Grundlage.Ausloeser is { } a && Partner is { } p ? [a, p] : null;

        public override string Hinweis
            => Grundlage.Ausloeser is not { } a ? "Keine Allianz – niemand hat das Feld aufgedeckt"
                : Partner is { } p ? $"Allianz: {Grundlage.NameVon(a)} und {Grundlage.NameVon(p)} antworten"
                : string.Empty;

        protected override IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => Grundlage.Ausloeser is { } a && Partner is { } p
                ? AllianzRegel.Werte(a, p, eingaenge)
                : new Dictionary<Guid, Punktevorschlag>();

        protected override string AbgabeText(Guid spieler)
        {
            if (Grundlage.Ausloeser is not { } a || Partner is not { } p)
                return spieler == Grundlage.Ausloeser ? "Kein Partner gewählt" : string.Empty;

            return spieler == a ? $"Allianz mit {Grundlage.NameVon(p)}"
                : spieler == p ? $"Allianz mit {Grundlage.NameVon(a)}"
                : "Nicht in der Allianz";
        }
    }
}
