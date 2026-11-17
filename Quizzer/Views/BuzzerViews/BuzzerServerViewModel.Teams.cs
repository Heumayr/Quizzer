using LocalBuzzer.Service.Base;
using Quizzer.Views.StaticRessources;

namespace Quizzer.Views.BuzzerViews
{
    /// <summary>
    /// Die Teams des geladenen Spiels an den Hub (M37 T4): als unveränderlicher Schnappschuss (<see cref="TeamRoster"/>),
    /// denn der Hub liest die Zuordnung nie aus dem Spielgraphen. Gesetzt beim Öffnen des Spiels, beim Serverstart und nach
    /// dem Umsetzen.
    /// </summary>
    public partial class BuzzerServerViewModel
    {
        /// <summary>Ersetzt die Aufstellung am Hub und schickt jedem Telefon sein Team. Ohne laufenden Server nichts.</summary>
        public async Task SendTeamsAsync()
        {
            try
            {
                if (IsBuzzerServerRunning && _server?.BuzzerController is { } ctrl)
                    await ctrl.SetTeamsAsync(TeamRoster.From(Game));
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        /// <summary>Ein anderes Spiel ist geladen: seine Aufstellung gilt sofort, die Telefone bekommen sie mit dem nächsten Stand.</summary>
        private void ApplyTeams()
        {
            if (IsBuzzerServerRunning && _server?.BuzzerController is { } ctrl)
                ctrl.StateManager.SetTeams(TeamRoster.From(_game));
        }
    }
}
