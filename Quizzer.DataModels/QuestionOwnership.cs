using Quizzer.DataModels.Models;

namespace Quizzer.DataModels
{
    /// <summary>
    /// Welche Fragen dem angemeldeten Spielleiter zur Verfügung stehen.
    /// <para>
    /// <b>Die Regel in einem Satz:</b> seine eigenen und die ohne Besitzer.
    /// </para>
    /// <para>
    /// Der zweite Teil ist der wichtige. Alle Fragen, die es vor der Einführung der Anmeldung
    /// gab, haben keinen Besitzer - würden sie nur dem Ersten gehören, wäre der gesamte Bestand
    /// für alle anderen verschwunden. So bleiben sie gemeinsamer Vorrat, und alles Neue gehört
    /// dem, der es anlegt.
    /// </para>
    /// <para>
    /// <b>Ohne Anmeldung gilt kein Filter.</b> Das ist der Fall direkt nach dem Einspielen, wenn
    /// noch niemand als Spielleiter angelegt ist - dort wäre eine leere Fragenliste eine Sperre,
    /// aus der man nicht mehr herauskäme.
    /// </para>
    /// </summary>
    public static class QuestionOwnership
    {
        /// <summary>Ob diese Frage dem angemeldeten Spielleiter zur Verfügung steht.</summary>
        public static bool IsVisible(QuestionBase frage)
        {
            if (!Session.IsSignedIn)
                return true;

            return frage.OwnerPlayerId == null || frage.OwnerPlayerId == Session.CurrentModeratorId;
        }

        /// <summary>Filtert eine Liste nach derselben Regel.</summary>
        public static List<QuestionBase> VisibleTo(IEnumerable<QuestionBase> fragen)
            => fragen.Where(IsVisible).ToList();

        /// <summary>
        /// Trägt den angemeldeten Spielleiter als Besitzer ein - aber nur bei einer Frage, die
        /// noch keinen hat. Eine fremde Frage wechselt nicht beim bloßen Öffnen den Besitzer.
        /// </summary>
        public static void ClaimIfUnowned(QuestionBase? frage)
        {
            if (frage == null || !Session.IsSignedIn)
                return;

            frage.OwnerPlayerId ??= Session.CurrentModeratorId;
        }
    }
}
