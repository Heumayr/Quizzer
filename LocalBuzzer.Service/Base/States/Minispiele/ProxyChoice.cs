using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei der Wahl des Stellvertreters abgibt. Feldnamen = <c>stellvertreter.js</c>; eine Zufallszahl gehört nicht hinein.</summary>
    public sealed class ProxyChoicePayload
    {
        /// <summary>Wen das Team schickt - ein Mitglied des eigenen Teams.</summary>
        public Guid? Chosen { get; set; }

        /// <summary>Statt einer Wahl: der Server lost unter den Mitgliedern.</summary>
        public bool Lot { get; set; }
    }

    /// <summary>Ein Team hat seinen Stellvertreter, andere wählen noch - jedes Telefon sieht es sofort.</summary>
    public sealed record ProxyChosen(Stellvertreter Vertreter);

    /// <summary>Alle Teams haben ihren Stellvertreter: die Disziplin beginnt, jedes Telefon bekommt seinen Zustand.</summary>
    /// <param name="Zuletzt">Die Wahl, mit der die Wahl schloss.</param>
    /// <param name="Vertreter">Je Team sein Stellvertreter.</param>
    public sealed record ProxiesComplete(Stellvertreter Zuletzt, IReadOnlyList<Stellvertreter> Vertreter) : IMinigameStageChange;

    /// <summary>
    /// Die Wahl der Stellvertreter (M37 T10). Wer beim Start in einem Team verbunden war, wählt für sein Team ein Mitglied aus
    /// eben diesen - oder lässt losen. <b>Das Team kommt aus der Aufstellung des Hubs, nie aus der Nachricht</b>; die erste
    /// gültige Wahl gilt fürs Team, danach wählt es nicht mehr. Gelost wird nur hier, mit der Zufallsquelle des Servers.
    /// Je Telefon nimmt die Wahl höchstens <see cref="StellvertreterEvaluator.MaxWahlenJeFenster"/> Versuche je Fenster an.
    /// </summary>
    internal sealed class ProxyChoice
    {
        private readonly MinigameRulesContext kontext;
        private Dictionary<Guid, TeamRosterEntry> teams = new();
        private Dictionary<Guid, Guid> mitglieder = new();
        private Dictionary<Guid, Stellvertreter> gewaehlt = new();
        private Dictionary<Guid, SlidingRateLimit> takt = new();

        public ProxyChoice(MinigameRulesContext kontext)
        {
            this.kontext = kontext;
        }

        /// <summary>Ob der Start festgehalten hat, wer mitspielt - bis zum Zurücksetzen gilt es.</summary>
        public bool Festgelegt => teams.Count > 0;

        public bool Offen => Festgelegt && gewaehlt.Count < teams.Count;

        public bool Fertig => Festgelegt && gewaehlt.Count == teams.Count;

        /// <summary>Die Teams, die mitspielen, nach Kennung - die Reihenfolge hängt an keiner Nachricht.</summary>
        public IReadOnlyList<Guid> TeamIds => teams.Keys.Order().ToList();

        /// <summary>Je Team sein Stellvertreter, nach Kennung des Teams.</summary>
        public IReadOnlyList<Stellvertreter> Vertreter => gewaehlt.Values.OrderBy(v => v.TeamId).ToList();

        public TeamRosterEntry? Team(Guid teamId) => teams.GetValueOrDefault(teamId);

        /// <summary>Für welches Team ein Spieler beim Start dabei war; <c>null</c> ohne Team oder getrennt.</summary>
        public Guid? TeamVon(Guid spieler) => mitglieder.TryGetValue(spieler, out var team) ? team : null;

        public Stellvertreter? VertreterVon(Guid teamId) => gewaehlt.GetValueOrDefault(teamId);

        /// <summary>Ob dieser Spieler gerade für sein Team wählen darf.</summary>
        public bool WaehltJetzt(Guid spieler) => Offen && TeamVon(spieler) is { } team && !gewaehlt.ContainsKey(team);

        public bool IstVertreter(Guid spieler) => Fertig && gewaehlt.Values.Any(v => v.SpielerId == spieler);

        /// <summary>Hält fest, wer mitspielt: verbundene Mitspieler mit Team - ohne Team oder getrennt schaut zu (fail-closed).</summary>
        public void LegeFest()
        {
            teams = new();
            mitglieder = new();

            foreach (var spieler in kontext.Mitspieler().Where(p => p.ConnectionState == PlayerConnection.Connected))
            {
                if (kontext.Team?.Invoke(spieler.Id) is not { } eintrag)
                    continue;

                mitglieder[spieler.Id] = eintrag.TeamId;
                teams[eintrag.TeamId] = eintrag;
            }
        }

        /// <summary>Nimmt die Wahl eines Mitglieds an; <c>null</c> heisst verworfen.</summary>
        public Stellvertreter? Accept(Player player, JsonElement payload)
        {
            if (!WaehltJetzt(player.Id) || !Takt(player.Id) || MinigamePayload.Read<ProxyChoicePayload>(payload) is not { } abgabe)
                return null;

            var team = mitglieder[player.Id];

            // Beim Los zählt nichts aus der Nachricht - auch kein mitgeschicktes „chosen“.
            var wer = abgabe.Lot ? Lose(team)
                : abgabe.Chosen is { } id && TeamVon(id) == team ? id
                : (Guid?)null;

            return wer is { } vertreter ? gewaehlt[team] = new Stellvertreter(team, vertreter, abgabe.Lot) : null;
        }

        /// <summary>Der Spielleiter lost für jedes Team, das noch keinen hat (E1); was schon gewählt ist, bleibt.</summary>
        public IReadOnlyList<Stellvertreter> LoseOffene()
        {
            var neu = new List<Stellvertreter>();

            foreach (var team in TeamIds.Where(t => !gewaehlt.ContainsKey(t)))
            {
                if (Lose(team) is { } wer)
                    neu.Add(gewaehlt[team] = new Stellvertreter(team, wer, true));
            }

            return neu;
        }

        /// <summary>Die Tasten mit den Mitgliedern des eigenen Teams - nur für den, der gerade wählt.</summary>
        public List<MinigamePlayerOption> OptionsFor(Player? player)
            => player != null && WaehltJetzt(player.Id)
                ? mitglieder.Where(m => m.Value == mitglieder[player.Id])
                    .Select(m => new MinigamePlayerOption { Id = m.Key, Name = kontext.NameVon(m.Key) ?? "?" })
                    .OrderBy(o => o.Name, StringComparer.CurrentCulture)
                    .ToList()
                : new();

        public void Clear()
        {
            teams = new();
            mitglieder = new();
            gewaehlt = new();
            takt = new();
        }

        private Guid? Lose(Guid team)
            => StellvertreterEvaluator.Lose(mitglieder.Where(m => m.Value == team).Select(m => m.Key), kontext.Zufall);

        private bool Takt(Guid spieler)
        {
            if (!takt.TryGetValue(spieler, out var grenze))
                takt[spieler] = grenze = new(StellvertreterEvaluator.MaxWahlenJeFenster, TimeSpan.FromSeconds(StellvertreterEvaluator.FensterSekunden));

            return grenze.TryTake(kontext.Jetzt());
        }
    }
}
