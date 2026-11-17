using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was das Telefon bei Raeuber und Waechter abgibt: das Schild oder ein Opfer. Feldnamen = <c>raeuber.js</c>.</summary>
    public sealed class RobberPayload
    {
        public bool Shield { get; set; }

        public Guid? Victim { get; set; }
    }

    /// <summary>Was ein Telefon bei Raeuber und Waechter sieht - nur die eigene Wahl, am Ende was daraus wurde.</summary>
    public sealed class RobberClientInfo
    {
        /// <summary>Wen man bestehlen kann - alle anderen Mitspieler, solange man noch nicht gewaehlt hat.</summary>
        public List<MinigamePlayerOption> Options { get; set; } = new();

        public bool OwnShield { get; set; }

        public string? OwnVictimName { get; set; }

        /// <summary>Erst am Ende: was aus der eigenen Wahl wurde und wer einen bestehlen wollte.</summary>
        public List<string> OwnResults { get; set; } = new();
    }

    /// <summary>
    /// Raeuber und Waechter am Server (M35 C2): alle waehlen geheim und gleichzeitig. Geprueft wird, dass
    /// das Opfer ein Mitspieler des laufenden Spiels ist, nicht der Waehlende selbst und im Teamspiel ein Gegner (M37 T8). Gewertet wird im
    /// Fragefenster (<see cref="RaeuberEvaluator"/>) - hier steht nur, was jedes Telefon sehen darf.
    /// </summary>
    public sealed class RobberRules : IMinigameRules
    {
        private readonly MinigameRulesContext kontext;
        private Dictionary<Guid, RaeuberWahl> laufend = new();
        private Dictionary<Guid, RaeuberWahl> gezaehlt = new();

        public RobberRules(MinigameRulesContext kontext)
        {
            this.kontext = kontext ?? throw new ArgumentNullException(nameof(kontext));
        }

        public MinigameKind Kind => MinigameKind.RobberAndGuard;

        public IReadOnlyDictionary<Guid, object> CurrentSubmissions
            => laufend.ToDictionary(w => w.Key, w => (object)w.Value);

        public void StartRound(int round)
        {
            laufend = new();
            gezaehlt = new();
        }

        public object? Accept(Player player, JsonElement payload)
        {
            if (MinigamePayload.Read<RobberPayload>(payload) is not { } abgabe)
                return null;

            RaeuberWahl wahl;

            if (abgabe.Shield)
                wahl = RaeuberWahl.MitSchild;
            else if (abgabe.Victim is { } opfer && kontext.IstZiel(player.Id, opfer))
                wahl = RaeuberWahl.Raub(opfer);
            else
                return null;

            laufend[player.Id] = wahl;
            return wahl;
        }

        public bool HasSubmitted(Guid playerId) => laufend.ContainsKey(playerId);

        public void CloseRound() => gezaehlt = new(laufend);

        public void DiscardRunning() => laufend = new();

        public void Clear()
        {
            laufend = new();
            gezaehlt = new();
        }

        public object? InfoFor(Player? player, MinigamePhase phase)
        {
            var info = new RobberClientInfo();

            if (player == null)
                return info;

            var eigene = laufend.GetValueOrDefault(player.Id) ?? gezaehlt.GetValueOrDefault(player.Id);

            if (phase == MinigamePhase.Running && eigene == null)
            {
                info.Options = kontext.Mitspieler()
                    .Where(p => kontext.IstZiel(player.Id, p.Id))
                    .Select(p => new MinigamePlayerOption { Id = p.Id, Name = Name(p.Id) })
                    .OrderBy(o => o.Name, StringComparer.CurrentCulture)
                    .ToList();
            }

            info.OwnShield = eigene?.Schild == true;
            info.OwnVictimName = eigene?.Opfer is { } opfer ? Name(opfer) : null;

            // Fremde Wahlen erst mit der Aufloesung - vorher erfuehre das Telefon, wer es bestehlen will.
            if (phase == MinigamePhase.Finished)
                info.OwnResults = Ergebnisse(player.Id);

            return info;
        }

        /// <summary>Was die gezaehlte Runde fuer diesen Spieler bedeutet - in der zweiten Person.</summary>
        private List<string> Ergebnisse(Guid spieler)
        {
            var ergebnis = RaeuberEvaluator.Werte(gezaehlt, 1, Kontostaende.OhneGrenze, Name);
            var saetze = new List<string>();

            foreach (var zug in ergebnis.Zuege.Where(z => z.Dieb == spieler))
                saetze.Add(zug.Abgewehrt
                    ? $"{Name(zug.Opfer)} hatte ein Schild – du zahlst."
                    : $"Du hast {Name(zug.Opfer)} bestohlen – kein Schild.");

            foreach (var zug in ergebnis.Zuege.Where(z => z.Opfer == spieler))
                saetze.Add(zug.Abgewehrt
                    ? $"Dein Schild hielt gegen {Name(zug.Dieb)} – {Name(zug.Dieb)} zahlt dir."
                    : $"{Name(zug.Dieb)} hat dich bestohlen.");

            if (saetze.Count == 0)
                saetze.Add(gezaehlt.ContainsKey(spieler) ? "Niemand wollte dich bestehlen." : "Du hast nichts gewählt.");

            return saetze;
        }

        private string Name(Guid id) => kontext.NameVon(id) ?? "?";
    }
}
