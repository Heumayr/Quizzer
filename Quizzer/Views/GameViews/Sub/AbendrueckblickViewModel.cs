using Quizzer.Base;
using Quizzer.Logic.Statistics;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Die Tafel „Rückblick auf diesen Abend" nach dem Sieger (M37 E04). Gerechnet hat <see cref="Abendrueckblick"/>; hier
    /// steht nur, welche Abschnitte erscheinen - ein leerer erscheint nicht.
    /// </summary>
    public sealed class AbendrueckblickViewModel : UcViewModelBase
    {
        public AbendrueckblickViewModel(Rueckblick rueckblick)
        {
            ArgumentNullException.ThrowIfNull(rueckblick);
            Rueckblick = rueckblick;
        }

        public Rueckblick Rueckblick { get; }

        public bool IstLeer => Rueckblick.IstLeer;

        public IReadOnlyList<string> BestwertZeilen => Rueckblick.Bestwerte.Select(b => b.Zeile).ToList();

        public string KlausZeile => Rueckblick.MeisteKlaus?.Zeile ?? string.Empty;

        public IReadOnlyList<string> JokerZeilen => Rueckblick.Joker.Select(j => j.Zeile).ToList();

        public string RichtigeZeile => Rueckblick.MeisteRichtige?.Zeile ?? string.Empty;

        public Visibility BestwerteVisibility => Sichtbar(Rueckblick.Bestwerte.Count > 0);

        public Visibility KlausVisibility => Sichtbar(Rueckblick.MeisteKlaus != null);

        public Visibility JokerVisibility => Sichtbar(Rueckblick.Joker.Count > 0);

        public Visibility RichtigeVisibility => Sichtbar(Rueckblick.MeisteRichtige != null);

        private static Visibility Sichtbar(bool ja) => ja ? Visibility.Visible : Visibility.Collapsed;
    }
}
