namespace Quizzer.DataModels.Helpers
{
    /// <summary>
    /// Punktebewegungen auf der Spielerkarte (M35): Klau, Einsatz, Gewinn sind nur Vorschlag; gebucht
    /// wird erst durch den Spielleiter (E1). Ob ein Kontostand unter 0 fallen darf, steht am Spiel (E2).
    /// </summary>
    public static class Punktebewegung
    {
        /// <summary>
        /// Kappt einen Minus-Vorschlag am Kontostand, wenn das Spiel kein Minus erlaubt. Ein Plus und ein
        /// erlaubtes Minus bleiben, wie sie sind; ein Kontostand schon unter 0 laesst nichts mehr abziehen.
        /// </summary>
        public static int Kappe(int vorschlag, int kontostand, bool unterNullErlaubt)
        {
            if (unterNullErlaubt || vorschlag >= 0)
                return vorschlag;

            return Math.Max(vorschlag, -Math.Max(0, kontostand));
        }
    }
}
