using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Staffel im Fragefenster (M37 T11): am Beamer stehen die Teams mit ihren Teilen und wer gerade läuft - gelesen am Server unter
    /// der Sperre des Rahmens, damit Beamer und Telefone denselben Stand zeigen. Ein verfehlter Teil zeigt seine Lösung erst am Ende.
    /// Danach bekommt das Team mit den meisten Teilen die Feldpunkte aufgeteilt als Vorschlag (E1, F-M37-06). Die Spielleitung lässt
    /// eine abgelehnte Antwort gelten oder gibt für das Team am Zug weiter (<see cref="CurrentQuestionViewModel.LasseStaffelAntwortGeltenAsync"/>).
    /// </summary>
    internal sealed class StaffelAblauf : MinispielAblauf
    {
        public StaffelAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.Relay)
        {
        }

        private sealed record Lage(StaffelStand Stand, bool Aufgeloest, bool KannGelten, bool KannWeitergeben);

        /// <summary>Was der Server gerade hält; <c>null</c> ohne Staffel.</summary>
        private Lage? Server => Zustand?.Lenke(r => r is RelayRules s ? new Lage(s.Stand, s.Aufgeloest, s.KannGelten, s.KannWeitergeben) : null);

        /// <summary>Ob die zuletzt abgelehnte Antwort noch gelten kann - der Knopf dazu kommt oder geht.</summary>
        internal bool KannGelten => Server?.KannGelten == true;

        internal bool KannWeitergeben => Server?.KannWeitergeben == true;

        private IReadOnlyList<MinispielZeile> Inhalt => Frage is { } frage ? MinispielInhalt.Zeilen(frage) : [];

        public override void ZeigeLaeuft()
        {
            Fenster.StaffelGeaendert();

            if (Server is not { } lage)
                return;

            var stand = lage.Stand;

            if (stand.Teams.Count == 0)
            {
                Anzeige.Zeige("Staffel – kein Team war beim Start verbunden");
                return;
            }

            var kopf = stand.Laeufer is { } wer && stand.TeamAmZug is { } team
                ? $"Staffel – Teil {stand.Teil + 1} von {stand.Teile}: {Name(wer)} läuft für {Teamname(team)}"
                : "Staffel – alle Teile sind gelaufen";

            Anzeige.Zeige(kopf, [.. StaffelTafel.Teams(stand, Teamname, Name, ende: false), .. StaffelTafel.Gespielt(stand, Inhalt, Teamname, ende: false)]);
        }

        /// <summary>Eine Antwort ist angenommen: Ton beim Treffer, der neue Läufer an den Beamer.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            // Nach dem Schließen steht das Ergebnis am Beamer - eine spät verteilte Antwort darf es nicht überschreiben.
            if (abgabe.Value is not StaffelAntwort antwort || Zustand?.Phase != MinigamePhase.Running)
                return;

            if (antwort.Richtig)
                Klang.Tick();

            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            Fenster.StaffelGeaendert();

            // Ein Abbruch vor dem Schluss: nichts zählt. Nach dem Schluss bleibt die Wertung.
            if (Server is not { Aufgeloest: true })
            {
                Karten?.WerteStaffel(null, Teamname);
                Anzeige.Zeige("Staffel abgebrochen – sie zählt nicht");
                return;
            }

            ZeigeEnde(runde.Aborted);
        }

        /// <summary>Nach einem Eingriff der Spielleitung: laufend die Tafel, nach dem Schluss neu gewertet.</summary>
        internal void NachEingriff()
        {
            if (Zustand?.Phase == MinigamePhase.Running)
                ZeigeLaeuft();
            else if (Server is { Aufgeloest: true })
                ZeigeEnde(Zustand?.Aborted == true);

            Fenster.StaffelGeaendert();
        }

        public override void Wiederhole(int runde) => Vergiss();

        /// <summary>„Runde zurücksetzen“: am Server läuft die Staffel von vorn - hier vergessen Satz und Vorschläge.</summary>
        public override void SetzeZurueck() => Vergiss();

        private void Vergiss()
        {
            Karten?.WerteStaffel(null, Teamname);
            Fenster.StaffelGeaendert();
        }

        private void ZeigeEnde(bool abgebrochen)
        {
            if (Server?.Stand is not { } stand)
                return;

            var ergebnis = StaffelEvaluator.Werte(stand);
            Karten?.WerteStaffel(stand, Teamname);

            var ausgang = ergebnis.Sieger.Count switch
            {
                0 when stand.Teams.Count < 2 => "ohne Gegner – keine Punkte",
                0 => "kein Team hat einen Teil – keine Punkte",
                1 => $"{Teamname(ergebnis.Sieger[0])} gewinnt die Staffel",
                _ => "Gleichstand – die Teams vorn gewinnen",
            };

            Anzeige.Zeige((abgebrochen ? "Staffel abgebrochen – " : "Ergebnis – ") + ausgang,
                [.. StaffelTafel.Teams(stand, Teamname, Name, ende: true), .. StaffelTafel.Gespielt(stand, Inhalt, Teamname, ende: true)]);
        }

        /// <summary>Symbol und Name aus dem Spielaufbau, wie Beamer und Karten das Team kennen.</summary>
        private string Teamname(Guid teamId) => TauziehenAblauf.TeamTitel(Fenster.Coordinate?.Game?.GameTeams.FirstOrDefault(t => t.Id == teamId));
    }
}
