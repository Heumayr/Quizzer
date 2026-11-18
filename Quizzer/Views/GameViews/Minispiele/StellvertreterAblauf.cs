using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Questions.Minispiele;

namespace Quizzer.Views.GameViews.Minispiele
{
    /// <summary>
    /// Stellvertreter-Duell im Fragefenster (M37 T10): erst stehen am Beamer die Teams und wen sie schicken - gelesen am Server
    /// unter der Sperre des Rahmens, damit Beamer und Telefone dasselbe zeigen -, dann zählen die Runden der Stellvertreter.
    /// Das Team des Siegers bekommt die Feldpunkte aufgeteilt als Vorschlag (E1, F-M37-06), die anderen nichts. Wählt ein
    /// Team nicht, lost der Spielleiter (<see cref="CurrentQuestionViewModel.LoseStellvertreterAsync"/>).
    /// </summary>
    internal sealed class StellvertreterAblauf : MinispielAblauf
    {
        /// <summary>Die gezählten Runden; eine Wiederholung ersetzt ihre Runde.</summary>
        private readonly SortedDictionary<int, IReadOnlyDictionary<Guid, object>> runden = new();

        /// <summary>Stellvertreter und Disziplin, wie der Server sie nach der Wahl hielt - gewertet wird damit.</summary>
        private Lage? gewaehlt;

        public StellvertreterAblauf(CurrentQuestionViewModel fenster)
            : base(fenster, MinigameKind.ProxyDuel)
        {
        }

        private sealed record Lage(IReadOnlyList<Guid> Teams, IReadOnlyList<Stellvertreter> Vertreter, bool WahlOffen, int Disziplin);

        /// <summary>Was der Server gerade hält; <c>null</c> ohne Stellvertreter-Duell.</summary>
        private Lage? Server => Zustand?.Lenke(r => r is ProxyDuelRules p ? new Lage(p.Teams, p.Vertreter, p.WahlOffen, p.Disziplin) : null);

        /// <summary>Ob in der laufenden Runde noch ein Team wählt - dann kann der Spielleiter auslosen.</summary>
        internal bool WahlOffen => Zustand?.Phase == MinigamePhase.Running && Server?.WahlOffen == true;

        public override void ZeigeLaeuft()
        {
            Fenster.StellvertreterGeaendert();

            if (Server is not { Teams.Count: > 0 } lage)
            {
                Anzeige.Zeige("Stellvertreter-Duell – kein Team war beim Start verbunden");
                return;
            }

            var tafel = StellvertreterTafel.Wahl(lage.Teams, lage.Vertreter, Teamname, Name);

            if (lage.WahlOffen)
            {
                Anzeige.Zeige("Stellvertreter-Duell – die Teams wählen, wer für sie spielt …", tafel);
                return;
            }

            var paar = string.Join(" gegen ", lage.Vertreter.Select(v => Name(v.SpielerId)));
            var was = lage.Disziplin == DuellEvaluator.SchereSteinPapierVariant ? "Schere, Stein oder Papier – geheim!" : "wartet auf Grün!";

            Anzeige.Zeige($"Runde {Zustand?.Round ?? 0} von {Zustand?.Setup.TotalRounds ?? 0} – {paar}: {was}", tafel);
        }

        /// <summary>Ein Team hat gewählt oder gelost: Ton und die Wahl an den Beamer.</summary>
        public override void Abgabe(MinigameSubmission abgabe)
        {
            if (abgabe.Value is not (ProxyChosen or ProxiesComplete))
                return;

            Klang.Tick();
            ZeigeLaeuft();
        }

        public override void RundeZu(MinigameRoundClosed runde)
        {
            if (!runde.Aborted)
                runden[runde.Round] = runde.Submissions;

            if (Server is { WahlOffen: false, Vertreter.Count: > 0 } lage)
                gewaehlt = lage;

            var ergebnis = Werte();
            Fenster.StellvertreterGeaendert();

            if (ergebnis == null)
                Anzeige.Zeige("Stellvertreter-Duell abgebrochen – es stand noch nicht fest, wer spielt");
            else if (runde.Aborted || runde.Finished)
                ZeigeEnde(ergebnis, runde.Aborted);
            else
                Anzeige.Zeige($"Runde {runde.Round} von {runde.TotalRounds} – " + Rundensatz(ergebnis, runden[runde.Round]),
                    StellvertreterTafel.Runde(ergebnis, runden[runde.Round], Teamname, Name));
        }

