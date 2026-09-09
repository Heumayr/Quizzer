using Quizzer.DataModels.Enumerations;

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
    /// <b>Der letzte Hinweis ist nicht das Ende der Punkte</b> (Nutzerentscheidung F16 vom
    /// 2026-09-09): „auch wenn alles erkennbar ist kann noch geraten werden ... es muss danach
    /// den aufloesungsschritt geben wo zB. der name der gesuchent figur angezeit wird ... ab dann
    /// 0 punkte". Diese Klasse rechnet deshalb nur die <b>Hinweise</b>; die Null im
    /// Auflösungsschritt setzt der Aufrufer, der als Einziger weiß, worauf er gerade steht.
    /// </para>
    /// </summary>
    public static class Punkteabzug
    {
        /// <summary>Womit multipliziert wird, wenn die Frage nichts anderes sagt.</summary>
        public const double Vorgabefaktor = 0.5;

        /// <summary>
        /// Was nach <paramref name="gezeigt"/> von <paramref name="schritte"/> Hinweisen noch zu
        /// holen ist.
        /// </summary>
        /// <param name="grundpunkte">Die Punkte der Zelle, bevor Schritte abgezogen werden.</param>
        /// <param name="schritte">Wie viele Inhaltsschritte die Frage hat.</param>
        /// <param name="gezeigt">Wie viele davon schon zu sehen waren.</param>
        /// <param name="modus">Nach welcher Kurve gerechnet wird.</param>
        /// <param name="faktor">Womit multipliziert wird; 0 oder kleiner nimmt die Vorgabe.</param>
        public static int Verbleibend(
            int grundpunkte,
            int schritte,
            int gezeigt,
            ScoreReductionMode modus = ScoreReductionMode.Linear,
            double faktor = Vorgabefaktor)
        {
            if (schritte <= 0 || grundpunkte <= 0)
                return 0;

            if (faktor <= 0 || faktor >= 1)
                faktor = Vorgabefaktor;

            gezeigt = Math.Clamp(gezeigt, 0, schritte);

            return modus == ScoreReductionMode.Halving
                ? Halbierend(grundpunkte, gezeigt, faktor)
                : Linear(grundpunkte, schritte, gezeigt, faktor);
        }

        /// <summary>
        /// Gleichmäßige Schritte, und der letzte nimmt den Rest bis auf den Faktor mit.
        /// <para>
        /// 100 Punkte, drei Hinweise: 67, 34, 17. Der dritte zieht nicht wieder 33 ab - das gäbe
        /// 1 und wäre praktisch die Auflösung, obwohl noch nichts aufgelöst ist. Er halbiert
        /// stattdessen, was der zweite übrig gelassen hat.
        /// </para>
        /// </summary>
        private static int Linear(int grundpunkte, int schritte, int gezeigt, double faktor)
        {
            if (gezeigt < schritte)
                return grundpunkte - (grundpunkte / schritte * gezeigt);

            var vorLetztem = grundpunkte - (grundpunkte / schritte * (schritte - 1));

            return (int)Math.Ceiling(vorLetztem * faktor);
        }

        /// <summary>
        /// Jeder Hinweis halbiert, aufgerundet: 100, 50, 25, 13.
        /// <para>
        /// Aufgerundet, damit eine lange Frage nicht schon nach dem halben Weg auf null steht -
        /// aus 1 wird so wieder 1 und nie 0. <b>Die Null gehört dem Auflösungsschritt</b>, nicht
        /// einer Rundung.
        /// </para>
        /// </summary>
        private static int Halbierend(int grundpunkte, int gezeigt, double faktor)
        {
            var wert = (double)grundpunkte;

            for (var i = 0; i < gezeigt; i++)
                wert = Math.Ceiling(wert * faktor);

            return (int)wert;
        }
    }
}
