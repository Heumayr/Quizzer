using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>Was die Vorphase am Server braucht: die Feldpunkte und die Kontostaende beim Oeffnen der Zelle.</summary>
    internal sealed record VorphaseGrundlage(int Feldpunkte, IReadOnlyDictionary<Guid, int> Kontostaende);

    /// <summary>
    /// Eine Sonderregel im Fragefenster (M35 C3): stellt die Vorphase ein, merkt sich deren Abgaben und
    /// rechnet daraus die Bewertung auf der Spielerkarte um (E1: nur Vorschlag). Das Fenster verteilt nur
    /// hierher; eine neue Regel (C3b) bringt eine Klasse und eine Zeile in <see cref="Fuer"/>.
    /// <para>Je Zelle eine Instanz - ihr Zustand gehoert genau dieser Frage.</para>
    /// </summary>
    internal abstract class SonderregelAblauf
    {
        protected SonderregelAblauf(SpecialRuleProfile profil)
        {
            Profil = profil;
        }

        public SpecialRuleProfile Profil { get; }

        /// <summary>„Sonderregel: Einsatz" - Ueberschrift an Beamer und Telefon.</summary>
        public string Titel => $"Sonderregel: {Profil.DisplayName}";

        /// <summary>Ob die Vorphase geschlossen ist - erst dann rechnet die Regel.</summary>
        public bool VorphaseZu { get; private set; }

        /// <summary>Die Regel zur Zelle; <c>null</c> bei keiner oder einer unbekannten.</summary>
        public static SonderregelAblauf? Fuer(CellSpecialRule regel) => regel switch
        {
            CellSpecialRule.Stake => new EinsatzAblauf(),
            CellSpecialRule.Risk => new RisikoAblauf(),
            _ => null,
        };

        /// <summary>Wie der Server die Vorphase einstellt - eine Runde, ohne Countdown.</summary>
        public virtual MinigameSetup Setup(VorphaseGrundlage grundlage)
            => new(default, Titel, Profil.RulesText, 1, CountdownSeconds: 0, SpecialRule: Profil.Rule,
                FieldPoints: grundlage.Feldpunkte, Balances: grundlage.Kontostaende);

        /// <summary>Die Vorphase ist zu: wer nichts abgegeben hat, fuer den greift die Regel nicht.</summary>
        public void Schliesse(IReadOnlyDictionary<Guid, object> abgaben)
        {
            Uebernimm(abgaben);
            VorphaseZu = true;
        }

        /// <summary>Was die Regel aus der Bewertung macht; <c>null</c>: sie greift nicht, die Karte bleibt.</summary>
        public Punktevorschlag? Werte(Guid spieler, SonderregelEingang eingang) => VorphaseZu ? WerteAbgabe(spieler, eingang) : null;

        /// <summary>Die Vorphase dieses Spielers im Klartext - fuer die Spielerkarte; leer, solange sie laeuft.</summary>
        public string Abgabe(Guid spieler) => VorphaseZu ? AbgabeText(spieler) : string.Empty;

        protected abstract void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben);

        protected abstract Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang);

        protected abstract string AbgabeText(Guid spieler);
    }

    /// <summary>Gemeinsamer Teil der Regeln mit genau einer Abgabe je Spieler.</summary>
    internal abstract class SonderregelAblauf<T> : SonderregelAblauf where T : struct
    {
        private Dictionary<Guid, T> abgaben = new();

        protected SonderregelAblauf(CellSpecialRule regel)
            : base(SpecialRuleProfiles.All.Single(p => p.Rule == regel))
        {
        }

        protected T? Von(Guid spieler) => abgaben.TryGetValue(spieler, out var wert) ? wert : null;

        protected override void Uebernimm(IReadOnlyDictionary<Guid, object> neu)
            => abgaben = neu.Where(e => e.Value is T).ToDictionary(e => e.Key, e => (T)e.Value);
    }

    /// <summary>Einsatz (M35 C3): statt der Feldpunkte zaehlt der Einsatz - <see cref="EinsatzRegel"/>.</summary>
    internal sealed class EinsatzAblauf : SonderregelAblauf<int>
    {
        public EinsatzAblauf()
            : base(CellSpecialRule.Stake)
        {
        }

        protected override Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang)
            => Von(spieler) is { } einsatz ? EinsatzRegel.Werte(einsatz, eingang) : null;

        protected override string AbgabeText(Guid spieler)
            => Von(spieler) is { } einsatz ? $"Einsatz: {einsatz}" : "Kein Einsatz – es zählen die Feldpunkte";
    }

    /// <summary>Risiko (M35 C3): wer doppelt spielt, bekommt Plus und Minus doppelt - <see cref="RisikoRegel"/>.</summary>
    internal sealed class RisikoAblauf : SonderregelAblauf<bool>
    {
        public RisikoAblauf()
            : base(CellSpecialRule.Risk)
        {
        }

        protected override Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang)
            => Von(spieler) is { } doppelt ? RisikoRegel.Werte(doppelt, eingang) : null;

        protected override string AbgabeText(Guid spieler) => Von(spieler) switch
        {
            true => "Risiko: doppelt",
            false => "Risiko: einfach",
            _ => "Keine Wahl – es zählt wie immer",
        };
    }
}
