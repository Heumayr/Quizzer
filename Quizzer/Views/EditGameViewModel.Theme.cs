using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Die Designwahl des Spielabends.
    /// <para>
    /// Ein Design gehoert zum Spiel, nicht zum Programm - der eine Abend soll anders aussehen
    /// duerfen als der naechste. Gewaehlt wird hier im Spielaufbau, und das Raster daneben zeigt
    /// die Wahl sofort: sonst muesste der Spielleiter das Spiel starten, um zu sehen, was er
    /// eingestellt hat.
    /// </para>
    /// <para>
    /// Eigene Teildatei, weil <c>EditGameViewModel.cs</c> ueber der Groessen-Obergrenze liegt
    /// (standards-allgemein.md §5).
    /// </para>
    /// </summary>
    public partial class EditGameViewModel
    {
        private ObservableCollection<GameTheme> verfuegbareDesigns = new();

        /// <summary>
        /// Die waehlbaren Designs. Der erste Eintrag ist immer der Auslieferungsstand - er hat
        /// eine leere Kennung und steht fuer "kein eigenes Design".
        /// </summary>
        public ObservableCollection<GameTheme> AvailableThemes
        {
            get => verfuegbareDesigns;
            private set
            {
                verfuegbareDesigns = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Das Design dieses Spiels. <see cref="Guid.Empty"/> heisst Auslieferungsstand.</summary>
        public Guid GameThemeId
        {
            get => Game?.GameThemeId ?? Guid.Empty;
            set
            {
                if (Game == null) return;

                var neu = value == Guid.Empty ? (Guid?)null : value;

                if (Game.GameThemeId == neu) return;

                Game.GameThemeId = neu;
                Game.GameTheme = AvailableThemes.FirstOrDefault(t => t.Id == value);

                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeHint));

                ApplyThemeToPreview();
            }
        }

        /// <summary>Was unter der Auswahl steht - der Ordner und die Notiz des Designs.</summary>
        public string ThemeHint
        {
            get
            {
                var gewaehlt = Game?.GameTheme;

                if (gewaehlt == null || gewaehlt.Id == Guid.Empty)
                    return "Das mitgelieferte Design mit seinen Texturen.";

                var teile = new List<string>();

                if (!string.IsNullOrWhiteSpace(gewaehlt.FolderName))
                    teile.Add($"Ordner: Themes\\{gewaehlt.FolderName}");

                if (!string.IsNullOrWhiteSpace(gewaehlt.Notes))
                    teile.Add(gewaehlt.Notes);

                return teile.Count == 0 ? "Ohne eigene Texturen." : string.Join(" — ", teile);
            }
        }

        /// <summary>
        /// Laedt die Designliste und stellt die Vorschau auf das Design des Spiels.
        /// Wird beim Laden des Modells gerufen.
        /// </summary>
        public async Task LoadThemesAsync()
        {
            using var ctrl = new GameThemesController();

            var alle = (await ctrl.GetAllAsync())
                .OrderBy(t => t.Designation, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // Der Auslieferungsstand als erster Eintrag, mit leerer Kennung.
            var liste = new List<GameTheme>
            {
                new() { Id = Guid.Empty, Designation = "Mitgeliefertes Design" },
            };

            liste.AddRange(alle);

            AvailableThemes = new ObservableCollection<GameTheme>(liste);

            OnPropertyChanged(nameof(GameThemeId));
            OnPropertyChanged(nameof(ThemeHint));

            ApplyThemeToPreview();
        }

        /// <summary>
        /// Stellt die Pinsel auf das gewaehlte Design um und laesst die Zellen neu zeichnen.
        /// <para>
        /// Ohne das zweite haette die Umstellung keine sichtbare Wirkung: die Zell-ViewModels
        /// lesen ihre Pinsel ueber Properties, und WPF fragt eine Property erst wieder ab,
        /// wenn ein <c>PropertyChanged</c> kommt.
        /// </para>
        /// </summary>
        private void ApplyThemeToPreview()
        {
            if (!ThemeBrushes.SetCurrent(Game?.GameTheme))
                return;

            OnPropertyChanged(nameof(PlayGroundBackGroundBrush));
            OnPropertyChanged(nameof(HeaderColumnBrush));
            OnPropertyChanged(nameof(HeaderRowBrush));

            // Nur die Zellen: die Kopfzeilen binden ihren Grund ueber den Elternkontext
            // (RelativeSource AncestorType=ItemsControl), sie ziehen also mit den drei
            // Meldungen oben nach.
            foreach (var zelle in GameGridVMs.CellVMs)
                zelle.RefreshFromModel();
        }

        /// <summary>Der Grund hinter dem Raster in der Vorschau.</summary>
        public System.Windows.Media.Brush PlayGroundBackGroundBrush => ThemeBrushes.Current.Hintergrund;

        /// <summary>Der Grund der Spaltenkoepfe in der Vorschau.</summary>
        public System.Windows.Media.Brush HeaderColumnBrush => ThemeBrushes.Current.SpaltenKopf;

        /// <summary>Der Grund der Zeilenkoepfe in der Vorschau.</summary>
        public System.Windows.Media.Brush HeaderRowBrush => ThemeBrushes.Current.ZeilenKopf;

        private AsyncRelayCommand? openThemesCommand;

        /// <summary>Oeffnet den Design-Editor und laedt die Liste danach neu.</summary>
        public ICommand OpenThemesCommand => openThemesCommand ??= new AsyncRelayCommand(OpenThemesAsync);

        private async Task OpenThemesAsync(object? commandParameter)
        {
            var fenster = new GameThemesView();

            fenster.ShowDialog();

            // Der Editor kann Texturen ausgetauscht haben - der Zwischenspeicher muss weg,
            // sonst zeigt die Vorschau weiter das alte Bild.
            ThemeBrushes.Forget();

            await LoadThemesAsync();
        }
    }
}
