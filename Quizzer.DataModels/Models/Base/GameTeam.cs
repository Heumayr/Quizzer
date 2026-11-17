using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quizzer.DataModels.Models.Base
{
    /// <summary>
    /// Ein Team eines Spiels (M37 T1, Entwurf <c>team-plan.md</c> Abschnitt 2). Die Spielleitung teilt zu
    /// (<see cref="PlayerXGame.GameTeamId"/>); die Punkte bleiben am Spieler, die Teamsumme wird nie
    /// gespeichert, immer gerechnet (F-M37-01). Gespeichert in <c>base.GameTeam</c>.
    /// <para>
    /// Alle drei Fremdschlüssel stehen auf NO ACTION (Spiel, Zuteilung, Ergebnis) - den Riegel je Weg nennt
    /// <c>DataContext.ConfigureTeams</c>. Ein Team mit Ergebnissen lässt sich nur noch umbenennen.
    /// </para>
    /// </summary>
    [Table(nameof(GameTeam), Schema = "base")]
    public class GameTeam : ModelBase<GameTeam>
    {
        /// <summary>Längster erlaubter Name - die Spalte, der Server und das Bündel halten ihn.</summary>
        public const int MaxNameLength = 30;

        /// <summary>Fremdschlüssel zum Spiel, zu dem das Team gehört. Wechselt nie (V15).</summary>
        public Guid GameId { get; set; }

        /// <summary>Der Name am Beamer und am Telefon, höchstens <see cref="MaxNameLength"/> Zeichen, nie leer.</summary>
        [MaxLength(MaxNameLength)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Die Farbe als Platz in der festen Palette <c>TeamFarben</c> - nie eine freie Farbe.</summary>
        public int ColorIndex { get; set; }

        /// <summary>Die Reihenfolge der Teams in Spielaufbau, Beamer und Siegerehrung.</summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Wer gerade für das Team spricht, als Zähler über die Sprecherfolge (<c>turn mod n</c>, T5). Steigt
        /// im Spiel - deshalb Verlauf, nicht Bauplan.
        /// </summary>
        public int SpeakerTurn { get; set; }

        public override GameTeam CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new GameTeam
            {
                GameId = GameId,
                Name = Name,
                ColorIndex = ColorIndex,
                SortOrder = SortOrder,
                SpeakerTurn = SpeakerTurn,
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}
