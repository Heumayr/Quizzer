using Quizzer.Base;
using Quizzer.Views.GameViews.Teamregeln;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using static LocalBuzzer.Service.Base.States.BuzzerInputState;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// „Gleich gedacht" im Fragefenster (M37 T6): sind die Antworten zu - alle abgegeben oder „Runde schließen" -, vergleicht
    /// die Regel, die Karten tragen die Anteile (E1), und der Beamer zeigt je Team die Gruppen als Text. Der Spielleiter sieht
    /// dieselben Gruppen zum Anhaken und legt Gruppen eines Teams zusammen, etwa bei einem Tippfehler.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private GleichGedachtAblauf? GleichGedachtRegel => sonderregel as GleichGedachtAblauf;

        /// <summary>Die Gruppen aller Teams zum Anhaken - erst nach dem Vergleich, sonst leer.</summary>
        public IReadOnlyList<GleichGedachtEintrag> GleichGedachtGruppen { get; private set; } = [];

        public Visibility GleichGedachtVisibility => GleichGedachtGruppen.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        private RelayCommand? gruppenZusammenlegenCommand;

        /// <summary>„Angehakte zusammenlegen" - nur Gruppen eines Teams.</summary>
        public ICommand GruppenZusammenlegenCommand
            => gruppenZusammenlegenCommand ??= new RelayCommand(_ => LegeGruppenZusammen(GleichGedachtGruppen.Where(e => e.Markiert).ToList()));

        private RelayCommand? zusammenlegenAufhebenCommand;

        public ICommand ZusammenlegenAufhebenCommand => zusammenlegenAufhebenCommand ??= new RelayCommand(_ => HebeZusammenlegenAuf());

        /// <summary>Die Antworten sind zu: die Regel vergleicht, Karten, Beamer und Liste folgen.</summary>
        private void GleichGedachtAntwortenZu(IReadOnlyDictionary<Guid, InputResult> eingaben)
        {
            if (GleichGedachtRegel is not { } regel)
                return;

            regel.NimmAntworten(eingaben.ToDictionary(e => e.Key, e => (string?)e.Value.Value));
            ZeigeGleichGedacht(regel);
        }

        /// <summary>
        /// Die Spielleitung legt die angehakten Gruppen zusammen (E1): die weiteren zur ersten. <c>false</c> bei weniger als
        /// zwei oder bei Gruppen aus verschiedenen Teams - dann ändert sich nichts.
        /// </summary>
        internal bool LegeGruppenZusammen(IReadOnlyList<GleichGedachtEintrag> eintraege)
        {
            if (GleichGedachtRegel is not { } regel || eintraege.Select(e => e.TeamId).Distinct().Count() != 1
                || !regel.LegeZusammen(eintraege[0].TeamId, eintraege.Select(e => e.Schluessel).ToList()))
                return false;

            ZeigeGleichGedacht(regel);
            return true;
        }

        /// <summary>Was zusammengelegt war, gilt nicht mehr - Karten und Beamer zeigen wieder den Vergleich allein.</summary>
        internal bool HebeZusammenlegenAuf()
        {
            if (GleichGedachtRegel is not { } regel || !regel.HebeZusammenlegenAuf())
                return false;

            ZeigeGleichGedacht(regel);
            return true;
        }

        private void ZeigeGleichGedacht(GleichGedachtAblauf regel)
        {
            var teams = regel.Gruppen;
            var gleich = teams.Count(t => t.Ergebnis.Gruppen.Any(g => g.Zaehlt));

            GleichGedachtGruppen = teams
                .SelectMany(t => t.Ergebnis.Gruppen.Select(g => new GleichGedachtEintrag(t.Team.TeamId, g.Schluessel, regel.Beschriftung(t.Team, g))))
                .ToList();

            ZeigeTeamregel(regel, $"Verglichen: in {gleich} von {teams.Count} Teams gleich gedacht");
            Sonderregel.ZeigeAufloesung(regel.Aufloesung());
            WerteSonderregel();

            OnPropertyChanged(nameof(GleichGedachtGruppen));
            OnPropertyChanged(nameof(GleichGedachtVisibility));
        }

        private void LeereGleichGedacht()
        {
            GleichGedachtGruppen = [];
            OnPropertyChanged(nameof(GleichGedachtGruppen));
            OnPropertyChanged(nameof(GleichGedachtVisibility));
        }
    }

    /// <summary>Eine Gruppe in der Liste des Spielleiters bei „Gleich gedacht" - angehakt wird zum Zusammenlegen.</summary>
    public sealed class GleichGedachtEintrag : INotifyPropertyChanged
    {
        private bool markiert;

        internal GleichGedachtEintrag(Guid teamId, string schluessel, string beschriftung)
        {
            TeamId = teamId;
            Schluessel = schluessel;
            Beschriftung = beschriftung;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        internal Guid TeamId { get; }

        internal string Schluessel { get; }

        public string Beschriftung { get; }

        public bool Markiert
        {
            get => markiert;
            set
            {
                markiert = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Markiert)));
            }
        }
    }
}
