using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Spielerwahl im Fragefenster (M35 C2): beim Duell und bei Teilen oder Nehmen waehlt der Ausloeser am
    /// Telefon. Fehlt er, waehlt hier der Spielleiter; er kann das Paar auch jederzeit ueberschreiben (E1).
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        private const string SpielerwahlCaption = "Wer spielt";

        /// <summary>Die Mitspieler zur Auswahl - dieselbe Liste je Frage, sonst verloere die Auswahlliste ihre Wahl.</summary>
        public IReadOnlyList<Player> PaarSpieler { get; private set; } = [];

        /// <summary>Wer waehlt - vorbelegt mit dem Ausloeser.</summary>
        public Player? PaarWaehler { get; set; }

        /// <summary>Wen der Spielleiter dazu festlegt; leer heisst: der Ausloeser waehlt am Telefon.</summary>
        public Player? PaarPartner { get; set; }

        private MinigameProfile? SpielerwahlArt
            => Minispiel is { } frage && MinigameProfiles.TryFor(frage.Kind, out var art) && art.ChoosesPartner ? art : null;

        public Visibility MinispielPaarVisibility
            => MinispielSteuerungVisibility == Visibility.Visible && SpielerwahlArt != null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>„Gegner" bzw. „Partner (leer: wählt der Auslöser am Telefon)".</summary>
        public string PaarPartnerBeschriftung => $"{SpielerwahlArt?.PartnerLabel ?? "Partner"} (leer: wählt der Auslöser)";

        private AsyncRelayCommand? setMinigamePairCommand;

        public ICommand SetMinigamePairCommand
            => setMinigamePairCommand ??= new AsyncRelayCommand(_ => SetzePaarAsync());

        /// <summary>Der Ausloeser, wenn er im Spiel mitspielt - sonst waehlt der Spielleiter.</summary>
        private Guid? Ausloeser
            => Coordinate?.Game is { } spiel && spiel.Players.Any(p => p.Id == spiel.CurrentChoosingPlayerId)
                ? spiel.CurrentChoosingPlayerId
                : null;

        /// <summary>Stellt die Auswahl auf die neue Frage ein - aus <c>PrepareMinigame</c>.</summary>
        private void BereitePaarVor()
        {
            PaarSpieler = Coordinate?.Game?.Players.Where(p => p != null).ToList() ?? [];
            PaarWaehler = PaarSpieler.FirstOrDefault(p => p.Id == Ausloeser);
            PaarPartner = null;

            OnPropertyChanged(nameof(PaarSpieler));
            OnPropertyChanged(nameof(PaarWaehler));
            OnPropertyChanged(nameof(PaarPartner));
            OnPropertyChanged(nameof(MinispielPaarVisibility));
            OnPropertyChanged(nameof(PaarPartnerBeschriftung));
        }

        internal async Task SetzePaarAsync()
        {
            if (BuzzerControlsViewModel?.MinigameState == null || SpielerwahlArt == null)
                return;

            if (PaarWaehler == null || PaarWaehler.Id == PaarPartner?.Id)
            {
                UserPrompt.Inform("Bitte wählen, wer fordert – und als Gegenüber jemand anderen.", SpielerwahlCaption);
                return;
            }

            if (!await BuzzerControlsViewModel.SetMinigamePairAsync(PaarWaehler.Id, PaarPartner?.Id))
            {
                UserPrompt.Inform("Das Paar ließ sich nicht festlegen – beide müssen in diesem Spiel mitspielen.", SpielerwahlCaption);
                return;
            }

            if (BuzzerControlsViewModel.MinigameState.Phase == MinigamePhase.Running)
                ZeigeLaeuft();
        }

        /// <summary>Ohne jemanden, der waehlt, liefe die Runde ins Leere - dann sagt es das Fenster.</summary>
        private bool PaarBereit()
        {
            if (SpielerwahlArt == null || BuzzerControlsViewModel?.MinigameState?.Rules is not IMinigamePairRules paar
                || paar.ChooserId != null)
                return true;

            UserPrompt.Inform("Wer das Feld aufgedeckt hat, ist nicht bekannt. Bitte unter „Minispiel“ wählen, wer "
                              + "fordert, und „Paar festlegen“ drücken.", SpielerwahlCaption);
            return false;
        }
    }
}
