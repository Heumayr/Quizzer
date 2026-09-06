using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Die Aufdeckfrage: ein Bild wird schrittweise sichtbar.
    /// <para>
    /// <b>Nutzerwunsch vom 2026-09-06.</b> Zwei Betriebsarten derselben Fragenart - entweder
    /// fallen nacheinander verdeckende Flaechen weg, oder das Bild wird von Schritt zu Schritt
    /// schaerfer.
    /// </para>
    /// <para>
    /// <b>Die Flaechen stehen als JSON an der Frage, nicht als Spalten am Schritt.</b> Vier
    /// Spalten an QuestionStepResource haetten alle vier anderen Fragetypen mitbelastet, ohne
    /// dass einer davon sie je liest. Der Schritt Nummer n deckt die Flaeche Nummer n auf.
    /// </para>
    /// </summary>
    [Table(nameof(RevealQuestion), Schema = "question")]
    public class RevealQuestion : QuestionBase
    {
        public RevealQuestion()
        {
            Points = 100;
            MinusPoints = 50;
            Typ = QuestionType.Reveal;

            // Alles Typeigene kommt aus dem Profil - das ist die einzige Stelle, an der es
            // steht, und die Eingabemaske liest von dort ebenfalls.
            QuestionTypeProfiles.For(QuestionType.Reveal).ApplyTo(this);
        }

        /// <summary>Welche der beiden Betriebsarten gilt.</summary>
        public RevealMode Mode { get; set; } = RevealMode.Areas;

        /// <summary>
        /// Das Bild, um das es geht - der Dateiname im Ressourcenordner, wie bei einem Schritt.
        /// </summary>
        public string ImageFileName { get; set; } = string.Empty;

        /// <summary>
        /// Die verdeckenden Flaechen als JSON - eine Liste aus je vier Zahlen zwischen 0 und 1
        /// (links, oben, Breite, Hoehe), bezogen auf das Bild.
        /// <para>
        /// <b>Relativ und nicht in Bildpunkten:</b> der Beamer, das Spielleiterfenster und die
        /// Vorschau zeigen dasselbe Bild in drei verschiedenen Groessen.
        /// </para>
        /// </summary>
        public string AreasJson { get; set; } = "[]";

        /// <summary>
        /// Wie unscharf das Bild im ersten Schritt ist. Sinkt bis zum letzten Schritt auf null.
        /// </summary>
        public double BlurStart { get; set; } = 40;

        /// <summary>
        /// Erzeugt die Instanz fuer den Klon - samt den typeigenen Feldern.
        /// <para>
        /// <b>Was hier fehlt, wird nie gespeichert.</b> Der Schreibweg schreibt einen Klon, nie
        /// die uebergebene Frage; ein vergessenes Feld geht lautlos verloren.
        /// </para>
        /// </summary>
        protected override QuestionBase CreateCloneInstance()
        {
            return new RevealQuestion
            {
                Mode = Mode,
                ImageFileName = ImageFileName,
                AreasJson = AreasJson,
                BlurStart = BlurStart,
            };
        }
    }
}
