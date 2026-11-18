using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.GameViews.Teamregeln;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Die Beratungszeit der Teamberatung (M37 T5, R5-5): sie läuft ab dem ersten Inhaltsschritt - auf dem Startbildschirm zeigt
    /// der Beamer nur die Fragenart, die Frage ist noch nicht lesbar. Sie endet früh, wenn alle Sprecher abgegeben haben. Ist
    /// sie um, nehmen die Telefone nichts mehr an - wie beim Countdown von Hand.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Die Sprecher stehen, die Uhr wartet darauf, dass der Beamer die Frage zeigt.</summary>
        private bool beratungszeitFaellig;

        /// <summary>Ob die laufende Uhr die Beratungszeit ist - nur sie endet mit der letzten Abgabe der Sprecher.</summary>
        private bool beratungszeitLaeuft;

        private bool HatBeratungszeit => Teamregel is TeamberatungAblauf && Coordinate?.Game is { TeamCouncilSeconds: > 0 };

        /// <summary>Die Sprecher stehen fest: die Uhr läuft, sobald die Frage lesbar ist - steht sie schon, sofort.</summary>
        private void BeratungszeitVormerken()
        {
            beratungszeitFaellig = HatBeratungszeit;
            BeratungszeitWennLesbar();
        }

        /// <summary>Bei jedem Schrittwechsel: einmal je Runde, sobald ein Schritt nach dem Startbildschirm am Beamer steht.</summary>
        private void BeratungszeitWennLesbar()
        {
            if (!beratungszeitFaellig || CurrentStep is null or { IsStart: true })
                return;

            beratungszeitFaellig = false;
            StarteBeratungszeit();
        }

        private void StarteBeratungszeit()
        {
            if (!HatBeratungszeit || Coordinate?.Game is not { } spiel)
                return;

            var buzzer = BuzzerControlsViewModel;
            beratungszeitLaeuft = true;
            Countdown?.Starte(Math.Min(spiel.TeamCouncilSeconds, TeamGrenzen.MaxCouncilSeconds), () =>
            {
                beratungszeitLaeuft = false;
                _ = buzzer?.SperreWegenZeitAsync();
            });
        }

        /// <summary>P2-2: „Runde zurücksetzen" hebt am Hub die Zeitsperre auf - die Beratungszeit läuft neu, sobald die Frage lesbar ist.</summary>
        private void BeratungszeitNachZuruecksetzen()
        {
            if (teamregelVorbereitet)
                BeratungszeitVormerken();
        }

        /// <summary>Alle Sprecher haben abgegeben: die Beratung ist vorbei, die Uhr hält an (ohne Zeitsperre - die Runde ist zu).</summary>
        private void BeratungszeitAbgegeben()
        {
            beratungszeitFaellig = false;

            if (!beratungszeitLaeuft)
                return;

            beratungszeitLaeuft = false;
            Countdown?.Stoppe();
        }
    }
}
