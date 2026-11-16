using Quizzer.Base;
using Quizzer.Client;

namespace Quizzer.Views
{
    /// <summary>V06 (M37 D3): die Massenlösch-Sperre bei „Jetzt abgleichen".</summary>
    public partial class MainViewModel
    {
        private const string JetztAbgleichen = "Jetzt abgleichen";

        /// <summary>
        /// Hat der Hintergrund-Abgleich an der Massenlösch-Sperre angehalten, fragt der ausdrückliche Abgleich nach; bei Ja
        /// geht genau die genannte Löschung hinaus. Der Hintergrund selbst gibt nie frei.
        /// </summary>
        private static async Task GesperrteLoeschungErfragenAsync(BackgroundSync background)
        {
            if (Server.ServerSession.Sync is not { } sync)
                return;

            try
            {
                if (await Server.MassDeletionPrompt.ApprovePendingAsync(background.Status.PendingMassDeletion,
                        gesperrt => Task.Run(() => sync.PushApprovedAsync(gesperrt)), JetztAbgleichen))
                    background.Refresh();
            }
            catch (Exception ex) when (ex is ServerUnreachableException or ApiException or SyncSafetyException)
            {
                UserPrompt.Inform(ex.Message, JetztAbgleichen);
            }
        }
    }
}
