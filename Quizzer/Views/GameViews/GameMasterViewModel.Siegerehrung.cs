using Quizzer.Base;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.Logic.Statistics;
using Quizzer.Views.GameViews.Sub;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Die Siegerehrung blättern (M37 E3): Sieger, Rückblick auf den Abend und wieder der Sieger. Per Knopf, nicht nach
    /// Zeit - die Siegerehrung kommt ohne Zutun, und wann der Sieger genug gefeiert ist, weiß nur der Spielleiter.
    /// </summary>
    public partial class GameMasterViewModel
    {
        private AsyncRelayCommand? naechsteTafelCommand;

        /// <summary>Erst nach der letzten Frage - vorher gibt es keine Siegerehrung zum Blättern.</summary>
        public ICommand NaechsteTafelCommand => naechsteTafelCommand ??= new AsyncRelayCommand(NaechsteTafelAsync, _ => IsGameFinished);

        private async Task NaechsteTafelAsync(object? _)
        {
            if (Game is not { } spiel || !IsGameFinished)
                return;

            // Bei jedem Verlassen des Siegers neu gerechnet - eine Rücknahme dazwischen ändert den Abend.
            if (StatsContext.Tafel == SiegerehrungTafel.Sieger)
                await LadeRueckblickAsync(spiel.Id);

            var naechste = StatsContext.NaechsteTafel;

            if (naechste == SiegerehrungTafel.Sieger && StatsContext.Tafel == SiegerehrungTafel.Sieger)
            {
                UserPrompt.Inform("Zu diesem Spiel gibt es keinen Rückblick: kein Bestwert, kein Klau, kein eingelöster Joker "
                    + "und keine richtige Antwort.", "Rückblick");
                return;
            }

            StatsContext.ZeigeTafel(naechste);
        }

        private async Task LadeRueckblickAsync(Guid spielId)
        {
            Rueckblick rueckblick;
            using (var ctrl = new QuestionResultsController())
                rueckblick = await ctrl.AbendrueckblickAsync(spielId);

            StatsContext.SetzeRueckblick(rueckblick.IstLeer ? null : new AbendrueckblickViewModel(rueckblick));
        }
    }
}
