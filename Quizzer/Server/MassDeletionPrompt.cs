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
        /// Freigabe für das Genannte. Bei Nein bietet <paramref name="discard"/> den Ausweg an (V17); liefert er
        /// <c>null</c>, bleibt es bei der Sperre.
        /// </summary>
        internal static async Task<T> RunAskingAsync<T>(Func<MassDeletion?, Task<T>> run, string caption, Func<MassDeletion, Task<T?>>? discard = null)
        {
            MassDeletion freigabe;

            try
            {
                return await run(null);
            }
            catch (SyncSafetyException ex)
            {
                if (!UserPrompt.Confirm(Question(ex.MassDeletion), caption))
                {
                    if (discard != null && await discard(ex.MassDeletion) is { } verworfen)
                        return verworfen;

                    throw;
                }

                freigabe = ex.MassDeletion;
            }

            return await run(freigabe);
        }

        /// <summary>
        /// „Jetzt abgleichen": der Hintergrund hat die Sperre nur gemeldet. Fragen, und bei Ja genau das Genannte
        /// senden; bei Nein bietet <paramref name="discard"/> den Ausweg an (V17). <c>true</c>, wenn gesendet oder
        /// verworfen wurde.
        /// </summary>
        internal static async Task<bool> ApprovePendingAsync(MassDeletion? gemeldet, Func<MassDeletion, Task> send, string caption,
            Func<MassDeletion, Task<bool>>? discard = null)
        {
            if (gemeldet == null)
                return false;

            if (!UserPrompt.Confirm(Question(gemeldet), caption))
                return discard != null && await discard(gemeldet);

            await send(gemeldet);
            return true;
        }

        /// <summary>V17: die zweite Frage nach „Nein" - was zurückkommt, und dass nichts hinausgeht.</summary>
        internal static string DiscardQuestion(MassDeletionDiscard verwerfen)
        {
            var zurueck = verwerfen.Restored.Keys.Count == 0
                ? "Am Server gibt es diese Einträge auch nicht mehr, zurück kommt nichts."
                : $"Zurück kommen {verwerfen.Restored.Description}"
                  + (verwerfen.Accompanying > 0 ? $" samt {verwerfen.Accompanying} weiteren Einträgen, die dazugehören (Schritte, Zellen, Ergebnisse, Kategorien)" : "")
                  + (verwerfen.Restored.Keys.Count < verwerfen.Deletion.Keys.Count ? "; die übrigen gibt es auch am Server nicht mehr." : ".");

            var offen = verwerfen.Kept switch
            {
                0 => "",
                1 => " Deine eine andere offene Änderung bleibt erhalten und wird danach wie gewohnt übertragen.",
                _ => $" Deine {verwerfen.Kept} anderen offenen Änderungen bleiben erhalten und werden danach wie gewohnt übertragen.",
            };

            return "Stattdessen die lokalen Löschungen verwerfen und vom Server laden?"
                   + Environment.NewLine + Environment.NewLine
                   + zurueck + " Hinausgeschickt wird dabei nichts, am Server bleibt alles, wie es ist." + offen
                   + Environment.NewLine + Environment.NewLine
                   + "Mit „Nein“ bleibt die Löschung hier liegen, und der Abgleich bleibt angehalten.";
        }

        /// <summary>
        /// V17 (umkehrbare Annahme, F-M37-15): nach „Nein" anbieten, die aufgehaltenen Löschungen zu verwerfen und den
        /// Serverstand zu holen. <c>default</c>, wenn auch das abgelehnt wird.
        /// </summary>
        internal static async Task<T?> OfferDiscardAsync<T>(Func<Task<MassDeletionDiscard>> prepare, Func<MassDeletionDiscard, Task<T>> apply, string caption)
        {
            var vorbereitet = await prepare();
            return UserPrompt.Confirm(DiscardQuestion(vorbereitet), caption) ? await apply(vorbereitet) : default;
        }

        /// <summary>Der Ausweg gegen den laufenden Abgleich selbst - „Jetzt abgleichen" und „Vom Server neu laden".</summary>
        internal static Task<PullResult?> OfferDiscardAsync(ClientSyncService sync, MassDeletion gesperrt, string caption)
            => OfferDiscardAsync(() => Task.Run(() => sync.PrepareDiscardAsync(gesperrt)), v => Task.Run(() => sync.DiscardAsync(v)), caption);
    }
}
