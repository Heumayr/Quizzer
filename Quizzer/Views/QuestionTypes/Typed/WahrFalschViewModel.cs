using Quizzer.DataModels.Models;
using Quizzer.DataModels.Questions.Schrittbau;

namespace Quizzer.Views.QuestionTypes.Typed
{
    /// <summary>
    /// Wahr oder falsch (M12): die Zeilen sind Aussagen, der Haken heißt „wahr".
    /// <para>
    /// <b>Kein Verschieben</b> - das Spiel mischt die Aussagen, wie bei Multiple Choice.
    /// </para>
    /// </summary>
    public sealed class WahrFalschViewModel : ZeileneditorViewModel
    {
        internal WahrFalschViewModel(IStepComposer composer, QuestionBase frage, Action melde)
            : base(composer, frage, melde)
        {
        }

        public override string RichtigBeschriftung => "wahr";

        public override bool DarfSortieren => false;

        public override string Tastenerklaerung
            => "Die Reihenfolge im Editor. Am Telefon werden die Aussagen bei jedem Spielen neu "
               + "gemischt - der Buchstabe dort ist ein anderer.";

        public override string Zeilenhinweis
        {
            get
            {
                var aussagen = Zeilen.Where(z => !z.IstLeer).ToList();
                var wahr = aussagen.Count(z => z.IstRichtig);

                return $"{aussagen.Count} Aussagen, davon {wahr} wahr. Die Spieler beurteilen jede am "
                       + "Telefon; jede richtig beurteilte bringt ihren Anteil der Punkte. Auch "
                       + "„alle falsch“ ist erlaubt.";
            }
        }

        protected override void Melde()
        {
            base.Melde();

            OnPropertyChanged(nameof(Zeilenhinweis));
        }
    }
}
