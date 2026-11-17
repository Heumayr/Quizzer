using Quizzer.DataModels.Attributes;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.QuestionTypes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace Quizzer.DataModels.Models.Base
{
    [Table(nameof(Game), Schema = "base")]
    public class Game : ModelBase<Game>
    {
        public bool Restart { get; set; } = false;

        public GameState State { get; set; } = GameState.Building;

        public int Height { get; set; } = 5;

        public int Width { get; set; } = 5;

        public int Depth { get; set; } = 0;

        public double CellHeight { get; set; } = 100;

        public double CellWidth { get; set; } = 120;

        public double DifficultyMultiplier { get; set; } = 1;

        public int DifficultyAddition { get; set; } = 0;

        public double DifficultyMinusMultiplier { get; set; } = 0.1;

        public int DifficultyMinusAddition { get; set; } = 0;

        public double PhaseMultiplier { get; set; } = 1;

        public int PhaseAddition { get; set; } = 0;

        public int CurrentRound { get; set; } = 1;

        public int Phase { get; set; } = 1;

        public int SuggestedPhases { get; set; } = 3;

        public Guid? ModeratorPlayerId { get; set; }

        /// <summary>
        /// Das Design dieses Spielabends. <c>null</c> heisst: der Auslieferungsstand, also genau
        /// das Aussehen, das es vor Einfuehrung der Designs gab.
        /// </summary>
        public Guid? GameThemeId { get; set; }

        public Guid RegularChoosingPlayerId { get; set; } = Guid.Empty;
        public Guid CurrentChoosingPlayerId { get; set; } = Guid.Empty;

        /// <summary>Darf ein Kontostand unter 0 fallen? Nein kappt jeden Minus-Vorschlag am Kontostand (M35 E2).</summary>
        public bool AllowNegativeScore { get; set; } = true;

        /// <summary>Ein Teamspiel (M37 T1): die Spielleitung teilt die Mitspieler in <see cref="GameTeams"/> ein.</summary>
        public bool IsTeamGame { get; set; }

        /// <summary>Wie Minispiele im Teamspiel je Team zählen (F-M37-11, Vorgabe Durchschnitt).</summary>
        public TeamMinigameScoring TeamMinigameScoring { get; set; } = TeamMinigameScoring.Average;

        /// <summary>Beratungszeit der Teamberatung in Sekunden, 0 bis <c>TeamGrenzen.MaxCouncilSeconds</c>; 0 heißt ohne Countdown.</summary>
        public int TeamCouncilSeconds { get; set; } = 30;

        [NotMapped]
        public List<int> PhaseTrashholds { get; set; } = new();

        /// <summary>
        /// Die Zellen, an denen wirklich eine Frage hängt.
        /// <para>
        /// <b>Nur die zählen für den Fortschritt.</b> Ein Raster, das größer ist als die Zahl
        /// der zugewiesenen Fragen, hat leere Zellen - und die werden nie gespielt. Zählte man
        /// sie mit, wäre das Spiel nie zu Ende und die Phasenschwelle nie erreicht.
        /// </para>
        /// </summary>
        [NotMapped]
        public IEnumerable<GameGridCoordinate> SpielbareZellen
            => GameGridCoordinates.Where(c => c.HatFrage);

        public void CalculatetThreshold()
        {
            PhaseTrashholds.Clear();

            var coordsCount = SpielbareZellen.Count();

            if (coordsCount == 0 || SuggestedPhases <= 0)
            {
                return;
            }

            var span = (int)Math.Ceiling((double)coordsCount / SuggestedPhases);

            var currTH = 0;
            while (currTH < coordsCount)
            {
                currTH += span;

                if (currTH < coordsCount)
                {
                    PhaseTrashholds.Add(currTH);
                }
            }
        }

        public void RaisePhase()
        {
            Phase++;
            SetPhaseAndSetCoordinatesPhase(Phase);
        }

        public void LowerPhase()
        {
            Phase--;
            SetPhaseAndSetCoordinatesPhase(Phase);
        }

        public void SetPhaseAndSetCoordinatesPhase(int phase)
        {
            if (phase < 1) phase = 1;

            Phase = phase;

            if (GameGridCoordinates.Count == 0) return;

            foreach (var coord in GameGridCoordinates)
            {
                coord.SetPhase(phase);
            }
        }

        public List<Header> Headers { get; set; } = new();

        public List<GameGridCoordinate> GameGridCoordinates { get; set; } = new();

        [ForeignKey(nameof(ModeratorPlayerId))]
        public Player? Moderator { get; set; }

        [ForeignKey(nameof(GameThemeId))]
        public GameTheme? GameTheme { get; set; }

        [NotMapped]
        public IEnumerable<QuestionResult> QuestionResults => GameGridCoordinates.SelectMany(x => x.QuestionResults);

        /// <summary>Summe aller gebuchten Punkte eines Spielers in diesem Spiel - die eine Stelle dafuer.</summary>
        public int Kontostand(Guid playerId) => QuestionResults.Where(r => r.PlayerId == playerId).Sum(r => r.FinalScore);

        /// <summary>
        /// Summe eines Teams (M37 T3) - gerechnet, nie gespeichert (A08), die eine Stelle dafür. Bei welchem Team ein
        /// Ergebnis zählt, sagt der Stempel (<see cref="Questions.Teams.Teamwertung.TeamDes"/>), nicht die heutige Zuordnung.
        /// </summary>
        public int Teamstand(Guid teamId) => QuestionResults.Where(r => Questions.Teams.Teamwertung.TeamDes(this, r) == teamId).Sum(r => r.FinalScore);

        public List<PlayerXGame> PlayerXGames { get; set; } = new();

        /// <summary>Die Teams eines Teamspiels (M37 T1); geladen mit <c>GamesController.Get</c>.</summary>
        public List<GameTeam> GameTeams { get; set; } = new();

        [NotMapped]
        public IEnumerable<Header> Columns => Headers.Where(h => h.HeaderType == HeaderType.Column);

        [NotMapped]
        public IEnumerable<Header> Rows => Headers.Where(h => h.HeaderType == HeaderType.Row);

        [NotMapped]
        public IEnumerable<Player> Players => PlayerXGames.Select(x => x.Player);

        public void CalculateAndSetCurrentPoints()
        {
            if (GameGridCoordinates.Count == 0) return;

            foreach (var coord in GameGridCoordinates)
            {
                coord.CalculateAndSetCurrentPoints();
            }
        }

        public override Game CloneWithoutReferences(bool copyIdentity = true)
        {
            var clone = new Game
            {
                Restart = Restart,
                State = State,
                Height = Height,
                Width = Width,
                Depth = Depth,
                CellHeight = CellHeight,
                CellWidth = CellWidth,
                DifficultyMultiplier = DifficultyMultiplier,
                DifficultyAddition = DifficultyAddition,
                DifficultyMinusMultiplier = DifficultyMinusMultiplier,
                DifficultyMinusAddition = DifficultyMinusAddition,
                PhaseMultiplier = PhaseMultiplier,
                PhaseAddition = PhaseAddition,
                CurrentRound = CurrentRound,
                Phase = Phase,
                SuggestedPhases = SuggestedPhases,
                ModeratorPlayerId = ModeratorPlayerId,
                GameThemeId = GameThemeId,
                RegularChoosingPlayerId = RegularChoosingPlayerId,
                CurrentChoosingPlayerId = CurrentChoosingPlayerId,
                AllowNegativeScore = AllowNegativeScore,
                IsTeamGame = IsTeamGame,
                TeamMinigameScoring = TeamMinigameScoring,
                TeamCouncilSeconds = TeamCouncilSeconds,
                Headers = new List<Header>(),
                GameGridCoordinates = new List<GameGridCoordinate>(),
                PlayerXGames = new List<PlayerXGame>(),
                GameTeams = new List<GameTeam>()
            };

            CopyBaseValuesTo(clone, copyIdentity);
            return clone;
        }
    }
}