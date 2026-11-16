using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>
    /// Kopfgeld (M35 C3b): keine Vorphase - die Gesuchten stehen beim Oeffnen fest (<see cref="KopfgeldRegel"/>), der
    /// Beamer nennt sie mit Symbol. Alle gleichauf: kein Kopfgeld, die Frage spielt normal.
    /// </summary>
    internal sealed class KopfgeldAblauf : SonderregelAblauf
    {
        private IReadOnlyList<Guid> gesuchte = [];

        public KopfgeldAblauf()
            : base(SpecialRuleProfiles.All.Single(p => p.Rule == CellSpecialRule.Bounty))
        {
        }

        public override string Hinweis
            => gesuchte.Count > 0
                ? $"Kopfgeld auf {string.Join(" und ", gesuchte.Select(Grundlage.NameVon))}"
                : "Kein Kopfgeld – alle gleichauf";

        protected override void BeimOeffnen() => gesuchte = KopfgeldRegel.Gesuchte(Grundlage.Kontostaende);

        protected override void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben)
        {
        }

        protected override IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => KopfgeldRegel.Werte(gesuchte, eingaenge, Punkteanteil.Von(Grundlage.Feldpunkte, Grundlage.Anteil), Grundlage.NameVon);

        protected override string AbgabeText(Guid spieler)
            => gesuchte.Count == 0 ? string.Empty : gesuchte.Contains(spieler) ? "Gesucht" : "Jäger";
    }
}
