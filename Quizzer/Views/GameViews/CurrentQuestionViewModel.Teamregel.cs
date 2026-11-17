using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.DataModels.Questions.Teams;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.GameViews.Teamregeln;
using Quizzer.Views.StaticRessources;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Teamregeln im Fragefenster (M37 T5, Entwurf Abschnitte 1, 4 und 7): eine Teamregel ist ein Wert von
    /// <c>CellSpecialRule</c> und läuft über denselben Ablauf wie eine Sonderregel - nur im Teamspiel, ohne Vorphase. Die
    /// Joker-Runde darf davor laufen; danach stehen die Sprecher fest (Gesperrte und Getrennte übersprungen), gehen an Hub und
    /// Telefone, und die Beratungszeit läuft. Der Sprecher antwortet per Tastenwahl, wo die Frage Tasten hat, sonst per Text.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Platzhalter der Texteingabe am Telefon des Sprechers.</summary>
        internal const string TeamregelEingabe = "Antwort eures Teams";

        private const string TeamregelBereich = "Teamregel";

        /// <summary>Ob das Spiel dieser Zelle ein Teamspiel ist - danach richtet sich, welche Regeln gespielt werden.</summary>
        private bool Teamspiel => Coordinate?.Game?.IsTeamGame == true;

        /// <summary>Die Teamregel dieser Zelle, sobald sie steht; <c>null</c> ohne.</summary>
        private TeamregelAblauf? Teamregel => sonderregel as TeamregelAblauf;

        private bool teamregelVorbereitet;

        /// <summary>Ob an dieser Zelle eine Teamregel gespielt wurde - nach ihrer Wertung spricht im Team der Nächste.</summary>
        internal bool TeamregelGespielt => teamregelVorbereitet;

        private IEnumerable<PlayerResultContext> Karten => (PlayersResultViewModel?.PlayerResultContextList ?? []).Where(k => k.Player != null);

        /// <summary>Was die Frage auf die Telefone bringt: an einer Teamregel antwortet der Sprecher per Tastenwahl oder Text.</summary>
        private BuzzerControlsLayout AntwortLayout
        {
            get
            {
                var layout = Question?.BuzzerControlsLayout ?? BuzzerControlsLayout.None;

                return Teamregel == null || layout is BuzzerControlsLayout.KeySelect or BuzzerControlsLayout.Input
                    ? layout
                    : BuzzerControlsLayout.Input;
            }
        }

        /// <summary>
        /// Die Teams der Karten - nach dem Stempel wie die Teamsumme (F-M37-07), je Team die Sprecherfolge ab dessen Stand. Wer
        /// ohne Team ist, ist sein eigenes Team (fail-closed, wie die Team-Sperre am Hub).
        /// </summary>
        private IReadOnlyList<Teamaufstellung> TeamaufstellungDerZelle(IEnumerable<PlayerResultContext> karten)
        {
            if (Coordinate?.Game is not { } spiel)
                return [];

            return karten.Where(k => k.Player != null)
                .GroupBy(k => k.ZaehltFuerTeam(spiel) ?? k.Player.Id)
                .Select(g => Aufstellung(spiel, g.Key, g.ToList()))
                .OrderBy(t => spiel.GameTeams.FirstOrDefault(x => x.Id == t.TeamId)?.SortOrder ?? int.MaxValue)
                .ToList();
        }

        private static Teamaufstellung Aufstellung(Game spiel, Guid schluessel, IReadOnlyList<PlayerResultContext> karten)
        {
            var team = spiel.GameTeams.FirstOrDefault(t => t.Id == schluessel);
            var farbe = team != null && TeamFarben.IsValid(team.ColorIndex) ? TeamFarben.Alle[team.ColorIndex] : null;
            var mitglieder = karten.Select(k => new Teammitglied(k.Player.Id, k.Player.CalculatedDisplayName));

            return new(schluessel, team?.Name ?? "ohne Team", farbe?.Symbol ?? "–", Sprecherfolge.Folge(mitglieder, team?.SpeakerTurn ?? 0));
        }

        /// <summary>
        /// Nach der Joker-Runde, vor der Frage - einmal je Zelle: wer getrennt oder per Joker gesperrt ist, spricht nicht; die
        /// Sprecher gehen an die Telefone, und die Beratungszeit läuft.
        /// </summary>
        private async Task BereiteTeamregelVorAsync()
        {
            if (teamregelVorbereitet || Teamregel is not { } regel || Coordinate is not { IsDone: false })
                return;

            teamregelVorbereitet = true;

            foreach (var karte in Karten.Where(k => k.Player.ConnectionState == PlayerConnection.Disconnected))
                regel.LasseAus(karte.Player.Id);

            if (joker is { Zu: true } ablauf)
            {
                foreach (var (gesperrt, satz) in ablauf.Sperrsaetze)
                    regel.LasseAus(gesperrt, satz);
            }

            await TeamregelAnwendenAsync(regel);
            StarteBeratungszeit();
        }

        /// <summary>Beamer, Karten und Telefone folgen den Sprechern dieser Frage.</summary>
        private async Task TeamregelAnwendenAsync(TeamregelAblauf regel)
        {
            ZeigeTeamregel(regel);

            foreach (var karte in Karten)
                karte.SonderregelGeaendert();

            WerteSonderregel();
            await StaticManager.BuzzerServerViewModel.SendTeamsAsync(regel.Sprecher);
        }

        private void ZeigeTeamregel(TeamregelAblauf regel)
        {
            var sekunden = Coordinate?.Game?.TeamCouncilSeconds ?? 0;
            var stand = regel is TeamberatungAblauf && sekunden > 0
                ? $"Beratung: {sekunden} s – nur die Sprecher antworten"
                : "Nur die Sprecher antworten";
            var hinweis = joker is { Zu: true, Hinweis.Length: > 0 } ablauf ? $"{regel.Hinweis} · {ablauf.Hinweis}" : regel.Hinweis;

            Sonderregel.Zeige(regel.Titel, regel.Regeltext, stand, false, hinweis, TeamregelBereich);
        }

        /// <summary>Die Beratungszeit des Spiels (0: ohne). Läuft sie ab, nehmen die Telefone nichts mehr an - wie beim Countdown von Hand.</summary>
        private void StarteBeratungszeit()
        {
            if (Teamregel is not TeamberatungAblauf || Coordinate?.Game is not { TeamCouncilSeconds: > 0 } spiel)
                return;

            var buzzer = BuzzerControlsViewModel;
            Countdown?.Starte(Math.Min(spiel.TeamCouncilSeconds, TeamGrenzen.MaxCouncilSeconds), () => _ = buzzer?.SperreWegenZeitAsync());
        }

        /// <summary>„Joker-Sperre aufheben" an einer Teamregel-Zelle (E1): wer gesperrt war, spricht wieder, wenn er dran ist.</summary>
        private async Task HebeTeamregelJokerSperreAufAsync()
        {
            if (Teamregel is not { } regel || joker == null)
                return;

            regel.NimmAuslassungZurueck(joker.Gesperrte);
            await TeamregelAnwendenAsync(regel);
            await SetzeAntwortendeAsync();
        }

        /// <summary>Die Karte an einer Teamregel-Zelle: wer spricht, und der eigene Joker oder die Sperre per Joker dazu.</summary>
        private string TeamregelAbgabe(TeamregelAblauf regel, Guid spieler)
            => string.Join(" · ", new[] { regel.Abgabe(spieler), joker?.Abgabe(spieler) ?? string.Empty }.Where(t => t.Length > 0));

        /// <summary>Die Frage ist zu: die Telefone zeigen wieder den Sprecher nach dem Stand - der Sprecher dieser Frage galt nur ihr.</summary>
        private async Task RaeumeTeamregelAufAsync()
        {
            if (teamregelVorbereitet)
                await StaticManager.BuzzerServerViewModel.SendTeamsAsync();
        }

        /// <summary>
        /// Was auf den Karten wirkt: eine Teamregel und danach der Joker auf den eigenen Anteil (M37 T5); sonst die Sonderregel
        /// nach ihrer Vorphase, sonst die Joker nach ihrer Runde.
        /// </summary>
        private Func<IReadOnlyDictionary<Guid, SonderregelEingang>, IReadOnlyDictionary<Guid, Punktevorschlag>>? ZellenWertung()
            => Teamregel is { VorphaseZu: true } team ? e => team.WerteMitJoker(e, joker is { Zu: true } offen ? offen.Werte : null)
                : sonderregel is { VorphaseZu: true } regel ? regel.WerteAlle
                : joker is { Zu: true } ablauf ? ablauf.WerteAlle
                : null;
    }
}
