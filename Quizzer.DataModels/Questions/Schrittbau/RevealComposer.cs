using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Aufdeckfrage: die Schritte sind <b>Flächen beziehungsweise Schärfestufen</b> und werden
    /// nicht von Hand angelegt, sondern aus der Einrichtung abgeleitet.
    /// <para>
    /// <b>Die Maske bietet deshalb kein „Schritt hinzufügen" an</b> - heute tut sie es, und beim
    /// nächsten „Übernehmen" entfernt der Angleich den Schritt wortlos wieder.
    /// </para>
    /// </summary>
    public sealed class RevealComposer : StepComposerBase
    {
        public override QuestionType Typ => QuestionType.Reveal;

        public override string ZeilenTitel => "Aufdeckschritte";

        public override string AbschlussTitel => "Was am Ende steht";

        /// <summary>
        /// Wie viele Aufdeckschritte die Einstellungen verlangen: je Fläche einen, oder so viele
        /// Schärfestufen wie eingestellt.
        /// <para>
        /// <b>Der Schritt ist die Einheit des Aufdeckens.</b> Ohne diesen Abgleich hätte eine
        /// Frage mit fünf Flächen zwei Schritte, und drei Flächen fielen nie.
        /// </para>
        /// </summary>
        public static int GebrauchteSchritte(RevealQuestion frage, int weicheSchritte)
        {
            ArgumentNullException.ThrowIfNull(frage);

            return frage.Mode == RevealMode.Areas
                ? RevealAreas.Parse(frage.AreasJson).Count
                : Math.Max(weicheSchritte, 0);
        }

        /// <summary>
        /// Gleicht die Zeilenzahl an das an, was die Einrichtung verlangt - hängt an oder nimmt
        /// weg, und lässt die vorhandenen Zeilen samt ihren Schritten stehen.
        /// </summary>
        public void GleicheAn(Schrittbild bild, int gebraucht)
        {
            ArgumentNullException.ThrowIfNull(bild);

            gebraucht = Math.Max(gebraucht, 0);

            while (bild.Zeilen.Count > gebraucht)
                bild.Zeilen.RemoveAt(bild.Zeilen.Count - 1);

            while (bild.Zeilen.Count < gebraucht)
                bild.Zeilen.Add(NeueZeile());
        }

        public override void Schreib(QuestionBase frage, Schrittbild bild)
        {
            ArgumentNullException.ThrowIfNull(bild);

            // Ein Aufdeckschritt ohne Text ist kein leerer Schritt, sondern eine Flaeche, die
            // faellt - er muss geschrieben werden. Deshalb bekommt jede Zeile eine Bezeichnung,
            // sonst raeumt Schrittbild sie als leer weg.
            for (var i = 0; i < bild.Zeilen.Count; i++)
            {
                var schritt = bild.Zeilen[i].Schritt;

                if (string.IsNullOrWhiteSpace(schritt.Designation))
                    schritt.Designation = $"Aufdecken {i + 1}";
            }

            base.Schreib(frage, bild);
        }
    }
}
