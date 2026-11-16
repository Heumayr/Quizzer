using LocalBuzzer.Service.Base.States.Minispiele;
using LocalBuzzer.Service.Hubs.Accessors;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using System.Text.Json;

namespace Quizzer.LogicUnitTests.LocalBuzzer.Base
{
    /// <summary>
    /// Der Pruefstand der Heissen Kartoffel (M35 C6): drei verbundene Spieler, Anna hat das Feld aufgedeckt, eine Uhr zum
    /// Vorstellen und ein Zuender an einer Stelle, deren Zahl sonst nirgends vorkommt.
    /// </summary>
    internal sealed class KartoffelPruefstand
    {
        public static readonly TimeSpan Vorlauf = TimeSpan.FromSeconds(3);

        /// <summary>Die laengste Zuendschnur - der Zuender liegt zwischen 20 und 60 Sekunden.</summary>
        public const int Dauer = 60;

        /// <summary>So weit ueber der Untergrenze: es knallt 41,337 s nach dem Countdown.</summary>
        public const int Wurf = 21_337;

        public static readonly TimeSpan Zuender = TimeSpan.FromMilliseconds(20_000 + Wurf);

        // Vier wahr, zwei falsch, keine Ziffer im Text: umgedreht ergibt sich eine andere Aufloesung, und keine Zahl im Text
        // kann mit einer Zeit verwechselt werden.
        public static readonly MinispielZeile[] Aussagen =
        [
            new("Der Mond ist ein Planet.", false),
            new("Berlin liegt an der Spree.", true),
            new("Spinnen haben acht Beine.", true),
            new("Wale sind Säugetiere.", true),
            new("Paris ist die Hauptstadt Frankreichs.", true),
            new("Der Rhein mündet ins Schwarze Meer.", false),
        ];

        private TimeSpan startzeit;

        public KartoffelPruefstand()
        {
            Anna = Spieler("Anna");
            Bert = Spieler("Bert");
            Carl = Spieler("Carl");
            Uhr = new StellbareUhr();

            Game = new Game { Id = Guid.NewGuid() };
            foreach (var p in new[] { Anna, Bert, Carl })
                Game.PlayerXGames.Add(new PlayerXGame { Id = Guid.NewGuid(), GameId = Game.Id, PlayerId = p.Id, Player = p });

            Spiel = Neu();
        }

        public Player Anna { get; }

        public Player Bert { get; }

        public Player Carl { get; }

        public Game Game { get; }

        public StellbareUhr Uhr { get; }

        public MinigameState Spiel { get; }

        private TimeSpan Jetzt => TimeSpan.FromTicks(Uhr.GetTimestamp());

        /// <summary>Ein Rahmen an derselben Uhr. Der Zuender kommt aus dem einzigen grossen Wurf; das Mischen nimmt immer 0.</summary>
        public MinigameState Neu(Func<int, int, int>? zufall = null, int wurf = Wurf)
            => new(new GameAccessor { GetGame = () => Game }) { Zufall = zufall ?? ((a, b) => b > 1000 ? wurf : a), Uhr = Uhr };

        private static Player Spieler(string name)
            => new() { Id = Guid.NewGuid(), Designation = name, DisplayName = name, ConnectionState = PlayerConnection.Connected };

        public void Starte(MinigameState zustand, IReadOnlyList<MinispielZeile>? inhalt = null, bool ohneAusloeser = false)
        {
            zustand.Configure(new MinigameSetup(MinigameKind.HotPotato, "Heiße Kartoffel", "Regel", 1, DurationSeconds: Dauer,
                TriggerPlayerId: ohneAusloeser ? null : Anna.Id, Share: 50, Content: inhalt ?? Aussagen));
            zustand.Reset();
            Assert.IsTrue(zustand.StartNextRound(), "Die Runde liess sich nicht starten.");
            startzeit = Jetzt;
        }

        /// <summary>Stellt die Uhr auf diesen Abstand zum Start der Runde (Countdown eingeschlossen).</summary>
        public void Bis(TimeSpan seitStart) => Uhr.Vor(startzeit + seitStart - Jetzt);

        /// <summary>Wie lange der Zuender ab jetzt noch brennt.</summary>
        public TimeSpan Rest => startzeit + Vorlauf + Zuender - Jetzt;

        public MinigameSubmitResult Gib(MinigameState zustand, Player p, object nutzlast, MinigameKind art = MinigameKind.HotPotato)
            => zustand.Submit(p, new MinigameAction
            {
                Kind = art,
                Durchgang = zustand.Durchgang,
                Payload = JsonSerializer.SerializeToElement(nutzlast),
            });

        public HotPotatoClientInfo Art(MinigameState zustand, Player? p)
            => (HotPotatoClientInfo)((MinigameClientInfo)zustand.InfoFor(p)).KindInfo!;

        public HotPotatoClientInfo Art(Player? p) => Art(Spiel, p);

        public static bool Wahr(string? text, IReadOnlyList<MinispielZeile>? inhalt = null)
            => (inhalt ?? Aussagen).Single(a => a.Text == text).Wahr;

        /// <summary>Urteilt ueber die laufende Aussage dieses Halters - richtig oder falsch -, mit Abstand, damit die Grenze nicht greift.</summary>
        public MinigameSubmitResult Urteile(
            MinigameState zustand, Player p, bool richtig, bool warte = true, IReadOnlyList<MinispielZeile>? inhalt = null)
        {
            if (warte)
                Uhr.Vor(TimeSpan.FromMilliseconds(800));

            var info = Art(zustand, p);
            Assert.IsNotNull(info.StatementText, $"{p.Designation} hat keine Aussage vor sich - der Test urteilte ins Leere.");
            return Gib(zustand, p, new { statementNumber = info.StatementNumber, answer = Wahr(info.StatementText, inhalt) == richtig });
        }

        /// <summary>Der Halter gibt weiter, mit der Nummer seines Zustands.</summary>
        public MinigameSubmitResult GibWeiter(MinigameState zustand, Player von, Player an)
            => Gib(zustand, von, new { statementNumber = Art(zustand, von).StatementNumber, passTo = an.Id });

        /// <summary>Der ganze Zustand, ohne den Durchgang - der ist je Instanz zufaellig und traegt nichts.</summary>
        public static string Json(MinigameState zustand, Player? p)
        {
            var info = (MinigameClientInfo)zustand.InfoFor(p);
            info.Durchgang = 0;
            return JsonSerializer.Serialize(info, info.GetType());
        }
    }
}
