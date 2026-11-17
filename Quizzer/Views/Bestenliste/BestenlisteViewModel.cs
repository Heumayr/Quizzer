using Quizzer.Base;
using Quizzer.Client;
using Quizzer.DataModels.Transfer.Server;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Server;
using System.Collections.ObjectModel;

namespace Quizzer.Views.Bestenliste
{
    /// <summary>
    /// Die Bestenliste auch ohne Server (M37 E03): im Einzelbetrieb und offline aus der lokalen Datenbank, gerechnet wie am
    /// Server. Online im Servermodus bleibt die Liste des Servers massgeblich.
    /// </summary>
    public sealed class BestenlisteViewModel : ViewModelBase
    {
        internal const string LokalText = "Gerechnet aus allen Spielen auf diesem Rechner.";

        internal const string OfflineText = "Offline – gerechnet aus den Spielen, die dieses Konto zuletzt vom Server geholt hat.";

        internal const string ServerText = "Vom Server, über alle Spiele, die du leitest oder in denen du mitspielst.";

        private string herkunft = "Wird geladen …";

        public ObservableCollection<BestenlisteKachel> Kacheln { get; } = new();

        /// <summary>Woher die Liste kommt - lokal und am Server koennen verschiedene Spiele zaehlen.</summary>
        public string Herkunft
        {
            get => herkunft;
            private set { herkunft = value; OnPropertyChanged(); }
        }

        /// <summary>Vom Server nur im Servermodus und online; ein gesetzter Client allein reicht nicht.</summary>
        internal static bool VomServer => ServerSession.IsActive && !ServerSession.IsOffline && ServerSession.Api != null;

        protected override async Task OnloadAsync()
        {
            var (liste, woher) = await LadeAsync();

            Kacheln.Clear();
            foreach (var kachel in BestenlisteKacheln.Aus(liste))
                Kacheln.Add(kachel);

            Herkunft = woher;
        }

        private static async Task<(Leaderboard Liste, string Woher)> LadeAsync()
        {
            if (VomServer && ServerSession.Api is { } api)
            {
                try
                {
                    return ((await api.GetStatisticsAsync()).Leaderboard, ServerText);
                }
                catch (Exception ex) when (ex is ApiException or ServerUnreachableException)
                {
                    return (await LokalAsync(), $"{ex.Message} {OfflineText}");
                }
            }

            return (await LokalAsync(), ServerSession.IsActive ? OfflineText : LokalText);
        }

        private static async Task<Leaderboard> LokalAsync()
        {
            using var ctrl = new QuestionResultsController();
            return await ctrl.BestenlisteAsync();
        }

        public override Task VMSaveAsync() => Task.CompletedTask;
    }
}
