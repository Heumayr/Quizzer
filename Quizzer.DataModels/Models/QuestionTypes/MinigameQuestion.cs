using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions;
using Quizzer.DataModels.Questions.Minispiele;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.QuestionTypes
{
    /// <summary>
    /// Minispiel (M35 B2): das Feld enthaelt statt einer Frage ein Spiel. Die Art steht in
    /// <see cref="Kind"/>, die allgemeinen Parameter deutet je Art <see cref="MinigameProfiles"/>.
    /// <para>
    /// Inhalt (Antwortoptionen, Begriffe, Aussagen) liegt in den gewoehnlichen Schritten - wie bei
    /// Multiple Choice, damit Maske, Telefon und Beamer ihren Weg behalten.
    /// </para>
    /// </summary>
    [Table(nameof(MinigameQuestion), Schema = "question")]
    public sealed class MinigameQuestion : QuestionBase
    {
        public MinigameQuestion()
        {
            Points = 100;

            // E1: nie Minuspunkte von selbst - die Felder bleiben, der Spielleiter bucht.
            MinusPoints = 0;
            Typ = QuestionType.Minigame;
            WarnOnResultStep = false;
            ShowTextOnKeySelect = true;

            QuestionTypeProfiles.For(QuestionType.Minigame).ApplyTo(this);
            MinigameProfiles.For(MinigameKind.Majority).ApplyDefaults(this);
        }

        /// <summary>Welches Minispiel. Nur anhaengen, nie umnummerieren - die Zahl steht in der Datenbank.</summary>
        public MinigameKind Kind { get; set; }

        /// <summary>Spieldauer in Sekunden, wo die Art eine kennt.</summary>
        public int DurationSeconds { get; set; }

        /// <summary>Anzahl der Runden, wo die Art welche spielt.</summary>
        public int Rounds { get; set; }

        /// <summary>Anteil der Feldpunkte in Prozent, wo die Art damit rechnet (Klau, Einsatz).</summary>
        public int Share { get; set; }

        /// <summary>Spielweise - Index in <see cref="MinigameProfile.Variants"/>.</summary>
        public int Variant { get; set; }

        /// <summary>„Minispiel: Mehrheit" - so steht es auf dem Aufdeckbildschirm und in den Listen.</summary>
        [NotMapped]
        public override string TypDisplayName
            => MinigameProfiles.TryFor(Kind, out var art)
                ? $"{base.TypDisplayName}: {art.DisplayName}"
                : base.TypDisplayName;

        /// <summary>Was hier fehlt, wird nie gespeichert - der Schreibweg schreibt einen Klon.</summary>
        protected override QuestionBase CreateCloneInstance()
        {
            return new MinigameQuestion
            {
                Kind = Kind,
                DurationSeconds = DurationSeconds,
                Rounds = Rounds,
                Share = Share,
                Variant = Variant,
            };
        }
    }
}
