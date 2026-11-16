using Quizzer.Base;
using Quizzer.Client;

namespace Quizzer.Server
{
    /// <summary>
    /// V06 (M37 D3): hält die Massenlösch-Sperre einen ausdrücklichen Abgleich an, fragt das Programm nach. Nur bei Ja
    /// geht genau die genannte Löschung hinaus. Der Hintergrund-Abgleich fragt nie und gibt nie frei.
    /// </summary>
    internal static class MassDeletionPrompt
    {
        internal static string Question(MassDeletion gesperrt)
            => $"Beim Abgleich würden am Server {gesperrt.Description} gelöscht - mehr als die Hälfte der "
               + $"{gesperrt.Known} Fragen und Spiele, die dein Konto kennt."
               + Environment.NewLine + Environment.NewLine
               + "Hast du sie bewusst gelöscht, etwa eine ganze Kategorie? Dann werden genau diese am Server gelöscht. "
               + "Mit „Nein“ wird nichts übertragen, und am Server bleibt alles stehen.";

        /// <summary>
        /// Läuft <paramref name="run"/> ohne Freigabe in die Sperre, wird gefragt; bei Ja läuft es genau einmal mit der
        /// Freigabe für das Genannte. Bei Nein bleibt es bei der Sperre.
        /// </summary>
        internal static async Task<T> RunAskingAsync<T>(Func<MassDeletion?, Task<T>> run, string caption)
        {
            MassDeletion freigabe;

            try
            {
                return await run(null);
            }
            catch (SyncSafetyException ex)
            {
                if (!UserPrompt.Confirm(Question(ex.MassDeletion), caption))
                    throw;

                freigabe = ex.MassDeletion;
            }

            return await run(freigabe);
        }

        /// <summary>
        /// „Jetzt abgleichen": der Hintergrund hat die Sperre nur gemeldet. Fragen, und bei Ja genau das Genannte
        /// senden. <c>true</c>, wenn gesendet wurde.
        /// </summary>
        internal static async Task<bool> ApprovePendingAsync(MassDeletion? gemeldet, Func<MassDeletion, Task> send, string caption)
        {
            if (gemeldet == null || !UserPrompt.Confirm(Question(gemeldet), caption))
                return false;

            await send(gemeldet);
            return true;
        }
    }
}
