using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Helpers;
using Quizzer.DataModels.Models;

namespace Quizzer.Views.QuestionTypes
{
    /// <summary>
    /// Was eine neue Frage aus der zuletzt gespeicherten übernimmt (E1, M31, 2026-10-23): Typ,
    /// Kategorie, Punkte, Minuspunkte, Schwierigkeit.
    /// <para>
    /// <b>Warum:</b> „ziel ist es leicht fragen zu ergänzen". Wer zehn Fragen zu einer Kategorie
    /// schreibt, stellte bisher zehnmal Typ, Kategorie und Punkte ein - und die Typwahl war ein
    /// eigenes Fenster vor dem Editor. Jetzt öffnet der Editor gleich, mit dem Stand von eben.
    /// </para>
    /// <para>Nur für diesen Programmlauf; ein Neustart beginnt mit der Standardfrage zu 100 Punkten.</para>
    /// </summary>
    internal static class Vorbelegung
    {
        private static QuestionType typ = QuestionType.Default;
        private static Guid kategorie;
        private static int punkte = 100;
        private static int minuspunkte;
        private static Difficulty schwierigkeit = Difficulty.Level1;

        public static QuestionType Typ => typ;

        /// <summary>Eine neue, ungespeicherte Frage mit dem Stand der letzten.</summary>
        public static QuestionBase NeueFrage(QuestionType? anderer = null)
        {
            var frage = Factory.CreateNewQuestion(anderer ?? typ);
            Wende(frage);
            return frage;
        }

        /// <summary>Setzt Kategorie, Punkte und Schwierigkeit - nicht den Typ.</summary>
        public static void Wende(QuestionBase frage)
        {
            frage.CategoryId = kategorie;
            frage.Points = punkte;
            frage.MinusPoints = frage.Typ == QuestionType.Ordering ? 0 : minuspunkte;
            frage.Difficulty = schwierigkeit;
        }

        /// <summary>Nach dem Speichern einer neuen Frage.</summary>
        public static void Merke(QuestionBase frage)
        {
            typ = frage.Typ;
            kategorie = frage.CategoryId;
            punkte = frage.Points > 0 ? frage.Points : punkte;
            minuspunkte = frage.MinusPoints;
            schwierigkeit = frage.Difficulty;
        }
    }
}
