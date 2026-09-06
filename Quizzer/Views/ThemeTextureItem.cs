using Quizzer.Base;
using Quizzer.DataModels.Themes;
using Quizzer.Views.StaticRessources;
using System.Windows.Media;

namespace Quizzer.Views
{
    /// <summary>
    /// Eine Zeile im Design-Editor: eine austauschbare Textur samt Vorschau.
    /// <para>
    /// Die Vorschau zeigt, was im Spiel wirklich zu sehen wäre - also die eigene Textur, wenn
    /// das Design eine mitbringt, und sonst die mitgelieferte. So ist ohne Umweg erkennbar, was
    /// eigen ist und was geerbt.
    /// </para>
    /// </summary>
    public class ThemeTextureItem : ViewCommonBase
    {
        private readonly string ordner;

        public ThemeTextureItem(ThemeAssets.Textur textur, string ordner)
        {
            this.ordner = ordner;

            Schluessel = textur.Schluessel;
            Dateiname = textur.Dateiname;
            Beschriftung = textur.Beschriftung;
            Erklaerung = textur.Erklaerung;
        }

        public string Schluessel { get; }

        public string Dateiname { get; }

        public string Beschriftung { get; }

        public string Erklaerung { get; }

        /// <summary>Bringt das Design diese Textur selbst mit?</summary>
        public bool IsOwn => ThemeAssets.IsOwn(ordner, Dateiname);

        /// <summary>Im Klartext, damit die Liste ohne Farbdeutung lesbar bleibt.</summary>
        public string HerkunftText => IsOwn ? "eigene Textur" : "mitgeliefert";

        /// <summary>Das Bild, das im Spiel erscheinen würde.</summary>
        public Brush Vorschau =>
            StaticResources.CreateImageBrushOrFallback(
                ThemeAssets.Resolve(ordner, Dateiname), Stretch.UniformToFill, Brushes.DimGray);

        /// <summary>Nach einem Austausch: alles neu abfragen lassen.</summary>
        public void Aktualisieren()
        {
            OnPropertyChanged(nameof(IsOwn));
            OnPropertyChanged(nameof(HerkunftText));
            OnPropertyChanged(nameof(Vorschau));
        }
    }
}
