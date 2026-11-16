using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>Was eine Regel beim Oeffnen der Zelle braucht - fuer die Vorphase am Server und fuer ihre Wertung.</summary>
    /// <param name="Feldpunkte">Was „richtig" in der Zelle bringt.</param>
    /// <param name="Kontostaende">Kontostand je Spieler beim Oeffnen.</param>
    /// <param name="Namen">Anzeigename je Spieler - fuer Beamer und Spielerkarte.</param>
    /// <param name="Ausloeser">Wer das Feld aufgedeckt hat, wenn er mitspielt (Allianz).</param>
    /// <param name="Anteil">Anteil der Zelle in Prozent, Vorgabe schon eingesetzt (M35 C3b).</param>
    /// <param name="Spielweise">Spielweise der Zelle, Index im Profil (M35 C3b).</param>
    internal sealed record VorphaseGrundlage(
        int Feldpunkte, IReadOnlyDictionary<Guid, int> Kontostaende, IReadOnlyDictionary<Guid, string>? Namen = null, Guid? Ausloeser = null,
        int Anteil = 0, int Spielweise = 0, IReadOnlyList<Guid>? Reihenfolge = null)
    {
        public static VorphaseGrundlage Leer { get; } = new(0, new Dictionary<Guid, int>());

        /// <summary>Die Spieler in der Reihenfolge der Karten - beim Experten entscheidet sie den Gleichstand vor.</summary>
        public IReadOnlyList<Guid> Spielerfolge => Reihenfolge ?? Kontostaende.Keys.ToList();

        /// <summary>Der Name zu einer Id - fuer Gruende und Abgabetexte.</summary>
        public string NameVon(Guid spieler) => Namen?.GetValueOrDefault(spieler) ?? "?";
    }

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

        /// <summary>Wer die Frage nach der Vorphase beantworten darf (M35 C3b); <c>null</c>: alle.</summary>
        public virtual IReadOnlyCollection<Guid>? Antwortende => null;

        /// <summary>Ob die Regel eine Vorphase am Telefon spielt (M35 C3b) - das Kopfgeld nicht, es steht beim Oeffnen fest.</summary>
        public virtual bool HatVorphase => Profil.HasPrePhase;

        /// <summary>Was Beamer und Spielleiter waehrend der Frage dazu lesen, etwa „Kopfgeld auf Anna"; leer: nichts.</summary>
        public virtual string Hinweis => string.Empty;

        /// <summary>Unter wem der Spielleiter waehlt, wer antwortet (Experte bei Gleichstand); leer: nichts zu waehlen.</summary>
        public virtual IReadOnlyList<Guid> Kandidaten => [];

        /// <summary>Der Spielleiter waehlt unter den Kandidaten (E1); <c>false</c>, wenn es keiner ist.</summary>
        public virtual bool WaehleKandidat(Guid spieler) => false;

        /// <summary>Was die Regel beim Oeffnen der Zelle bekam - Kontostaende, Namen, Ausloeser, Anteil.</summary>
        public VorphaseGrundlage Grundlage { get; private set; } = VorphaseGrundlage.Leer;

        /// <summary>Der Regeltext fuer Beamer und Telefon - Spielweise und Anteil der Zelle eingesetzt.</summary>
        public string Regeltext => Profil.RulesTextFor(Grundlage.Spielweise, Grundlage.Anteil);

        /// <summary>Die Zelle ist offen: die Regel merkt sich, was sie spaeter braucht.</summary>
        public void Oeffne(VorphaseGrundlage grundlage)
        {
            Grundlage = grundlage;
            BeimOeffnen();
        }

        /// <summary>Was eine Regel beim Oeffnen selbst festlegt - das Kopfgeld seine Gesuchten.</summary>
        protected virtual void BeimOeffnen()
        {
        }

        /// <summary>Ob die Vorphase geschlossen ist - erst dann rechnet die Regel.</summary>
        public bool VorphaseZu { get; private set; }

        /// <summary>Die Regel zur Zelle; <c>null</c> bei keiner oder einer unbekannten.</summary>
        public static SonderregelAblauf? Fuer(CellSpecialRule regel) => regel switch
        {
            CellSpecialRule.Stake => new EinsatzAblauf(),
            CellSpecialRule.Risk => new RisikoAblauf(),
            CellSpecialRule.Auction => new AuktionAblauf(),
            CellSpecialRule.Bet => new WetteAblauf(),
            CellSpecialRule.Expert => new ExperteAblauf(),
            CellSpecialRule.Alliance => new AllianzAblauf(),
            _ => null,
        };

        /// <summary>Wie der Server die Vorphase einstellt - eine Runde, ohne Countdown.</summary>
        public virtual MinigameSetup Setup(VorphaseGrundlage grundlage)
            => new(default, Titel, Regeltext, 1, CountdownSeconds: 0, TriggerPlayerId: grundlage.Ausloeser,
                Variant: grundlage.Spielweise, SpecialRule: Profil.Rule, FieldPoints: grundlage.Feldpunkte,
                Balances: grundlage.Kontostaende, Share: grundlage.Anteil);

        /// <summary>Die Vorphase ist zu: wer nichts abgegeben hat, fuer den greift die Regel nicht.</summary>
        public void Schliesse(IReadOnlyDictionary<Guid, object> abgaben)
        {
            Uebernimm(abgaben);
            VorphaseZu = true;
        }

        /// <summary>
        /// Was die Regel aus <b>allen</b> Bewertungen der Zelle macht - je Spieler ein Vorschlag; wer fehlt, fuer
        /// den greift sie nicht. Leer, solange die Vorphase laeuft.
        /// </summary>
        public IReadOnlyDictionary<Guid, Punktevorschlag> WerteAlle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
            => VorphaseZu ? WerteZelle(eingaenge) : new Dictionary<Guid, Punktevorschlag>();

        /// <summary>Die Vorphase dieses Spielers im Klartext - fuer die Spielerkarte; leer, solange sie laeuft.</summary>
        public string Abgabe(Guid spieler) => VorphaseZu ? AbgabeText(spieler) : string.Empty;

        protected abstract void Uebernimm(IReadOnlyDictionary<Guid, object> abgaben);

        /// <summary>Je Spieler aus seiner eigenen Abgabe - eine Regel, die an fremden Bewertungen haengt, ueberschreibt das.</summary>
        protected virtual IReadOnlyDictionary<Guid, Punktevorschlag> WerteZelle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var (spieler, eingang) in eingaenge)
            {
                if (WerteAbgabe(spieler, eingang) is { } vorschlag)
                    vorschlaege[spieler] = vorschlag;
            }

            return vorschlaege;
        }

        /// <summary>Was die Regel aus der eigenen Bewertung macht; <c>null</c>: sie greift nicht.</summary>
        protected virtual Punktevorschlag? WerteAbgabe(Guid spieler, SonderregelEingang eingang) => null;

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

        /// <summary>Alle Abgaben der Vorphase - fuer Regeln, die ueber alle rechnen (Auktion, Experte).</summary>
        protected IReadOnlyDictionary<Guid, T> Abgaben => abgaben;

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
