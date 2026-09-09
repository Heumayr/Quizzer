namespace Quizzer.DataModels.Enumerations
{
    /// <summary>
    /// Wie die Punkte einer Hinweisfrage mit jedem aufgedeckten Schritt sinken.
    /// <para>
    /// <b>Nutzerentscheidung F16 vom 2026-09-09.</b> Zur Wahl standen die beiden Kurven, die der
    /// Nutzer selbst aufgeschrieben hatte; gewählt hat er „beide, je Frage wählbar" - eine kurze
    /// Frage mit drei Hinweisen und eine lange mit zehn wollen verschieden gerechnet werden.
    /// </para>
    /// </summary>
    public enum ScoreReductionMode
    {
        /// <summary>
        /// Gleichmäßige Schritte, und der letzte Hinweis halbiert.
        /// <para>
        /// Bei 100 Punkten und drei Hinweisen: 67, 34, 17. Der letzte Hinweis zieht nicht den
        /// gleichen Betrag ab wie die davor, sondern nimmt den Rest bis auf den Faktor mit - er
        /// zeigt ja alles. <b>Auf 0 geht es erst im Auflösungsschritt</b>, denn solange nur
        /// Hinweise stehen, kann immer noch geraten werden.
        /// </para>
        /// </summary>
        Linear = 0,

        /// <summary>
        /// Jeder Hinweis halbiert, aufgerundet.
        /// <para>
        /// Bei 100 Punkten: 50, 25, 13 - unabhängig davon, wie viele Hinweise die Frage hat. Der
        /// erste Hinweis kostet am meisten. Für lange Fragen gedacht, bei denen ein
        /// gleichmäßiger Abzug jeden einzelnen Hinweis billig machte.
        /// </para>
        /// </summary>
        Halving = 1,
    }
}
