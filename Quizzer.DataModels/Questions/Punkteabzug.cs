namespace Quizzer.DataModels.Questions
{
    /// <summary>
    /// Wie die Punkte mit jedem aufgedeckten Schritt sinken.
    /// <para>
    /// <b>Herausgezogen, nicht abgeschrieben.</b> Die Eigenschaftsfrage will beim Anlegen je
    /// Hinweis anzeigen, was danach noch zu holen ist - und eine zweite Abschrift derselben
    /// Formel liefe der ersten davon. Was der Editor verspricht, muss das Spiel auch geben.
    /// </para>
    /// <para>
    /// <b>Die ganzzahlige Division ist Absicht und Bestand:</b> bei 100 Punkten und drei
    /// Schritten sind das 33 Abzug je Schritt, nicht 33,33 - das Spiel rechnet seit jeher so, und
    /// eine „Verbesserung" hier verschöbe die Punktestände aller bestehenden Fragen.
    /// </para>
    /// </summary>
    public static class Punkteabzug
    {
        /// <summary>
        /// Was nach <paramref name="gezeigt"/> von <paramref name="schritte"/> Schritten noch zu
        /// holen ist.
        /// </summary>
        /// <param name="grundpunkte">Die Punkte der Zelle, bevor Schritte abgezogen werden.</param>
        /// <param name="schritte">Wie viele Inhaltsschritte die Frage hat.</param>
        /// <param name="gezeigt">Wie viele davon schon zu sehen waren.</param>
        public static int Verbleibend(int grundpunkte, int schritte, int gezeigt)
        {
            if (schritte <= 0 || grundpunkte <= 0)
                return 0;

            return grundpunkte - (grundpunkte / schritte * gezeigt);
        }
    }
}
