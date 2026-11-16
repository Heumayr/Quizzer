using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>
    /// Experte (M35 C3b): der Meistgewaehlte antwortet allein, seine Waehler bekommen den Anteil, wenn er richtig ist -
    /// <see cref="ExperteRegel"/>. Bei Gleichstand gilt der erste in der Spielerreihenfolge; der Spielleiter kann im
    /// Fragefenster unter den Gleichauf umwaehlen (E1). Waehlt niemand, spielt die Frage normal.
    /// </summary>
    internal sealed class ExperteAblauf : SonderregelAblauf<Guid>
    {
        private IReadOnlyList<Guid> kandidaten = [];
        private Guid? experte;

        public ExperteAblauf()
            : base(CellSpecialRule.Expert)
        {
        }

        public override IReadOnlyCollection<Guid>? Antwortende => experte is { } e ? [e] : null;

        public override IReadOnlyList<Guid> Kandidaten => kandidaten;

        public override string Hinweis => experte is { } e ? $"Experte: {Grundlage.NameVon(e)} antwortet für alle" : string.Empty;

        public override bool WaehleKandidat(Guid spieler)
        {
            if (!kandidaten.Contains(spieler))
                return false;

            experte = spieler;
            return true;
        }

        protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
        {
            base.Uebernimm(abgaben);
            kandidaten = ExperteRegel.Kandidaten(Abgaben, Grundlage.Spielerfolge);
            experte = kandidaten.Count > 0 ? kandidaten[0] : null;
        }

        protected override IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => experte is { } e
                ? ExperteRegel.Werte(e, Abgaben, eingaenge, Punkteanteil.Von(Grundlage.Feldpunkte, Grundlage.Anteil))
                : new Dictionary<Guid, Punktevorschlag>();

        protected override string AbgabeText(Guid spieler)
        {
            var wahl = Von(spieler) is { } gewaehlt ? $"Wählt {Grundlage.NameVon(gewaehlt)}" : "Keine Wahl";
            return experte == spieler ? $"Antwortet als Experte · {wahl}" : wahl;
        }
    }
}
