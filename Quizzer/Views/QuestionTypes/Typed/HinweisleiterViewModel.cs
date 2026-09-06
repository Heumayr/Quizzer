using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Schrittbau;
using System.Windows;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Standard- und Eigenschaftsfrage: eine Leiter aus Hinweisen und darunter die richtige
    /// Antwort.
    /// <para>
    /// <b>Die Reihenfolge ist hier echt</b> - anders als bei Multiple Choice mischt das Spiel
    /// nicht. Deshalb die Pfeile zum Verschieben: das kostete bisher vier Klicks je Verschiebung
    /// (Schritt öffnen, Zahl tippen, schließen).
    /// </para>
    /// </summary>
    public class HinweisleiterViewModel : ZeileneditorViewModel
    {
        private readonly QuestionBase frage;

        internal HinweisleiterViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
            this.frage = frage;

            SchreibePunkte();
        }

        /// <summary>
        /// Ob je Zeile steht, was danach noch zu holen ist. Nur bei der Eigenschaftsfrage - dort
        /// ist der Punkteabzug der Sinn der Sache.
        /// </summary>
        public bool ZeigtPunkte => frage.UseProportionalScoreReductionOnStep;

        public Visibility PunkteVisibility
            => ZeigtPunkte ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Schreibt neben jede Zeile, was danach noch zu holen ist - <b>aus derselben Rechnung
        /// wie das Spiel</b> (<see cref="Punkteabzug"/>), nicht aus einer zweiten Abschrift.
        /// <para>
        /// <b>„von 100" meint den Grundwert</b>, nicht die Punkte im Spielfeld: Schwierigkeit und
        /// Phase greifen erst, wenn die Frage in einem Raster liegt.
        /// </para>
        /// </summary>
        private void SchreibePunkte()
        {
            var schritte = Zeilen.Count(z => !z.IstLeer);

            for (var i = 0; i < Zeilen.Count; i++)
            {
                Zeilen[i].Zusatz = ZeigtPunkte && schritte > 0
                    ? $"danach noch {Punkteabzug.Verbleibend(frage.Points, schritte, i + 1)}"
                    : string.Empty;
            }
        }

        protected override void Melde()
        {
            base.Melde();

            SchreibePunkte();
        }
    }
}
