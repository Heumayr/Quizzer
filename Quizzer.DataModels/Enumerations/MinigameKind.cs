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

        /// <summary>Duell: der Ausloeser waehlt einen Gegner, nur die beiden spielen - Schere-Stein-Papier oder Reaktion (M35 C2).</summary>
        Duel = 5,

        /// <summary>Raeuber und Waechter: alle waehlen geheim „bestehlen: Spieler X" oder „Schild" (M35 C2).</summary>
        RobberAndGuard = 6,

        /// <summary>Teilen oder Nehmen: Ausloeser und gewaehlter Partner entscheiden geheim um den Topf (M35 C2).</summary>
        ShareOrTake = 7,

        /// <summary>Zocker: ein Multiplikator steigt, wer vor dem geheimen Absturz aussteigt, gewinnt (M35 C4).</summary>
        Crash = 8,

        /// <summary>Gluecksrad: der Spielleiter dreht, das Rad trifft den Ausloeser - Ziel waehlt er am Telefon (M35 C4).</summary>
        WheelOfFortune = 9,

        /// <summary>Kopfrechnen: alle rechnen dieselbe Aufgabenfolge, der Server prueft jede Antwort (M35 C5).</summary>
        MentalArithmetic = 10,

        /// <summary>Memory: jeder sein Feld mit derselben Belegung, jedes Umdrehen fragt den Server (M35 C5).</summary>
        Memory = 11,
    }
}
