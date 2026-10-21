using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Schrittbau;
using System.Windows;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Zuordnung (M12): je Zeile ein Paar - links der feste Begriff, rechts das Gegenstück (Text
    /// oder über „Medium" ein Bild).
    /// <para>
    /// <b>Die Pfeile ordnen die Begriffe</b>: in dieser Folge stehen sie mit 1, 2, 3 … auf dem
    /// Beamer. Die Gegenstücke mischt das Spiel.
    /// </para>
    /// </summary>
    public sealed class ZuordnungViewModel : ZeileneditorViewModel
    {
        internal ZuordnungViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
        }

        public override Visibility PartnerVisibility => Visibility.Visible;

        public override bool DarfSortieren => true;

        public override string Tastenerklaerung
            => "Die Reihenfolge im Editor ist die Nummer des Begriffs. Die Gegenstücke werden bei "
               + "jedem Spielen neu gemischt - der Buchstabe am Telefon ist ein anderer.";

        public override string Zeilenhinweis
        {
            get
            {
                var paare = Zeilen.Count(z => !z.IstLeer);

                var zahl = paare < 3
                    ? $"{paare} Paar(e) - es braucht mindestens drei."
                    : $"{paare} Paare - die Spieler tippen zu Begriff 1 bis {paare} der Reihe nach das Gegenstück an.";

                return zahl
                    + " Links der feste Begriff, rechts das Gegenstück (Text, oder über „Medium“ ein Bild). "
                    + "Jedes richtige Paar bringt seinen Anteil der Punkte.";
            }
        }

        protected override void Melde()
        {
            base.Melde();

            OnPropertyChanged(nameof(Zeilenhinweis));
        }
    }
}
