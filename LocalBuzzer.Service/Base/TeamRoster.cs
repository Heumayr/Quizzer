using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.Buzzer;
using Quizzer.DataModels.Questions.Teams;

namespace LocalBuzzer.Service.Base
{
    /// <summary>Das Team eines Spielers, wie der Hub es kennt: Kennung, Name, Platz in der Palette und Symbol.</summary>
    public sealed record TeamRosterEntry(Guid TeamId, string Name, int ColorIndex, string Symbol)
    {
        /// <summary>Der Name des Sprechers (M37 T5); leer, wenn das Team keinen hat. Nur zum Anzeigen - zwei Mitglieder können so heißen.</summary>
        public string Speaker { get; init; } = string.Empty;

        /// <summary>Der Sprecher als Kennung (V27) - gesucht wird danach, nie nach dem Namen; <c>null</c> ohne Sprecher.</summary>
        public Guid? SpeakerId { get; init; }

        /// <summary>Der Platz des Teams aus <see cref="GameTeam.SortOrder"/> (V27) - in dieser Reihenfolge stehen die Teams überall.</summary>
        public int SortOrder { get; init; }

        /// <summary>Was das Telefon davon zeigt - die Kennung bleibt am Server.</summary>
        public TeamClientInfo ToClientInfo() => new() { Name = Name, Symbol = Symbol, ColorIndex = ColorIndex, Speaker = Speaker };
    }

    /// <summary>
    /// Wer im Teamspiel zu welchem Team gehört, als unveränderlicher Schnappschuss (M37 T4, Entwurf Abschnitt 4). Das WPF
    /// baut ihn aus dem geladenen Spiel und ersetzt ihn über <c>BuzzerController.SetTeamsAsync</c> - der Hub liest die
    /// Zuordnung nie aus dem Spielgraphen (Muster <see cref="AnswerLock"/>).
    /// <para>
    /// <b>Ein Spieler ohne Team ist im Teamspiel sein eigenes Team</b> (fail-closed): er sperrt mit seinem Buzzer nur
    /// sich selbst und ist für niemanden ein Teamkollege.
    /// </para>
    /// </summary>
    public sealed class TeamRoster
    {
        private readonly IReadOnlyDictionary<Guid, TeamRosterEntry> nachSpieler;

        /// <summary>Kein Teamspiel - so startet jeder Server.</summary>
        public static TeamRoster Empty { get; } = new(false, new Dictionary<Guid, TeamRosterEntry>());

        public TeamRoster(bool isTeamGame, IReadOnlyDictionary<Guid, TeamRosterEntry> nachSpieler)
        {
            ArgumentNullException.ThrowIfNull(nachSpieler);

            IsTeamGame = isTeamGame;
            this.nachSpieler = isTeamGame ? nachSpieler.ToDictionary() : new Dictionary<Guid, TeamRosterEntry>();
        }

        public bool IsTeamGame { get; }

        /// <summary>Das Team des Spielers; <c>null</c> ohne Teamspiel oder ohne Team.</summary>
        public TeamRosterEntry? TeamOf(Guid playerId) => nachSpieler.GetValueOrDefault(playerId);

        /// <summary>Wofür die Team-Sperre gilt: das Team, ohne Team der Spieler selbst; <c>null</c> ohne Teamspiel.</summary>
        public Guid? KeyFor(Guid playerId) => IsTeamGame ? TeamOf(playerId)?.TeamId ?? playerId : null;

        /// <summary>
        /// Der Schnappschuss des geladenen Spiels - die Zuordnung nach <see cref="Teamzuordnung.TeamVon"/>, wie beim Stempel,
        /// der Sprecher nach <see cref="Sprecherfolge"/> (M37 T5). <paramref name="sprecher"/>: je Team der Sprecher dieser
        /// Frage, wenn er vom Stand abweicht (Joker-Sperre, getrennt) - das Telefon zeigt, wer wirklich antworten darf.
        /// </summary>
        public static TeamRoster From(Game? spiel, IReadOnlyDictionary<Guid, Guid>? sprecher = null)
        {
            if (spiel is not { IsTeamGame: true })
                return Empty;

            var eintraege = new Dictionary<Guid, TeamRosterEntry>();
            var namen = spiel.PlayerXGames.Where(x => x.Player != null).DistinctBy(x => x.PlayerId)
                .ToDictionary(x => x.PlayerId, x => x.Player.CalculatedDisplayName);

            foreach (var platz in spiel.PlayerXGames)
            {
                if (Teamzuordnung.TeamVon(spiel, platz.PlayerId) is not { } teamId)
                    continue;

                var team = spiel.GameTeams.First(t => t.Id == teamId);
                var farbe = TeamFarben.IsValid(team.ColorIndex) ? TeamFarben.Alle[team.ColorIndex] : null;
                var wer = sprecher?.GetValueOrDefault(team.Id) is { } fest && fest != Guid.Empty ? fest : Sprecherfolge.Sprecher(Sprecherfolge.Folge(spiel, team));

                eintraege[platz.PlayerId] = new TeamRosterEntry(team.Id, team.Name, farbe?.Index ?? -1, farbe?.Symbol ?? string.Empty)
                {
                    Speaker = wer is { } id ? namen.GetValueOrDefault(id) ?? string.Empty : string.Empty,
                    SpeakerId = wer,
                    SortOrder = team.SortOrder,
                };
            }

            return new TeamRoster(true, eintraege);
        }
    }
}
