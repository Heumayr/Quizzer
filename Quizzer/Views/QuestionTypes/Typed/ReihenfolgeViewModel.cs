using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Reihenfolge (M12): die Zeilen sind die Elemente, <b>und ihre Reihenfolge ist die Lösung</b>.
    /// <para>
    /// Deshalb die Pfeile, anders als bei Multiple Choice: dort ist die Reihenfolge im Editor
    /// bedeutungslos, hier ist sie das Einzige, worauf es ankommt.
    /// </para>
    /// </summary>
    public sealed class ReihenfolgeViewModel : ZeileneditorViewModel
    {
        internal ReihenfolgeViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
        }

        public override bool DarfSortieren => true;

        public override string Tastenerklaerung
            => "Die Reihenfolge im Editor ist die Lösung. Am Telefon werden die Elemente bei jedem "
               + "Spielen neu gemischt - der Buchstabe dort ist ein anderer.";

        /// <summary>Was unter der Liste steht.</summary>
        public override string Zeilenhinweis
        {
            get
            {
                var elemente = Zeilen.Count(z => !z.IstLeer);

                var zahl = elemente switch
                {
                    0 => "Noch kein Element.",
                    1 => "1 Element - es braucht mindestens drei.",
                    2 => "2 Elemente - es braucht mindestens drei.",
                    _ => $"{elemente} Elemente - die Spieler tippen alle {elemente} der Reihe nach an.",
                };

                return zahl
                    + " Oben steht, was zuerst kommt. Jedes Element am richtigen Platz bringt seinen "
                    + "Anteil der Punkte; bei der Auflösung zeigt der Beamer zu jedem Element den Platz.";
            }
        }

        protected override void Melde()
        {
            base.Melde();

            OnPropertyChanged(nameof(Zeilenhinweis));
        }
    }
}
