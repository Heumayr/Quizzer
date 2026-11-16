using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Quizzer.Logic.Context
{
    /// <summary>
    /// Eine Transaktion bis zum nächsten Speichern (M37 D2, V05). Ein Aufräumschritt per
    /// <c>ExecuteDeleteAsync</c> läuft sofort gegen die Datenbank, nicht in <c>SaveChanges</c> - ohne
    /// diese Klammer stünde er fest, auch wenn das Speichern danach scheitert.
    /// </summary>
    internal partial class DataContext
    {
        private IDbContextTransaction? transactionUntilSave;

        /// <summary>
        /// Öffnet eine Transaktion, die das nächste Speichern festschreibt oder - scheitert es - zurückrollt.
        /// Läuft schon eine (etwa die des Abgleichs), gehört der Schritt zu ihr. Wird nie gespeichert, rollt
        /// das Schließen des Kontexts zurück.
        /// </summary>
        internal async Task EnsureTransactionUntilSaveAsync()
        {
            if (Database.CurrentTransaction != null)
                return;

            transactionUntilSave = await Database.BeginTransactionAsync().ConfigureAwait(false);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            var offen = transactionUntilSave;

            if (offen == null)
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);

            transactionUntilSave = null;

            try
            {
                var anzahl = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
                await offen.CommitAsync(cancellationToken).ConfigureAwait(false);
                return anzahl;
            }
            finally
            {
                // Ohne Commit rollt das Freigeben zurück - samt allem, was vorher aufgeräumt wurde.
                await offen.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
