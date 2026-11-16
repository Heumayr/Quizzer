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

        internal void Zeige(string kopfzeile, IReadOnlyList<ReaktionTafelZeile>? zeilen = null)
        {
            Kopfzeile = kopfzeile;
            Zeilen = zeilen ?? [];

            OnPropertyChanged(nameof(Kopfzeile));
            OnPropertyChanged(nameof(Zeilen));
            OnPropertyChanged(nameof(ZeilenVisibility));
        }
    }
}
