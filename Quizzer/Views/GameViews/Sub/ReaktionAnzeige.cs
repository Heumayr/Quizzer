using Quizzer.Base;
using Quizzer.DataModels.Questions.Minispiele;
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

        internal void Zeige(string kopfzeile, IReadOnlyList<ReaktionTafelZeile>? zeilen = null, IReadOnlyList<string>? aufloesung = null)
        {
            Kopfzeile = kopfzeile;
            Zeilen = zeilen ?? [];
            Aufloesung = aufloesung ?? [];

            OnPropertyChanged(nameof(Kopfzeile));
            OnPropertyChanged(nameof(Zeilen));
            OnPropertyChanged(nameof(ZeilenVisibility));
            OnPropertyChanged(nameof(Aufloesung));
            OnPropertyChanged(nameof(AufloesungVisibility));
        }
    }
}