        public override void Wiederhole(int runde)
        {
            runden.Remove(runde);
            Werte();
        }

        /// <summary>„Runde zurücksetzen“: am Server wird neu gewählt - hier vergessen Runden und Stellvertreter, auch die Vorschläge.</summary>
        public override void SetzeZurueck()
        {
            runden.Clear();
            gewaehlt = null;
            Werte();
            Fenster.StellvertreterGeaendert();
        }

        private void ZeigeEnde(StellvertreterErgebnis ergebnis, bool abgebrochen)
        {
            var sieger = ergebnis.Vertreter.Where(v => ergebnis.Sieger.Contains(v.SpielerId)).ToList();
            var ausgang = sieger.Count switch
            {
                0 when ergebnis.Vertreter.Count < 2 => "ohne Gegner – keine Punkte",
                0 when ergebnis.IstSchereSteinPapier => "Unentschieden – keine Punkte",
                0 => "keine gültige Zeit – keine Punkte",
                1 => $"{Name(sieger[0].SpielerId)} gewinnt für {Teamname(sieger[0].TeamId)}",
                _ => "Gleichstand – die Teams vorn gewinnen",
            };

            Anzeige.Zeige((abgebrochen ? "Stellvertreter-Duell abgebrochen – " : "Ergebnis – ") + ausgang,
                StellvertreterTafel.Stand(ergebnis, Teamname, Name, ende: true));
        }

        private string Rundensatz(StellvertreterErgebnis ergebnis, IReadOnlyDictionary<Guid, object> abgaben)
        {
            if (ergebnis is not { IstSchereSteinPapier: true, Duell: { } duell, Vertreter: [var a, var b] })
                return "die Zeiten";

            var sieger = DuellEvaluator.RundenSieger(a.SpielerId, b.SpielerId, Zuege(abgaben));

            return (sieger is { } s ? $"{Name(s)} gewinnt die Runde" : "Gleichstand") + $" · Stand {duell.Stand}";
        }

        /// <summary>Rechnet den Stand und legt die Vorschläge auf die Karten; <c>null</c>, solange niemand gewählt ist.</summary>
        private StellvertreterErgebnis? Werte()
        {
            if (gewaehlt is not { } lage)
            {
                Karten?.WerteStellvertreter(null, Teamname);
                return null;
            }

            var ergebnis = lage.Disziplin == DuellEvaluator.SchereSteinPapierVariant
                ? StellvertreterEvaluator.WerteSchereSteinPapier(lage.Vertreter, runden.Values.Select(Zuege))
                : StellvertreterEvaluator.WerteReaktion(lage.Vertreter, runden.Values.Select(Reaktionen));

            Karten?.WerteStellvertreter(runden.Count == 0 ? null : ergebnis, Teamname);
            return ergebnis;
        }

        /// <summary>Symbol und Name aus dem Spielaufbau, wie Beamer und Karten das Team kennen.</summary>
        private string Teamname(Guid teamId) => TauziehenAblauf.TeamTitel(Fenster.Coordinate?.Game?.GameTeams.FirstOrDefault(t => t.Id == teamId));

        private static IReadOnlyDictionary<Guid, SchereSteinPapier> Zuege(IReadOnlyDictionary<Guid, object> abgaben)
            => abgaben.Where(a => a.Value is SchereSteinPapier).ToDictionary(a => a.Key, a => (SchereSteinPapier)a.Value);

        private static IReadOnlyDictionary<Guid, ReactionAttempt> Reaktionen(IReadOnlyDictionary<Guid, object> abgaben)
            => abgaben.Where(a => a.Value is ReactionAttempt).ToDictionary(a => a.Key, a => (ReactionAttempt)a.Value);
    }
}
