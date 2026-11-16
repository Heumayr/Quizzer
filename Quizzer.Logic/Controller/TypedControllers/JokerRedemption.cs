namespace Quizzer.Logic.Controller.TypedControllers
{
    /// <summary>Ein Joker soll für eine Zelle eingelöst werden (M35 C7b).</summary>
    /// <param name="JokerId">Der Joker - kommt aus dem WLAN, also wird alles an ihm geprüft.</param>
    /// <param name="PlayerId">Wer einlöst - aus der Verbindung des Telefons, nie aus der Nachricht.</param>
    /// <param name="GameId">Das laufende Spiel.</param>
    /// <param name="CellId">Die Zelle, deren Frage gerade aufgeht.</param>
    /// <param name="TargetPlayerId">Bei der Sperre: wen sie trifft; sonst leer.</param>
    public sealed record JokerRedemption(Guid JokerId, Guid PlayerId, Guid GameId, Guid CellId, Guid? TargetPlayerId);

    /// <summary>Wie <see cref="PlayerJokersController.RedeemAsync"/> entschieden hat - nur <see cref="Redeemed"/> schreibt.</summary>
    public enum JokerRedeemOutcome
    {
        Redeemed = 0,

        /// <summary>Unbekannte Joker-Id.</summary>
        NotFound = 1,

        /// <summary>Der Joker gehört einem anderen Spieler.</summary>
        NotOwner = 2,

        /// <summary>Der Joker gilt in einem anderen Spiel.</summary>
        WrongGame = 3,

        /// <summary>Der Joker ist schon eingelöst.</summary>
        AlreadyRedeemed = 4,

        /// <summary>Die Zelle gehört nicht zu diesem Spiel.</summary>
        CellNotInGame = 5,

        /// <summary>Der Spieler hat für diese Zelle schon einen Joker eingelöst.</summary>
        SecondJokerForCell = 6,

        /// <summary>Sperre ohne Ziel, gegen sich selbst oder gegen einen Nicht-Mitspieler; oder ein Ziel bei Doppelt/Schutz.</summary>
        TargetInvalid = 7,
    }
}
