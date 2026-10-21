using System.Text.Json;

namespace Quizzer.DataModels.Transfer.Sync
{
    /// <summary>
    /// Eine Datenbankzeile im Abgleich zwischen Server und WPF: Typname, Id und die gemappten
    /// Werte, mehr nicht. Navigationen, <c>RowVersion</c> und gesperrte Felder (etwa
    /// <c>Player.PasswordHash</c>) stehen nie darin.
    /// <para>
    /// <see cref="Hash"/> ist der SHA-256 ueber Typ, Id und Werte in fester Reihenfolge. Server
    /// und Client rechnen ihn gleich (<c>SyncRowConverter</c>), und damit erkennt der Server,
    /// ob eine Zeile seit dem letzten Abholen von jemand anderem geaendert wurde.
    /// </para>
    /// </summary>
    public sealed class SyncRow
    {
        public string Type { get; set; } = string.Empty;

        public Guid Id { get; set; }

        public Dictionary<string, JsonElement> Values { get; set; } = new(StringComparer.Ordinal);

        public string Hash { get; set; } = string.Empty;

        public SyncKey Key => new(Type, Id);
    }

    /// <summary>Typ und Id einer Zeile - der Schluessel im Abgleich.</summary>
    public readonly record struct SyncKey(string Type, Guid Id);

    /// <summary>Alles, was ein Konto sehen darf, auf einmal (M9: „alle daten zu beginn").</summary>
    public sealed class SyncSnapshot
    {
        public const int CurrentFormatVersion = 1;

        public int FormatVersion { get; set; } = CurrentFormatVersion;

        public DateTimeOffset CreatedAt { get; set; }

        public List<SyncRow> Rows { get; set; } = new();
    }

    /// <summary>Eine geaenderte oder neue Zeile samt dem Stand, auf dem die Aenderung beruht.</summary>
    public sealed class SyncUpsert
    {
        public SyncRow Row { get; set; } = new();

        /// <summary>Der Hash beim letzten Abholen; <c>null</c> heisst: die Zeile ist neu.</summary>
        public string? BaseHash { get; set; }
    }

    /// <summary>Eine geloeschte Zeile samt dem Stand, auf dem das Loeschen beruht.</summary>
    public sealed class SyncDelete
    {
        public string Type { get; set; } = string.Empty;

        public Guid Id { get; set; }

        public string? BaseHash { get; set; }

        public SyncKey Key => new(Type, Id);
    }

    public sealed class SyncPushRequest
    {
        public int FormatVersion { get; set; } = SyncSnapshot.CurrentFormatVersion;

        public List<SyncUpsert> Upserts { get; set; } = new();

        public List<SyncDelete> Deletes { get; set; } = new();
    }

    /// <summary>Warum eine Zeile nicht uebernommen wurde.</summary>
    public enum SyncRejectReason
    {
        /// <summary>Das Konto darf diese Zeile nicht schreiben.</summary>
        Forbidden = 0,

        /// <summary>Am Server hat sich die Zeile seit dem letzten Abholen geaendert.</summary>
        Conflict = 1,

        /// <summary>Die Zeile ist fehlerhaft (unbekannter Typ, unbekanntes Feld, falscher Wert).</summary>
        Invalid = 2,
    }

    public sealed class SyncRejected
    {
        public string Type { get; set; } = string.Empty;

        public Guid Id { get; set; }

        public SyncRejectReason Reason { get; set; }

        public string Message { get; set; } = string.Empty;
    }

    public sealed class SyncPushResponse
    {
        /// <summary>Die Hashes der uebernommenen Zeilen, wie der Server sie jetzt haelt.</summary>
        public List<SyncRowHash> Accepted { get; set; } = new();

        /// <summary>Uebernommene Loeschungen.</summary>
        public List<SyncKey> Deleted { get; set; } = new();

        public List<SyncRejected> Rejected { get; set; } = new();
    }

    public sealed class SyncRowHash
    {
        public string Type { get; set; } = string.Empty;

        public Guid Id { get; set; }

        public string Hash { get; set; } = string.Empty;
    }
}
