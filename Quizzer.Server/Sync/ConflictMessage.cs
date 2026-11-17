namespace Quizzer.Server.Sync
{
    /// <summary>
    /// Der Text einer Konflikt-Abweisung (V12). Legen zwei Konten dieselbe feste Id an - etwa beim Minispiel-Vorrat -,
    /// hatte der zweite nie einen Stand; „inzwischen geändert" fuehrte ihn in die Irre. Der Client erkennt den Fall
    /// selbst an seinem Manifest (<c>OpenRejection.ErstAnlage</c>), der Text hier steht in der Spalte „Meldung".
    /// </summary>
    internal static class ConflictMessage
    {
        public const string Changed = "Am Server inzwischen geändert.";

        public const string Deleted = "Am Server inzwischen gelöscht.";

        public const string AlreadyCreated = "Am Server schon angelegt – ein anderer Rechner war beim ersten Anlegen schneller. Abholen übernimmt diesen Stand.";

        /// <summary>Ohne Grundlage (<paramref name="baseHash"/> leer) hat der Client die Zeile neu angelegt.</summary>
        public static string ForUpsert(string? baseHash, string? serverHash)
            => serverHash == null ? Deleted : baseHash == null ? AlreadyCreated : Changed;
    }
}
