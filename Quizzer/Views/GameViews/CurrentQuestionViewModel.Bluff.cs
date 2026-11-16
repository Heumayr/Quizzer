using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using System.ComponentModel;
using System.Windows;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Bluff im Fragefenster (M35 C6): zwischen den Phasen streicht der Spielleiter Bluffs, die der echten Antwort
    /// gleichen. Was ihr schon nach dem Vergleich des Buchstabensalats gleicht, ist von selbst gestrichen und bleibt es.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Die Bluffs zwischen den Phasen - sonst leer.</summary>
        public IReadOnlyList<BluffStreichEintrag> BluffEintraege { get; private set; } = [];

        public Visibility BluffStreichenVisibility
            => MinispielSteuerungVisibility == Visibility.Visible && Minispiel?.Kind == MinigameKind.Bluff
               && BuzzerControlsViewModel?.MinigameState is { Phase: MinigamePhase.RoundOver, Round: 1 }
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Nur im Spielleiterfenster, nie am Beamer.</summary>
        public string BluffEchteAntwort
            => $"Echte Antwort: {BuzzerControlsViewModel?.MinigameState?.Lenke(r => (r as BluffRules)?.EchteAntwort) ?? "?"}";

        /// <summary>Aus dem Ablauf: eine Phase hat begonnen oder ist zu - die Liste kommt neu vom Server.</summary>
        internal void BluffGeaendert()
        {
            var zustand = BuzzerControlsViewModel?.MinigameState;

            BluffEintraege = zustand?.Lenke(r => (r as BluffRules)?.Eingaenge)?
                .Select(e => new BluffStreichEintrag(e, streichen => zustand.Lenke(r => (r as BluffRules)?.Streiche(e.Schluessel, streichen) == true)))
                .ToList() ?? [];

            OnPropertyChanged(nameof(BluffEintraege));
            OnPropertyChanged(nameof(BluffStreichenVisibility));
            OnPropertyChanged(nameof(BluffEchteAntwort));
        }
    }

    /// <summary>Ein Bluff in der Streichliste des Spielleiters - ohne Urheber.</summary>
    public sealed class BluffStreichEintrag : INotifyPropertyChanged
    {
        private readonly Func<bool, bool> streiche;
        private bool gestrichen;

        internal BluffStreichEintrag(BluffEintrag eintrag, Func<bool, bool> streiche)
        {
            this.streiche = streiche;
            gestrichen = eintrag.Gestrichen;
            Aenderbar = !eintrag.VonSelbst;
            Beschriftung = eintrag.Text + (eintrag.Anzahl > 1 ? $" ({eintrag.Anzahl}×)" : string.Empty)
                           + (eintrag.VonSelbst ? " – gleicht der echten Antwort" : string.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Beschriftung { get; }

        /// <summary>Was schon der Vergleich gestrichen hat, bleibt gestrichen.</summary>
        public bool Aenderbar { get; }

        /// <summary>Angehakt: steht in Phase 2 nicht zur Wahl; der Server entscheidet, ob es gilt.</summary>
        public bool Gestrichen
        {
            get => gestrichen;
            set
            {
                if (value != gestrichen && streiche(value))
                    gestrichen = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gestrichen)));
            }
        }
    }
}
