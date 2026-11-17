namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Wie ein Minispiel im Teamspiel je Team gewertet wird (M37 T1, F-M37-11): eine Einstellung je Spiel,
    /// Vorgabe Durchschnitt. Die Schicht über den Karten baut T8.
    /// <para>
    /// <b>Werte nur anhängen:</b> sie stehen als Zahl in der Datenbank. Ein neuer Wert zählt das
    /// Abgleichformat hoch (<c>SyncSnapshot.CurrentFormatVersion</c>).
    /// </para>
    /// </summary>
    public enum TeamMinigameScoring
    {
        /// <summary>Durchschnitt der Einzelvorschläge des Teams - ein großes Team gewinnt nicht durch seine Größe.</summary>
        Average = 0,

        /// <summary>Summe der Einzelvorschläge des Teams.</summary>
        Sum = 1,
    }
}
