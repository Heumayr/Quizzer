namespace Quizzer.Client
{
    /// <summary>
    /// V17: ein vorbereitetes Verwerfen einer aufgehaltenen Massenlöschung - die Zahlen für die Rückfrage. Angewandt wird
    /// es mit <see cref="ClientSyncService.DiscardAsync"/>, das den Serverstand dafür frisch holt.
    /// </summary>
    /// <param name="Deletion">Was die Sperre aufgehalten hat.</param>
    /// <param name="Restored">Die Fragen und Spiele, die zurückkommen. Was auch am Server fehlt, kommt nicht.</param>
    /// <param name="Accompanying">Lokal Gelöschtes, das mitkommt, weil es daran hängt (Schritte, Zellen, Kategorie …).</param>
    /// <param name="Kept">Andere lokale Änderungen: sie bleiben offen und gehen danach wie gewohnt hinaus.</param>
    public sealed record MassDeletionDiscard(MassDeletion Deletion, MassDeletion Restored, int Accompanying, int Kept);
}
