namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Die Sonderregel einer Rasterzelle (M35 C3): sie umhuellt die gewoehnliche Frage der Zelle mit
    /// einer Vorphase am Telefon. Reihenfolge = Katalog im Plan.
    /// <para>
    /// <b>Werte nur anhaengen:</b> sie stehen als Zahl in der Datenbank und gehen ans Telefon
    /// (<c>constants.js</c>). Ein neuer Wert zaehlt das Abgleichformat hoch.
    /// </para>
    /// </summary>
    public enum CellSpecialRule
    {
        /// <summary>Keine Sonderregel - die Zelle spielt wie immer.</summary>
        None = 0,

        /// <summary>Einsatz: jeder setzt vorher Punkte; richtig gewinnt den Einsatz, falsch verliert ihn.</summary>
        Stake = 1,

        /// <summary>Risiko: wer „doppelt spielen" waehlt, bekommt Plus und Minus verdoppelt.</summary>
        Risk = 2,

        /// <summary>Auktion (M35 C3b): geheime Gebote; der Hoechstbietende antwortet allein und zahlt.</summary>
        Auction = 3,

        /// <summary>Wette (M35 C3b): jeder wettet auf einen Spieler, auch sich; nur dessen „richtig" gewinnt.</summary>
        Bet = 4,
    }
}
