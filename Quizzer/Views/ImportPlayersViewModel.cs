using Quizzer.Base;
using Quizzer.Logic.Controller.TypedControllers;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Quizzer.Views
{
    /// <summary>
    /// Wer beim importierten Spiel mitspielt.
    /// <para>
    /// <b>Nutzerwort vom 2026-09-06:</b> „die spieler koennen beim import gewaehlt werden (oder
    /// auch keine)". Deshalb gibt es drei Ausgaenge, nicht zwei: uebernehmen, ohne Mitspieler,
    /// abbrechen. Die letzten beiden sind ausdruecklich verschieden.
    /// </para>
    /// </summary>
    public class ImportPlayersViewModel : ViewModelBase
    {
        /// <summary>Ein Mitspieler zur Auswahl.</summary>
        public sealed class Eintrag : ViewModelBase
        {
            private bool gewaehlt;

            public Guid Id { get; init; }

            public string Anzeigename { get; init; } = string.Empty;

            public bool Gewaehlt
            {
                get => gewaehlt;
                set { gewaehlt = value; OnPropertyChanged(); }
            }

            protected override Task OnloadAsync() => Task.CompletedTask;

            public override Task VMSaveAsync() => Task.CompletedTask;
        }

        public ObservableCollection<Eintrag> Spieler { get; } = new();

        /// <summary>Das Ergebnis. <c>null</c> heisst: abgebrochen.</summary>
        public IReadOnlyList<Guid>? Ergebnis { get; private set; }

        protected override async Task OnloadAsync()
        {
            using var ctrl = new PlayersController();

            foreach (var spieler in (await ctrl.GetAllAsync())
                         .OrderBy(p => p.CalculatedDisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                Spieler.Add(new Eintrag
                {
                    Id = spieler.Id,
                    Anzeigename = spieler.CalculatedDisplayName,
                });
            }
        }

        public override Task VMSaveAsync() => Task.CompletedTask;

        private RelayCommand? acceptCommand;

        public ICommand AcceptCommand => acceptCommand ??= new RelayCommand(_ =>
        {
            Ergebnis = Spieler.Where(s => s.Gewaehlt).Select(s => s.Id).ToList();
            Window?.Close();
        });

        private RelayCommand? noneCommand;

        public ICommand NoneCommand => noneCommand ??= new RelayCommand(_ =>
        {
            Ergebnis = new List<Guid>();
            Window?.Close();
        });

        private RelayCommand? cancelCommand;

        public ICommand CancelCommand => cancelCommand ??= new RelayCommand(_ =>
        {
            Ergebnis = null;
            Window?.Close();
        });
    }
}
