using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Wo ein Minispiel steht. Die Zahl geht ans Telefon (<c>constants.js</c>) - nur anhaengen.</summary>
    public enum MinigamePhase
    {
        /// <summary>Vor der ersten Runde: das Telefon wartet auf den Spielleiter.</summary>
        Waiting = 0,

        /// <summary>Eine Runde laeuft - das Modul der Art ist dran.</summary>
        Running = 1,

        /// <summary>Die Runde ist zu, die naechste steht noch aus.</summary>
        RoundOver = 2,

        /// <summary>Alle Runden gespielt oder abgebrochen.</summary>
        Finished = 3,
    }

    /// <summary>
    /// Was ein Telefon ueber den Hub-Eingang <c>SpielAktion</c> schickt (M35 B3). Wer abgibt, sagt die
    /// Verbindung, nie die Nachricht.
    /// </summary>
    public sealed class MinigameAction
    {
        /// <summary>Die Art, fuer die das Telefon abgibt - eine andere als die laufende wird verworfen.</summary>
        public MinigameKind Kind { get; set; }

        /// <summary>Der Durchgang, in dem das Telefon die Runde gesehen hat - ein alter wird verworfen.</summary>
        public int Durchgang { get; set; }

        /// <summary>Die Sonderregel, deren Vorphase das Telefon sah (M35 C3) - eine andere als die laufende wird verworfen.</summary>
        public CellSpecialRule SpecialRule { get; set; }

        /// <summary>Die Abgabe selbst; ihr Aufbau haengt an der Art.</summary>
        public JsonElement Payload { get; set; }
    }

    /// <summary>Was der Rahmen am Telefon braucht; <see cref="KindInfo"/> traegt den Teil der Art fuer genau diesen Spieler.</summary>
    public sealed class MinigameClientInfo
    {
        public MinigameKind Kind { get; set; }

        /// <summary>Gesetzt, wenn der Rahmen die Vorphase einer Sonderregel spielt (M35 C3) - dann zaehlt sie, nicht die Art.</summary>
        public CellSpecialRule SpecialRule { get; set; }

        /// <summary>„Reaktion" - die Ueberschrift am Telefon.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Der Regeltext, wie er auch am Beamer steht.</summary>
        public string RulesText { get; set; } = string.Empty;

        public MinigamePhase Phase { get; set; }

        /// <summary>Die laufende oder zuletzt gespielte Runde des Minispiels, 1-basiert; 0 vor der ersten.</summary>
        public int CurrentRound { get; set; }

        public int TotalRounds { get; set; }

        /// <summary>Steigt mit jedem Start und jeder Wiederholung - das Telefon schickt ihn mit.</summary>
        public int Durchgang { get; set; }

        /// <summary>So lange zaehlt das Telefon vor dem Start herunter.</summary>
        public int CountdownSeconds { get; set; }

        /// <summary>Der Spielleiter hat abgebrochen.</summary>
        public bool Aborted { get; set; }

        /// <summary>Dieser Spieler hat in der laufenden Runde schon abgegeben - nach dem Wiederverbinden kein zweiter Versuch.</summary>
        public bool OwnSubmitted { get; set; }

        /// <summary>Was die Art diesem Spieler zeigt - bei der Reaktion <see cref="ReactionClientInfo"/>.</summary>
        public object? KindInfo { get; set; }

        /// <summary>Was ein Zuschauer sieht, wenn die Art nur einige spielen laesst (M35 C2) - sonst <c>null</c>.</summary>
        public string? WatcherText { get; set; }
    }

    /// <summary>Eine angenommene Abgabe, gemeldet an die Spielleiterseite.</summary>
    /// <param name="Player">Wer abgegeben hat - aus der Verbindung.</param>
    /// <param name="Round">In welcher Runde des Minispiels.</param>
    /// <param name="Value">Was die Art daraus gemacht hat, bei der Reaktion ein <c>ReactionAttempt</c>.</param>
    public sealed record MinigameSubmission(Player Player, int Round, object Value);

    /// <summary>Eine Runde ist zu (oder das Minispiel abgebrochen).</summary>
    /// <param name="Round">Die Runde, 1-basiert.</param>
    /// <param name="TotalRounds">Wie viele es insgesamt sind.</param>
    /// <param name="Finished">Ob es die letzte war.</param>
    /// <param name="Aborted">Ob der Spielleiter abgebrochen hat - dann zaehlt die laufende Runde nicht.</param>
    /// <param name="Submissions">Die Abgaben dieser Runde je Spieler; wer fehlt, hat nichts abgegeben.</param>
    public sealed record MinigameRoundClosed(
        int Round, int TotalRounds, bool Finished, bool Aborted, IReadOnlyDictionary<Guid, object> Submissions);

    /// <summary>Was die Spielleiterseite vor der ersten Runde einstellt.</summary>
    /// <param name="DurationSeconds">Dauer des Tippsprints bzw. Zielzeit der Stoppuhr (M35 C1); sonst ungenutzt.</param>
    /// <param name="TriggerPlayerId">Der Ausloeser - wer das Feld aufgedeckt hat; beim Duell und bei Teilen oder Nehmen waehlt er (M35 C2).</param>
    /// <param name="Variant">Die Spielweise der Art, beim Duell die Disziplin (M35 C2).</param>
    /// <param name="SpecialRule">Statt einer Art die Vorphase dieser Sonderregel (M35 C3).</param>
    /// <param name="FieldPoints">Was „richtig" in der Zelle bringt - beim Einsatz Teil der Obergrenze.</param>
    /// <param name="Balances">Kontostand je Spieler beim Einstellen - beim Einsatz Teil der Obergrenze.</param>
    public sealed record MinigameSetup(
        MinigameKind Kind, string Title, string RulesText, int TotalRounds, int CountdownSeconds = 3, int DurationSeconds = 0,
        Guid? TriggerPlayerId = null, int Variant = 0, CellSpecialRule SpecialRule = CellSpecialRule.None,
        int FieldPoints = 0, IReadOnlyDictionary<Guid, int>? Balances = null);

    /// <summary>
    /// Die Regeln einer Art am Server: was eine gueltige Abgabe ist und was ein Spieler sieht. Der
    /// Rahmen (<see cref="MinigameState"/>) prueft vorher Phase, Durchgang, Sperre und Doppelabgabe.
    /// </summary>
    public interface IMinigameRules
    {
        MinigameKind Kind { get; }

        /// <summary>Eine Runde beginnt (auch eine Wiederholung): ihr Geheimnis wird neu gewuerfelt.</summary>
        void StartRound(int round);

        /// <summary>Prueft und speichert eine Abgabe; <c>null</c> heisst verworfen.</summary>
        object? Accept(Player player, JsonElement payload);

        bool HasSubmitted(Guid playerId);

        /// <summary>Die Abgaben der laufenden Runde.</summary>
        IReadOnlyDictionary<Guid, object> CurrentSubmissions { get; }

        /// <summary>Legt die laufende Runde ab - sie zaehlt.</summary>
        void CloseRound();

        /// <summary>Verwirft die laufende Runde - sie zaehlt nicht (Abbruch).</summary>
        void DiscardRunning();

        /// <summary>Alles vergessen - neue Frage.</summary>
        void Clear();

        /// <summary>Was dieser Spieler in dieser Phase sieht; <c>null</c> fuer den Blick von aussen.</summary>
        object? InfoFor(Player? player, MinigamePhase phase);

        // --- Spiele, in denen nur einige spielen (M35 C2); die Vorgaben passen fuer alle uebrigen ---

        /// <summary>Ob dieser Spieler gerade abgeben darf - beim Duell nur die beiden, vor der Wahl nur der Ausloeser.</summary>
        bool Plays(Guid playerId) => true;

        /// <summary>Ob das Spiel vor der letzten Runde entschieden ist (Best of n) - dann endet es.</summary>
        bool IsDecided => false;

        /// <summary>Was die Zuschauer am Telefon lesen; <c>null</c>: der Rahmen sagt es allgemein.</summary>
        string? WatcherText => null;
    }

    /// <summary>
    /// Eine Abgabe, die nicht zaehlt, sondern die naechste Stufe der Runde oeffnet (M35 C2): der Rahmen
    /// gibt einen neuen Durchgang aus, und jedes Telefon bekommt seinen Zustand.
    /// </summary>
    public interface IMinigameStageChange
    {
    }

    /// <summary>Der Ausloeser hat seinen Gegner oder Partner gewaehlt - geprueft am Server (E4).</summary>
    public sealed record PlayerChosen(Guid ChooserId, Guid ChosenId) : IMinigameStageChange;

    /// <summary>Arten mit Spielerwahl (Duell, Teilen oder Nehmen): der Spielleiter kann das Paar festlegen (E1).</summary>
    public interface IMinigamePairRules : IMinigameRules
    {
        Guid? ChooserId { get; }

        Guid? PartnerId { get; }

        /// <summary>Legt das Paar fest; <c>false</c>, wenn einer kein Mitspieler ist oder beide derselbe.</summary>
        bool SetPair(Guid chooserId, Guid? partnerId);
    }
}
