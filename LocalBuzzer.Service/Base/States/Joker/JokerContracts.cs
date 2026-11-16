using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;

namespace LocalBuzzer.Service.Base.States.Joker
{
    /// <summary>
    /// Eine Vorphase, die weder Art noch Sonderregel ist (M35 C7b). Nur zur Laufzeit, nie in der Datenbank oder im
    /// Abgleich. Die Zahl geht ans Telefon (<c>constants.js</c>) - nur anhaengen.
    /// </summary>
    public enum MinigamePrePhase
    {
        None = 0,

        /// <summary>Die Joker-Runde vor einer gewoehnlichen Frage.</summary>
        Joker = 1,
    }

    /// <summary>Ein offener Joker, den ein Spieler in der Runde einsetzen kann - die WPF-Seite liest ihn aus der Datenbank.</summary>
    public sealed record JokerOffer(Guid Id, JokerKind Kind);

    /// <summary>Was das Telefon abgibt. Feldnamen = <c>joker.js</c>.</summary>
    public sealed class JokerPayload
    {
        /// <summary>Der eigene Joker; leer heisst „keinen Joker".</summary>
        public Guid? JokerId { get; set; }

        /// <summary>Bei der Sperre: wen sie trifft; sonst leer.</summary>
        public Guid? Target { get; set; }
    }

    /// <summary>Eine angenommene Wahl. Der Spieler kommt aus der Verbindung, nie aus der Nachricht.</summary>
    /// <param name="PlayerId">Wer gewaehlt hat.</param>
    /// <param name="JokerId">Der eingesetzte Joker; <c>null</c>: keiner.</param>
    /// <param name="Kind">Seine Art; <c>null</c>: keiner.</param>
    /// <param name="TargetId">Bei der Sperre: wen sie trifft.</param>
    public sealed record JokerChoice(Guid PlayerId, Guid? JokerId, JokerKind? Kind, Guid? TargetId);

    /// <summary>Ein eigener Joker als Taste am Telefon.</summary>
    public sealed class JokerOptionInfo
    {
        public Guid Id { get; set; }

        /// <summary>„Doppelt".</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Der Satz, was er bewirkt.</summary>
        public string Effect { get; set; } = string.Empty;

        /// <summary>Die Sperre braucht ein Ziel.</summary>
        public bool NeedsTarget { get; set; }
    }

    /// <summary>Wer welchen Joker eingesetzt hat - erst nach dem Schliessen, dann fuer alle.</summary>
    public sealed class JokerResultInfo
    {
        public string Player { get; set; } = string.Empty;

        public string Joker { get; set; } = string.Empty;

        /// <summary>Bei der Sperre: wen sie trifft.</summary>
        public string? Target { get; set; }
    }

    /// <summary>Was ein Telefon in der Joker-Runde sieht. Feldnamen = <c>joker.js</c>.</summary>
    public sealed class JokerClientInfo
    {
        /// <summary>Nur die eigenen offenen Joker - nie fremde.</summary>
        public List<JokerOptionInfo> Options { get; set; } = new();

        /// <summary>Wen eine Sperre treffen kann - die uebrigen Mitspieler; nur fuer den, der eine Sperre haelt.</summary>
        public List<MinigamePlayerOption> Targets { get; set; } = new();

        /// <summary>Die eigene Wahl, sobald abgegeben - auch nach dem Wiederverbinden, nie eine fremde.</summary>
        public Guid? OwnJokerId { get; set; }

        public Guid? OwnTarget { get; set; }

        /// <summary>Wer welchen Joker eingesetzt hat - leer, solange die Runde laeuft.</summary>
        public List<JokerResultInfo> Results { get; set; } = new();

        /// <summary>Wer diesen Spieler gesperrt hat - erst nach dem Schliessen, und nur fuer ihn.</summary>
        public string? BlockedBy { get; set; }
    }
}
