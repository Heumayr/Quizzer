using LocalBuzzer.Service.Base.States.Joker;
using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.DataModels.Anzeige;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Logic.Controller.TypedControllers;

namespace Quizzer.Views.GameViews.Sonderregeln
{
    /// <summary>
    /// Die Joker einer Zelle im Fragefenster (M35 C7b): wer in die Joker-Runde geht, was eingeloest ist, und was daraus
    /// auf Karten, Beamer und Telefonen wird. Je Zelle eine Instanz - ihr Zustand gehoert genau dieser Frage.
    /// <para>
    /// Ein Joker, der schon fuer diese Zelle eingeloest ist (Absturz nach dem Schliessen, Zelle erneut geoeffnet), gilt
    /// weiter; sein Spieler entscheidet nicht noch einmal - hoechstens ein Joker je Spieler und Frage.
    /// </para>
    /// </summary>
    internal sealed class JokerAblauf
    {
        public const string Bereich = "Joker";

        public const string Titel = "Joker-Runde";

        public const string Regeltext = "Wer einen Joker hat, entscheidet jetzt am Telefon: einsetzen oder nicht. Danach kommt die Frage.";

        private readonly Dictionary<Guid, List<JokerOffer>> angebote;
        private readonly IReadOnlyDictionary<Guid, string> namen;
        private readonly Dictionary<Guid, PlayerJoker> eingeloest;
        private readonly List<Guid> neuEingeloest = new();

        /// <param name="jokerDesSpiels">Alle Joker des Spiels, wie sie in der Datenbank stehen.</param>
        /// <param name="zelle">Die Zelle, deren Frage aufgeht.</param>
        /// <param name="namen">Die Mitspieler mit Anzeigenamen - Joker anderer zaehlen nicht.</param>
        public JokerAblauf(IEnumerable<PlayerJoker> jokerDesSpiels, Guid zelle, IReadOnlyDictionary<Guid, string> namen)
        {
            ArgumentNullException.ThrowIfNull(jokerDesSpiels);
            ArgumentNullException.ThrowIfNull(namen);

            this.namen = namen;
            var joker = jokerDesSpiels.Where(j => namen.ContainsKey(j.PlayerId)).OrderBy(j => j.GrantedAt).ToList();

            eingeloest = joker.Where(j => j.GameGridCoordinateId == zelle)
                .GroupBy(j => j.PlayerId)
                .ToDictionary(g => g.Key, g => g.First());

            angebote = joker.Where(j => !j.IsRedeemed && !eingeloest.ContainsKey(j.PlayerId))
                .GroupBy(j => j.PlayerId)
                .ToDictionary(g => g.Key, g => g.Select(j => new JokerOffer(j.Id, j.Kind)).ToList());
        }

        /// <summary>Mindestens ein Mitspieler haelt einen offenen Joker - nur dann gibt es eine Runde.</summary>
        public bool HatRunde => angebote.Count > 0;

        /// <summary>Wie viele in der Runde entscheiden.</summary>
        public int Halter => angebote.Count;

        /// <summary>Schon vor der Runde fuer diese Zelle eingeloeste Joker.</summary>
        public bool HatEingeloeste => eingeloest.Count > 0;

        /// <summary>Die Runde ist zu - erst dann wirken die Joker.</summary>
        public bool Zu { get; private set; }

        /// <summary>Die in diesem Fenster eingeloesten Joker - nur sie gehen beim Verlassen ohne Wertung wieder auf.</summary>
        public IReadOnlyList<Guid> NeuEingeloest => neuEingeloest;

        /// <summary>Wie der Server die Runde einstellt - eine Runde, ohne Countdown.</summary>
        public MinigameSetup Setup()
            => new(default, Titel, Regeltext, 1, CountdownSeconds: 0,
                Jokers: angebote.ToDictionary(e => e.Key, e => (IReadOnlyList<JokerOffer>)e.Value));

        /// <summary>Was eingeloest werden soll: je Wahl mit Joker eine Einloesung. Der Spieler ist der Schluessel - die Verbindung.</summary>
        public IReadOnlyList<JokerRedemption> Einloesungen(IReadOnlyDictionary<Guid, object> abgaben, Guid spiel, Guid zelle)
            => abgaben
                .Select(e => e.Value is JokerChoice { JokerId: { } joker } wahl ? new JokerRedemption(joker, e.Key, spiel, zelle, wahl.TargetId) : null)
                .OfType<JokerRedemption>()
                .ToList();

