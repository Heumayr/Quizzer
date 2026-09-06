using Microsoft.Win32;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Themes;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Views.StaticRessources;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Der Design-Editor: Designs anlegen, benennen und ihre Texturen austauschen.
    /// <para>
    /// Ein Design ist ein Ordner unter <c>Themes</c> im Datenverzeichnis. Eine Textur
    /// auszutauschen heißt: eine Bilddatei dorthin kopieren, unter dem Namen, den das Programm
    /// erwartet. Genau das macht „Bild wählen" - der Spielleiter muss weder den Ordner kennen
    /// noch den richtigen Dateinamen treffen.
    /// </para>
    /// <para>
    /// <b>Das mitgelieferte Design bleibt unberührt.</b> Es liegt im Datenverzeichnis selbst und
    /// wird nie überschrieben; ein Design kann es nur überlagern. Wer eine eigene Textur wieder
    /// entfernt, sieht sofort wieder die mitgelieferte.
    /// </para>
    /// </summary>
    public class GameThemesViewModel : ViewModelBase
    {
        private ObservableCollection<GameTheme> designs = new();
        private GameTheme? ausgewaehlt;

        /// <summary>Alle angelegten Designs.</summary>
        public ObservableCollection<GameTheme> Themes
        {
            get => designs;
            private set
            {
                designs = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Das Design, das gerade bearbeitet wird.</summary>
        public GameTheme? SelectedTheme
        {
            get => ausgewaehlt;
            set
            {
                ausgewaehlt = value;

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectionVisibility));
                OnPropertyChanged(nameof(FolderHint));

                BaueTexturliste();
            }
        }

        public bool HasSelection => SelectedTheme != null;

        public System.Windows.Visibility SelectionVisibility =>
            HasSelection ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        /// <summary>Wo die Texturen dieses Designs liegen - im Klartext, zum Nachsehen.</summary>
        public string FolderHint
        {
            get
            {
                if (SelectedTheme == null)
                    return string.Empty;

                var ordner = ThemeAssets.FolderOf(SelectedTheme.FolderName);

                return string.IsNullOrEmpty(ordner)
                    ? "Ohne eigenen Ordner - alle Texturen kommen aus dem mitgelieferten Design."
                    : ordner;
            }
        }

        private ObservableCollection<ThemeTextureItem> texturen = new();

        /// <summary>Die zwölf austauschbaren Texturen des gewählten Designs.</summary>
        public ObservableCollection<ThemeTextureItem> Textures
        {
            get => texturen;
            private set
            {
                texturen = value;
                OnPropertyChanged();
            }
        }

        private void BaueTexturliste()
        {
            if (SelectedTheme == null)
            {
                Textures = new ObservableCollection<ThemeTextureItem>();
                return;
            }

            Textures = new ObservableCollection<ThemeTextureItem>(
                ThemeAssets.Texturen.Select(t => new ThemeTextureItem(t, SelectedTheme.FolderName)));
        }

        protected override async Task OnloadAsync() => await LadeAsync();

        private async Task LadeAsync()
        {
            using var ctrl = new GameThemesController();

            var alle = (await ctrl.GetAllAsync())
                .OrderBy(t => t.Designation, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            Themes = new ObservableCollection<GameTheme>(alle);
            SelectedTheme = alle.FirstOrDefault(t => t.Id == SelectedTheme?.Id) ?? alle.FirstOrDefault();
        }

        public override async Task VMSaveAsync()
        {
            if (Themes.Count == 0) return;

            using var ctrl = new GameThemesController();

            foreach (var design in Themes)
                await ctrl.UpsertAsync(design);

            await ctrl.SaveChangesAsync();
        }

        private AsyncRelayCommand? saveCommand;
        public ICommand SaveCommand => saveCommand ??= new AsyncRelayCommand(SpeichernAsync);

        private async Task SpeichernAsync(object? _)
        {
            await VMSaveAsync();
            await LadeAsync();
        }

        private AsyncRelayCommand? addCommand;
        public ICommand AddThemeCommand => addCommand ??= new AsyncRelayCommand(AnlegenAsync);

        /// <summary>
        /// Legt ein Design an - Datenbankzeile und Ordner in einem Zug. Ohne den Ordner ließe
        /// sich keine Textur ablegen, und der Spielleiter müsste ihn selbst treffen.
        /// </summary>
        private async Task AnlegenAsync(object? _)
        {
            var nummer = Themes.Count + 1;
            var ordner = $"Design{nummer}";

            while (Directory.Exists(Path.Combine(ThemeAssets.ThemesRoot, ordner)))
            {
                nummer++;
                ordner = $"Design{nummer}";
            }

            var design = new GameTheme
            {
                Id = Guid.NewGuid(),
                Designation = $"Design {nummer}",
                FolderName = ordner,
                Notes = "Noch ohne eigene Texturen.",
            };

            Directory.CreateDirectory(Path.Combine(ThemeAssets.ThemesRoot, ordner));

            using (var ctrl = new GameThemesController())
            {
                await ctrl.InsertAsync(design);
                await ctrl.SaveChangesAsync();
            }

            await LadeAsync();

            SelectedTheme = Themes.FirstOrDefault(t => t.Id == design.Id);
        }

        private AsyncRelayCommand? removeCommand;
        public ICommand RemoveThemeCommand => removeCommand ??= new AsyncRelayCommand(EntfernenAsync);

        /// <summary>
        /// Entfernt ein Design. Der Ordner bleibt liegen - die Bilder darin gehören dem
        /// Spielleiter, und ein Programm löscht keine Bilder, nach denen es nicht gefragt hat.
        /// </summary>
        private async Task EntfernenAsync(object? _)
        {
            if (SelectedTheme == null) return;

            var betroffene = await SpieleMitDesignAsync(SelectedTheme.Id);

            if (betroffene.Count > 0)
            {
                UserPrompt.Inform(
                    "Dieses Design wird noch verwendet:" + Environment.NewLine + Environment.NewLine
                    + string.Join(Environment.NewLine, betroffene)
                    + Environment.NewLine + Environment.NewLine
                    + "Erst im Spielaufbau ein anderes Design wählen.",
                    "Design entfernen");

                return;
            }

            var ordner = ThemeAssets.FolderOf(SelectedTheme.FolderName);

            if (!UserPrompt.Confirm(
                    $"Das Design \"{SelectedTheme.DisplayName}\" entfernen?" + Environment.NewLine
                    + Environment.NewLine
                    + $"Der Ordner bleibt erhalten: {ordner}",
                    "Design entfernen"))
                return;

            using (var ctrl = new GameThemesController())
            {
                await ctrl.DeleteAsync(SelectedTheme.Id);
                await ctrl.SaveChangesAsync();
            }

            await LadeAsync();
        }

        private static async Task<List<string>> SpieleMitDesignAsync(Guid designId)
        {
            using var ctrl = new GamesController();

            return (await ctrl.GetAllAsync())
                .Where(g => g.GameThemeId == designId)
                .Select(g => g.Designation)
                .ToList();
        }

        private RelayCommand<ThemeTextureItem>? chooseCommand;

        /// <summary>Kopiert ein gewähltes Bild als Textur in den Design-Ordner.</summary>
        public ICommand ChooseTextureCommand =>
            chooseCommand ??= new RelayCommand<ThemeTextureItem>(Austauschen);

        private void Austauschen(ThemeTextureItem item)
        {
            if (SelectedTheme == null) return;

            var dialog = new OpenFileDialog
            {
                Title = $"Bild für \"{item.Beschriftung}\" wählen",
                CheckFileExists = true,
                Filter = "Bilder|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien|*.*",
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var ordner = ThemeAssets.FolderOf(SelectedTheme.FolderName);

                Directory.CreateDirectory(ordner);
                File.Copy(dialog.FileName, Path.Combine(ordner, item.Dateiname), overwrite: true);

                Erneuern(item);
            }
            catch (Exception ex)
            {
                UserPrompt.Inform(
                    "Das Bild ließ sich nicht übernehmen." + Environment.NewLine + Environment.NewLine
                    + ex.Message,
                    "Textur austauschen");
            }
        }

        private RelayCommand<ThemeTextureItem>? resetCommand;

        /// <summary>Nimmt eine eigene Textur zurück; danach gilt wieder die mitgelieferte.</summary>
        public ICommand ResetTextureCommand =>
            resetCommand ??= new RelayCommand<ThemeTextureItem>(Zuruecknehmen);

        private void Zuruecknehmen(ThemeTextureItem item)
        {
            if (SelectedTheme == null || !item.IsOwn) return;

            if (!UserPrompt.Confirm(
                    $"Die eigene Textur für \"{item.Beschriftung}\" löschen?" + Environment.NewLine
                    + Environment.NewLine + "Danach gilt wieder die mitgelieferte.",
                    "Textur zurücknehmen"))
                return;

            try
            {
                File.Delete(Path.Combine(ThemeAssets.FolderOf(SelectedTheme.FolderName), item.Dateiname));

                Erneuern(item);
            }
            catch (Exception ex)
            {
                UserPrompt.Inform(
                    "Die Textur ließ sich nicht löschen." + Environment.NewLine + Environment.NewLine
                    + ex.Message,
                    "Textur zurücknehmen");
            }
        }

        /// <summary>
        /// Liest eine Textur neu ein. Der Zwischenspeicher muss weg, sonst zeigt die Vorschau
        /// weiter das alte Bild - <c>BitmapCacheOption.OnLoad</c> haelt es fest.
        /// </summary>
        private void Erneuern(ThemeTextureItem item)
        {
            ThemeBrushes.Forget();

            item.Aktualisieren();

            OnPropertyChanged(nameof(Textures));
        }

        private RelayCommand? closeCommand;
        public ICommand CloseCommand => closeCommand ??= new RelayCommand(_ => Window?.Close());
    }
}
