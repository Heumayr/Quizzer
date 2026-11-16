using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.Views.GameViews.Sub;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Eine Minispiel-Art im Fragefenster (M35 C2): wertet die geschlossenen Runden, bringt die Tafel an
    /// den Beamer und vergisst beim Wiederholen. Das Fenster verteilt nur hierher, statt je Art zu
    /// verzweigen; eine neue Art bringt eine Klasse und eine Zeile in <see cref="Fuer"/>.
    /// <para>Je Frage eine Instanz - ihr Zustand gehoert genau dieser Frage.</para>
    /// </summary>
    internal abstract class MinispielAblauf
    {
        protected MinispielAblauf(CurrentQuestionViewModel fenster, MinigameKind? art)
        {
            Fenster = fenster;
            Art = art;
        }

        /// <summary>Die Art, fuer die diese Instanz gebaut ist - wechselt die Frage, gilt sie nicht mehr.</summary>
        public MinigameKind? Art { get; }

        protected CurrentQuestionViewModel Fenster { get; }

        protected ReaktionAnzeige Anzeige => Fenster.Reaktion;

        protected MinigameQuestion? Frage => Fenster.Minispiel;

        protected PlayersResultViewModel? Karten => Fenster.PlayersResultViewModel;

        protected MinigameState? Zustand => Fenster.BuzzerControlsViewModel?.MinigameState;

        protected List<(Guid Id, string Name)> Spieler() => Fenster.MinispielSpieler();

        /// <summary>Die Strategie zur Art; eine unbekannte spielt wie die Reaktion (Bestand vor C2).</summary>
        public static MinispielAblauf Fuer(MinigameKind? art, CurrentQuestionViewModel fenster) => art switch
        {
            MinigameKind.TapSprint => new TippsprintAblauf(fenster),
            MinigameKind.Stopwatch => new StoppuhrAblauf(fenster),
            _ => new ReaktionAblauf(fenster, art),
        };

        /// <summary>Was waehrend der Runde am Beamer steht.</summary>
        public abstract void ZeigeLaeuft();

        /// <summary>Eine Abgabe ist angenommen - die meisten Arten warten auf das Ende der Runde.</summary>
        public virtual void Abgabe(MinigameSubmission abgabe)
        {
        }

        /// <summary>Eine Runde ist zu oder abgebrochen: werten, Tafel an den Beamer.</summary>
        public abstract void RundeZu(MinigameRoundClosed runde);

        /// <summary>Der Spielleiter spielt die Runde neu (E1) - ihre Wertung zaehlt nicht mehr.</summary>
        public abstract void Wiederhole(int runde);

        /// <summary>„Runde zuruecksetzen" hat das Minispiel am Server auf Anfang gestellt - hier ebenso.</summary>
        public abstract void SetzeZurueck();
    }
}
