namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Die Art eines Minispiels (M35). Reihenfolge = Katalog im Plan.
    /// <para>
    /// <b>Werte nur anhaengen:</b> sie stehen als Zahl in der Datenbank. Kein Wert 0 - eine Zeile mit
    /// 0 ist kaputt, nicht „Standard".
    /// </para>
    /// </summary>
    public enum MinigameKind
    {
        /// <summary>Mehrheit: jeder waehlt geheim eine Option, die groesste (oder kleinste) Gruppe gewinnt.</summary>
        Majority = 1,

        /// <summary>Reaktion: rot, nach zufaelliger Wartezeit gruen - wer am schnellsten tippt (M35 B3).</summary>
        Reaction = 2,

        /// <summary>Tippsprint: in der Dauer so oft tippen wie moeglich, Rennen am Beamer (M35 C1).</summary>
        TapSprint = 3,

        /// <summary>Stoppuhr: Uhr starten, sie verschwindet nach 3 s, bei der Zielzeit stoppen (M35 C1).</summary>
        Stopwatch = 4,
    }
}
