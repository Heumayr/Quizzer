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

        /// <summary>Blitzrunde: Aussagen aus den Zeilen, je Spieler einzeln nachgereicht - wahr oder falsch (M35 C5).</summary>
        LightningRound = 12,

        /// <summary>Buchstabensalat: verwuerfelte Begriffe aus den Zeilen, wer zuerst richtig tippt, loest ihn (M35 C5).</summary>
        WordScramble = 13,

        /// <summary>Erklaerbaer: nur der Erklaerer sieht den Begriff, die anderen raten am Telefon - wer zuerst richtig liegt (M35 C6).</summary>
        ExplainIt = 14,

        /// <summary>Bluff: erst schreibt jeder eine falsche Antwort, dann waehlen alle unter Bluffs und der echten (M35 C6).</summary>
        Bluff = 15,

        /// <summary>Heisse Kartoffel: der Halter beantwortet Aussagen, nach einer richtigen gibt er weiter; der Zuender ist geheim (M35 C6).</summary>
        HotPotato = 16,

        /// <summary>Tauziehen: nur im Teamspiel - die Teams tippen gegeneinander, das Seil wandert am Beamer; die Serveruhr entscheidet (M37 T9).</summary>
        TugOfWar = 17,

        /// <summary>Stellvertreter-Duell: nur im Teamspiel - jedes Team schickt einen, nur die Gewaehlten spielen (M37 T10).</summary>
        ProxyDuel = 18,

        /// <summary>Staffel: nur im Teamspiel - die Mitglieder eines Teams tippen reihum je einen Teil aus den Zeilen, ein Fehler gibt ans naechste Team weiter (M37 T11).</summary>
        Relay = 19,
    }
}
