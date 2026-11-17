using Quizzer.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Statistics;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Was Beamer und Spielleiter bei der Reaktion sehen (M35 B3): „Runde n von m", danach die Zeiten.
    /// Ein Objekt fuer beide Bildschirme - die Schrittkontexte zeigen ueber ihren Owner hierher.
    /// </summary>
    public sealed class ReaktionAnzeige : UcViewModelBase
    {
        public string Kopfzeile { get; private set; } = string.Empty;

        public IReadOnlyList<ReaktionTafelZeile> Zeilen { get; private set; } = [];

        public Visibility ZeilenVisibility => Zeilen.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Die Aufloesung nach der Runde (Blitzrunde, Buchstabensalat) - waehrend der Runde immer leer.</summary>
        public IReadOnlyList<string> Aufloesung { get; private set; } = [];

        public Visibility AufloesungVisibility => Aufloesung.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>„Neuer Rekord!" zur Tafel nach dem Ende (M37 E02); <c>null</c>: keiner. Jede neue Tafel nimmt ihn weg.</summary>
        public RekordAnsage? Rekord { get; private set; }

        public Visibility RekordVisibility => Rekord != null ? Visibility.Visible : Visibility.Collapsed;

        internal void Zeige(string kopfzeile, IReadOnlyList<ReaktionTafelZeile>? zeilen = null, IReadOnlyList<string>? aufloesung = null)
        {
            Kopfzeile = kopfzeile;
            Zeilen = zeilen ?? [];
            Aufloesung = aufloesung ?? [];
            ZeigeRekord(null);
            ZeigeSeil(null);

            OnPropertyChanged(nameof(Kopfzeile));
            OnPropertyChanged(nameof(Zeilen));
            OnPropertyChanged(nameof(ZeilenVisibility));
            OnPropertyChanged(nameof(Aufloesung));
            OnPropertyChanged(nameof(AufloesungVisibility));
        }

        internal void ZeigeRekord(RekordAnsage? ansage)
        {
            Rekord = ansage;

            OnPropertyChanged(nameof(Rekord));
            OnPropertyChanged(nameof(RekordVisibility));
        }

        /// <summary>Das Seil des Tauziehens bei zwei Teams (M37 T9); <c>null</c>: keins. Jede neue Tafel nimmt es weg.</summary>
        public TauziehenSeil? Seil { get; private set; }

        public Visibility SeilVisibility => Seil != null ? Visibility.Visible : Visibility.Collapsed;

        internal void ZeigeSeil(TauziehenSeil? seil)
        {
            Seil = seil;

            OnPropertyChanged(nameof(Seil));
            OnPropertyChanged(nameof(SeilVisibility));
        }
    }
}
