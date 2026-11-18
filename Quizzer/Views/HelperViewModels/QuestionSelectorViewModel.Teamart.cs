using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;

namespace Quizzer.Views.HelperViewModels
{
    /// <summary>
    /// Minispiel-Arten im Auswahldialog (M37 P1, V23): wie die Sonderregel-Auswahl steht nur zur Wahl, was zum Spiel mit oder
    /// ohne Teams passt. Was schon auf der Zelle liegt, bleibt und wird benannt - der Spielstart meldet es weiterhin.
    /// </summary>
    public partial class QuestionSelectorViewModel
    {
        /// <summary>Ob die Frage im Spiel gespielt wird; nur Minispiele mit bekannter Art hängen am Teamspiel.</summary>
        private bool PasstZumSpiel(QuestionBase frage)
            => frage is not MinigameQuestion minispiel || !MinigameProfiles.TryFor(minispiel.Kind, out var art) || art.PasstZu(Teamspiel);

        /// <summary>Warum das Minispiel auf dieser Zelle nicht gespielt wird - leer, wenn es passt.</summary>
        public string TeamartHinweis
            => Coordinate?.QuestionBase is MinigameQuestion minispiel && !PasstZumSpiel(minispiel) && MinigameProfiles.TryFor(minispiel.Kind, out var art)
                ? Teamspiel
                    ? $"„{art.DisplayName}“ wird im Teamspiel nicht gespielt – der Spielstart meldet es."
                    : $"„{art.DisplayName}“ gibt es nur im Teamspiel – der Spielstart meldet es. „Teamspiel“ einschalten oder ein anderes Minispiel wählen."
                : string.Empty;

        public Visibility TeamartHinweisVisibility => TeamartHinweis.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

        private void TeamartGeaendert()
        {
            OnPropertyChanged(nameof(TeamartHinweis));
            OnPropertyChanged(nameof(TeamartHinweisVisibility));
        }
    }
}