        /// <summary>Die Runde ist zu: <paramref name="neu"/> sind die Joker, die wirklich geschrieben wurden.</summary>
        public void Schliesse(IEnumerable<PlayerJoker> neu)
        {
            foreach (var joker in neu ?? [])
            {
                eingeloest[joker.PlayerId] = joker;
                neuEingeloest.Add(joker.Id);
            }

            Zu = true;
        }

        /// <summary>Je Karte der Vorschlag ihres Jokers (E1); wer fehlt, fuer den greift keiner.</summary>
        public IReadOnlyDictionary<Guid, Punktevorschlag> WerteAlle(IReadOnlyDictionary<Guid, SonderregelEingang> eingaenge)
        {
            var vorschlaege = new Dictionary<Guid, Punktevorschlag>();

            foreach (var (spieler, eingang) in eingaenge)
            {
                if (Werte(spieler, eingang) is { } vorschlag)
                    vorschlaege[spieler] = vorschlag;
            }

            return vorschlaege;
        }

        /// <summary>Der Vorschlag des Jokers dieses Spielers auf seinen Eingang - an einer Teamregel auf seinen Anteil (M37 T5).</summary>
        public Punktevorschlag? Werte(Guid spieler, SonderregelEingang eingang)
            => Zu && eingeloest.TryGetValue(spieler, out var joker) ? JokerRegel.Werte(joker.Kind, eingang) : null;

        /// <summary>Was die Karte dieses Spielers zeigt: sein Joker und ob ihn einer gesperrt hat; leer, solange die Runde laeuft.</summary>
        public string Abgabe(Guid spieler)
        {
            if (!Zu)
                return string.Empty;

            var teile = new List<string>();

            if (eingeloest.TryGetValue(spieler, out var eigener))
                teile.Add($"Joker: {Beschreibung(eigener)}");

            var sperrer = Sperrer(spieler);
            if (sperrer.Count > 0)
                teile.Add($"Gesperrt per Joker von {string.Join(", ", sperrer)}");

            return string.Join(" · ", teile);
        }

        /// <summary>Was Beamer und Spielleiter waehrend der Frage lesen, etwa „Joker: Anna – Doppelt · Bert – Sperre gegen Clara".</summary>
        public string Hinweis
            => Zu && eingeloest.Count > 0
                ? "Joker: " + string.Join(" · ", eingeloest.Values.OrderBy(j => NameVon(j.PlayerId), StringComparer.CurrentCulture)
                    .Select(j => $"{NameVon(j.PlayerId)} – {Beschreibung(j)}"))
                : string.Empty;

        /// <summary>Wen eine eingeloeste Sperre trifft.</summary>
        public IReadOnlyCollection<Guid> Gesperrte
            => eingeloest.Values.Where(j => j.Kind == JokerKind.Block).Select(j => j.TargetPlayerId).OfType<Guid>().ToHashSet();

        /// <summary>Wer antworten darf - alle ausser den Gesperrten; <c>null</c>: niemand ist gesperrt.</summary>
        public IReadOnlyCollection<Guid>? Antwortende(IEnumerable<Guid> alle)
            => Gesperrte.Count == 0 ? null : alle.Where(id => !Gesperrte.Contains(id)).ToList();

        /// <summary>Je Gesperrtem der Satz fuer sein Telefon.</summary>
        public IReadOnlyDictionary<Guid, string> Sperrsaetze
            => Gesperrte.ToDictionary(id => id, id => $"{string.Join(", ", Sperrer(id))} hat dich per Joker gesperrt – du schaust zu.");

        private List<string> Sperrer(Guid gesperrt)
            => eingeloest.Values.Where(j => j.Kind == JokerKind.Block && j.TargetPlayerId == gesperrt).Select(j => NameVon(j.PlayerId)).ToList();

        private string Beschreibung(PlayerJoker joker)
            => joker.TargetPlayerId is { } ziel ? $"{JokerTexte.Name(joker.Kind)} gegen {NameVon(ziel)}" : JokerTexte.Name(joker.Kind);

        private string NameVon(Guid id) => namen.GetValueOrDefault(id) ?? "?";
    }
}
