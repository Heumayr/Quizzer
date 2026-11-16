namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Die Art eines Jokers (M35 C7, Entwurf F-M35-12). Der Spielleiter vergibt ihn, der Spieler löst
    /// ihn später für eine Frage ein.
    /// <para>
    /// <b>Werte nur anhaengen:</b> sie stehen als Zahl in der Datenbank. Ein neuer Wert zaehlt das
    /// Abgleichformat hoch (<c>SyncSnapshot.CurrentFormatVersion</c>).
    /// </para>
    /// </summary>
    public enum JokerKind
    {
        /// <summary>Doppelt: das eigene Ergebnis dieser Frage zählt doppelt, Plus wie Minus.</summary>
        Double = 1,

        /// <summary>Schutz: das eigene Minus dieser Frage entfällt.</summary>
        Shield = 2,

        /// <summary>Sperre: ein gewählter Mitspieler darf diese Frage nicht beantworten.</summary>
        Block = 3,
    }
}
