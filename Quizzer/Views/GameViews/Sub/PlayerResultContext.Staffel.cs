using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Staffel auf der Spielerkarte (M37 T11): die Teile des Teams stehen im Satz, der Vorschlag ist der Anteil des Siegerteams -
    /// derselbe Weg wie beim Tauziehen. Gerechnet wird über alle Karten im <c>PlayersResultViewModel</c>.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Kein eigener Vorschlag - nur der Teamanteil (E1); was der Spielleiter danach setzt, bleibt.</summary>
        internal void UebernimmStaffel(string satz, Punktevorschlag? anteil) => UebernimmTauziehen(satz, anteil);
    }
}
