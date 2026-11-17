using Quizzer.DataModels.Questions.Minispiele;
using System.Windows;

namespace Quizzer.Views.GameViews.Sub
{
    /// <summary>
    /// Tauziehen auf der Spielerkarte (M37 T9): der eigene Stand und der des Teams im Satz, der Vorschlag ist der Anteil des
    /// Siegerteams. Gerechnet wird über alle Karten im <c>PlayersResultViewModel</c>, hier nur übernommen.
    /// </summary>
    public partial class PlayerResultContext
    {
        /// <summary>Kein eigener Vorschlag - nur der Teamanteil (E1); was der Spielleiter danach setzt, bleibt.</summary>
        internal void UebernimmTauziehen(string satz, Punktevorschlag? anteil)
        {
            istZuordnung = false;
            Reihenfolge = null;

            ZeigeMehrheit([], Visibility.Collapsed, satz);
            UebernimmTeamanteil(anteil);
        }
    }
}
