using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Controller.TypedControllers;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Sonderregeln im Spielaufbau (M35 C3, V3): zufaellig verteilen, alle entfernen, und beim Speichern
    /// keine Regel auf einer Zelle lassen, auf der sie nie gespielt wuerde. Die Regel je Zelle waehlt der
    /// Auswahldialog der Zelle.
    /// </summary>
    public partial class EditGameViewModel
    {
        private const string SonderregelCaption = "Sonderregeln";

        private int sonderregelAnzahl = 2;
        private bool sonderregelnAngekuendigt;

        /// <summary>So viele Zellen bekommen beim Verteilen eine Regel.</summary>
        public int SonderregelAnzahl
        {
            get => sonderregelAnzahl;
            set
            {
                sonderregelAnzahl = Math.Max(0, value);
                OnPropertyChanged();
            }
        }

        /// <summary>Ob die verteilten Regeln am Beamer-Raster angekuendigt sind - sonst verdeckt.</summary>
        public bool SonderregelnAngekuendigt
        {
            get => sonderregelnAngekuendigt;
            set
            {
                sonderregelnAngekuendigt = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</summary>
        internal Func<int, int, int> Zufall { get; set; } = Random.Shared.Next;

        /// <summary>Die Zellen des Spiels im Aufbau - fuer die Tests, die das Verteilen messen.</summary>
        internal List<GameGridCoordinate> Zellen => Game?.GameGridCoordinates ?? [];

        private AsyncRelayCommand? distributeSpecialRulesCommand;
        private AsyncRelayCommand? removeSpecialRulesCommand;

        public ICommand DistributeSpecialRulesCommand
            => distributeSpecialRulesCommand ??= new AsyncRelayCommand(_ => VerteileSonderregelnAsync());

        public ICommand RemoveSpecialRulesCommand
            => removeSpecialRulesCommand ??= new AsyncRelayCommand(_ => EntferneSonderregelnAsync());

        /// <summary>
        /// Verteilt die Regeln auf freie, gewoehnliche, ungespielte Zellen. Liegen schon welche, wird gefragt -
        /// ja ersetzt sie alle, nein laesst alles, wie es ist.
        /// </summary>
        internal async Task VerteileSonderregelnAsync()
        {
            if (Game == null)
                return;

            var zellen = Game.GameGridCoordinates;
            var belegt = Sonderregelraster.Belegt(zellen);

            if (belegt > 0 && !UserPrompt.Confirm(
                    $"Im Raster liegen schon {belegt} Sonderregeln. Alle entfernen und {SonderregelAnzahl} neu verteilen?",
                    SonderregelCaption))
                return;

            var geaendert = Sonderregelraster.Entferne(zellen).ToHashSet();
            var auftrag = new Verteilauftrag(SonderregelAnzahl, SonderregelnAngekuendigt,
                SpecialRuleProfiles.All.Select(p => p.Rule).ToList(), Zufall);
            var neu = Sonderregelraster.Verteile(zellen, auftrag);
            geaendert.UnionWith(neu);

            await SpeichereSonderregelnAsync(geaendert);

            if (neu.Count < SonderregelAnzahl)
                UserPrompt.Inform(
                    $"Nur {neu.Count} Zellen kommen in Frage – eine gewöhnliche Frage, kein Minispiel, noch nicht gespielt. "
                    + $"Verteilt sind {neu.Count} Sonderregeln.", SonderregelCaption);
        }

        /// <summary>Nimmt jede Sonderregel vom Raster.</summary>
        internal async Task EntferneSonderregelnAsync()
        {
            if (Game == null || Sonderregelraster.Belegt(Game.GameGridCoordinates) == 0)
                return;

            if (!UserPrompt.Confirm("Alle Sonderregeln vom Raster nehmen?", SonderregelCaption))
                return;

            await SpeichereSonderregelnAsync(Sonderregelraster.Entferne(Game.GameGridCoordinates));
        }

        /// <summary>Beim Speichern: keine Regel auf einem Minispiel oder einer leeren Zelle - und es wird gesagt.</summary>
        private void SonderregelnBereinigen(IEnumerable<GameGridCoordinate> zellen)
        {
            var entfernt = zellen.Where(Sonderregelraster.Bereinige).ToList();

            if (entfernt.Count > 0)
                UserPrompt.Inform(
                    $"{entfernt.Count} Sonderregel(n) entfernt: Minispiele und leere Zellen tragen keine Sonderregel.",
                    SonderregelCaption);
        }

        private async Task SpeichereSonderregelnAsync(IReadOnlyCollection<GameGridCoordinate> zellen)
        {
            if (zellen.Count > 0)
            {
                await RunGuardedAsync(async () =>
                {
                    using var ctrl = new GameGridCoordinatesController();

                    await ctrl.UpsertAsync(zellen);
                    await ctrl.SaveChangesAsync();
                });
            }

            foreach (var zelle in GameGridVMs.CellVMs)
                zelle.RefreshFromModel();
        }
    }
}
