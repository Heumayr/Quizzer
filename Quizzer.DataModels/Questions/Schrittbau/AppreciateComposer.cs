using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Models.QuestionTypes;

namespace Quizzer.DataModels.Questions.Schrittbau
{
    /// <summary>
    /// Schätzfrage: <b>keine Zeilenliste</b>. Nur der Sollwert und das, was am Ende steht.
    /// <para>
    /// Der Typ hat <c>MinNormalSteps = 0</c> und <c>AllowsMultipleResultSteps = false</c>; heute
    /// bekommt er trotzdem ein volles Schritt-Raster samt Spalte „Ist Lösung", die für ihn nie
    /// etwas bedeuten kann.
    /// </para>
    /// <para>
    /// <b>Der Abschlusstext wird vorgeschlagen, nicht gesetzt.</b> Solange niemand hineintippt,
    /// leitet er sich aus Sollwert und Einheit neu ab - tippt jemand hinein, hört das auf. Das
    /// Merkmal sitzt an der Zeile (<see cref="StepZeile.Beruehrt"/>), nicht am Modell: sonst
    /// könnte man einen einmal abgeleiteten Text nicht mehr von einem getippten unterscheiden.
    /// </para>
    /// </summary>
    public sealed class AppreciateComposer : StepComposerBase
    {
        public override QuestionType Typ => QuestionType.Appreciate;

        public override string AbschlussTitel => "Was am Ende steht";

        public override Schrittbild Lies(QuestionBase frage)
        {
            var bild = base.Lies(frage);

            SchlageAbschlussVor(frage, bild);

            return bild;
        }

        public override void Schreib(QuestionBase frage, Schrittbild bild)
        {
            // Noch einmal vorschlagen: der Sollwert kann sich geaendert haben, seit die Maske
            // aufging - und solange niemand hineingetippt hat, soll der Text ihm folgen.
            SchlageAbschlussVor(frage, bild);

            base.Schreib(frage, bild);
        }

        private static void SchlageAbschlussVor(QuestionBase frage, Schrittbild bild)
        {
            if (frage is not AppreciateQestion schaetzung || bild.Abschluss == null)
                return;

            var beschreibung = AppreciateEvaluator.DescribeExpected(schaetzung);

            if (!string.IsNullOrWhiteSpace(beschreibung))
                bild.Abschluss.SchlageVor(beschreibung);
        }
    }
}
