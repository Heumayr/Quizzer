using LocalBuzzer.Service.Base.States;
using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Models.Buzzer;

namespace LocalBuzzer.Service.Base
{
    /// <summary>
    /// Die Team-Abstimmung am Hub (M37 T7, Entwurf Abschnitt 4, F-M37-13): solange sie läuft, sieht jedes Telefon nur die eigene
    /// Stimme und wie viele seines Teams abgestimmt haben; nach dem Schluss die Antwort des eigenen Teams. Die Antworten aller
    /// Teams kommen vom WPF - es entscheidet einen Gleichstand (E1) - als Schnappschuss, der ersetzt, nie geändert wird; jedes
    /// Telefon bekommt davon nur den Eintrag seines Teams. Wer zu welchem Team gehört, sagt die Aufstellung
    /// (<see cref="TeamRoster"/>), nie der Spielgraph; ohne Team ist ein Spieler sein eigenes Team (fail-closed).
    /// <para>Gesetzt vom Oberflächen-Thread, gelesen von den Hub-Threads.</para>
    /// </summary>
    public sealed class TeamVoteBoard
    {
        /// <summary>So viele Stimmen nimmt ein Telefon je <see cref="Window"/> an - jede geht an das ganze Team (Entwurf Abschnitt 4).</summary>
        public const int MaxVotesPerWindow = 5;

        public static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

        private readonly object gate = new();
        private readonly Dictionary<Guid, SlidingRateLimit> takt = new();
        private readonly long start = TimeProvider.System.GetTimestamp();
        private volatile bool aktiv;
        private volatile IReadOnlyDictionary<Guid, string>? ergebnisse;

        /// <summary>Ob an dieser Frage abgestimmt wird.</summary>
        public bool Active => aktiv;

        /// <summary>Die Frage ist offen: ab dem nächsten Stand zeigt jedes Telefon seine Stimme und den Stand seines Teams.</summary>
        public void Start()
        {
            lock (gate) takt.Clear();
            ergebnisse = null;
            aktiv = true;
        }

        /// <summary>Die Frage ist zu - keine Abstimmung mehr.</summary>
        public void End()
        {
            aktiv = false;
            ergebnisse = null;
            lock (gate) takt.Clear();
        }

        /// <summary>„Runde zurücksetzen": es wird neu abgestimmt, eine alte Antwort gilt nicht mehr.</summary>
        public void Reopen() => ergebnisse = null;

        /// <summary>Je Team (Schlüssel wie <see cref="TeamRoster.KeyFor"/>) seine Antwort, wie das Telefon sie nach dem Schluss zeigt.</summary>
        public void SetResults(IReadOnlyDictionary<Guid, string> neu)
        {
            ArgumentNullException.ThrowIfNull(neu);
            ergebnisse = neu.ToDictionary();
        }

        /// <summary>Ob diese Stimme noch in die Grenze passt; ohne laufende Abstimmung immer.</summary>
        public bool TryTakeVote(Guid playerId)
        {
            if (!aktiv)
                return true;

            lock (gate)
            {
                if (!takt.TryGetValue(playerId, out var grenze))
                    takt[playerId] = grenze = new(MaxVotesPerWindow, Window);

                return grenze.TryTake(TimeProvider.System.GetElapsedTime(start));
            }
        }

        /// <summary>Ob zwei Spieler nach der Aufstellung im selben Team sind - ohne Teamspiel nie.</summary>
        public static bool SameTeam(TeamRoster roster, Guid a, Guid b) => roster.KeyFor(a) is { } team && roster.KeyFor(b) == team;

        /// <summary>
        /// Was dieses Telefon von der Abstimmung sieht; <c>null</c> ohne laufende Abstimmung oder ohne Teamspiel. Gezählt wird nur
        /// das eigene Team, und nur wer abstimmen darf (<paramref name="sperre"/>); nach dem Schluss nur die eigene Antwort.
        /// </summary>
        /// <param name="spieler">Alle Spieler des geladenen Spiels - das Team daraus bestimmt die Aufstellung.</param>
        /// <param name="zu">Ob die Abstimmung geschlossen ist.</param>
        public TeamVoteClientInfo? InfoFor(Guid playerId, TeamRoster roster, IEnumerable<Guid> spieler, BuzzerKeySelector tasten, AnswerLock sperre, bool zu)
        {
            if (!aktiv || roster.KeyFor(playerId) is not { } team)
                return null;

            var mitglieder = spieler.Distinct().Where(id => roster.KeyFor(id) == team && sperre.Allows(id)).ToList();

            return new TeamVoteClientInfo
            {
                Own = StimmeVon(tasten, playerId)?.ToList() ?? new(),
                Voted = mitglieder.Count(id => StimmeVon(tasten, id) is { Count: > 0 }),
                Of = mitglieder.Count,
                Closed = zu,
                Result = zu ? ergebnisse?.GetValueOrDefault(team) ?? string.Empty : string.Empty,
            };
        }

        private static IReadOnlyList<string>? StimmeVon(BuzzerKeySelector tasten, Guid spieler)
            => tasten.KeyResultsForPlayer.TryGetValue(spieler, out var wahl) && wahl.CommittedResult ? wahl.SelectedKeys.ToList() : null;
    }
}
