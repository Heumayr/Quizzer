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
        /// <summary>
        /// Hochzaehlen, sobald eine Spalte oder ein Typ in den Abgleich kommt, den ein aelteres WPF nicht kennt -
        /// sonst setzt dessen Push still den Standardwert ein. Der Versionsriegel (M36 A4) haelt es dann an.
        /// 2: Game.AllowNegativeScore (M35 B1).
        /// 3: Fragetyp Minispiel - Zeilentyp MinigameQuestion (M35 B2); ein altes WPF kennt ihn nicht.
        /// 4: Minispiel-Art Reaktion (M35 B3) - neue Werte in Kind und BuzzerControlsLayout. Keine Spalte,
        ///    aber ein altes WPF haelt die Frage fuer verbogen, und seine Reparatur schriebe Tastenwahl zurueck.
        /// 5: Minispiel-Arten Tippsprint und Stoppuhr (M35 C1) - derselbe Grund wie bei 4.
        /// 6: Minispiel-Arten Duell, Raeuber und Waechter, Teilen oder Nehmen (M35 C2) - derselbe Grund wie bei 4.
        /// 7: Sonderregel an der Zelle - Spalten GameGridCoordinate.SpecialRule und SpecialRuleAnnounced (M35 C3).
        /// 8: Anteil und Spielweise der Sonderregel - Spalten SpecialRuleShare und SpecialRuleVariant (M35 C3b).
        /// 9: Sonderregel Auktion (M35 C3b) - ein altes WPF kennt den Wert nicht und spielte die Zelle ohne Regel.
        /// 10: Sonderregel Wette (M35 C3b) - derselbe Grund wie bei 9.
        /// 11: Sonderregel Experte (M35 C3b) - derselbe Grund wie bei 9.
        /// 12: Sonderregel Allianz (M35 C3b) - derselbe Grund wie bei 9.
        /// 13: Sonderregel Kopfgeld (M35 C3b) - derselbe Grund wie bei 9.
        /// 14: Minispiel-Art Zocker (M35 C4) - derselbe Grund wie bei 4.
        /// 15: Minispiel-Art Gluecksrad (M35 C4) - derselbe Grund wie bei 4.
        /// 16: Minispiel-Art Kopfrechnen (M35 C5) - derselbe Grund wie bei 4.
        /// 17: Minispiel-Art Memory (M35 C5) - derselbe Grund wie bei 4.
        /// 18: Minispiel-Art Blitzrunde (M35 C5) - derselbe Grund wie bei 4.
        /// 19: Minispiel-Art Buchstabensalat (M35 C5) - derselbe Grund wie bei 4.
        /// 20: Minispiel-Art Erklaerbaer (M35 C6) - derselbe Grund wie bei 4.
        /// 21: Minispiel-Art Bluff (M35 C6) - derselbe Grund wie bei 4.
        /// 22: Minispiel-Art Heisse Kartoffel (M35 C6) - derselbe Grund wie bei 4.
        /// 23: Joker (M35 C7) - neuer Zeilentyp PlayerJoker, den ein altes WPF nicht kennt.
        /// 24: Bestenliste (M35 C9) - Spalten QuestionResult.RecordValue, RecordedAt und Steals; ein altes WPF
        ///    setzte beim Push still null und 0 ein.
        /// 25: Punkteverlauf (M37 E06) - Spalte GameGridCoordinate.PlayedAt; ein altes WPF setzte beim Push still null ein.
        /// 26: Teams (M37 T1) - neuer Zeilentyp GameTeam, Spalten PlayerXGame.GameTeamId, QuestionResult.GameTeamId und die
        ///    Teameinstellungen am Spiel; ein altes WPF nahm beim Push still jede Zuteilung heraus.
        /// </summary>
        public const int CurrentFormatVersion = 26;

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
