using LocalBuzzer.Service.Base.States.Minispiele;
using Quizzer.Base;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.DataModels.Questions.Sonderregeln;
using Quizzer.Views.GameViews.Sonderregeln;
using Quizzer.Views.GameViews.Sub;
using Quizzer.Views.StaticRessources;
using System.Windows;
using System.Windows.Input;

namespace Quizzer.Views.GameViews
{
    /// <summary>
    /// Sonderregel der Zelle (M35 C3): erst die Vorphase am Telefon (im Minispiel-Rahmen), dann die
    /// gewoehnliche Frage; danach rechnet die Regel die Bewertung auf der Spielerkarte um (E1, E2). Was eine
    /// Regel ausmacht, steht in ihrer Strategie unter <c>Sonderregeln/</c>.
    /// </summary>
    public partial class CurrentQuestionViewModel
    {
        /// <summary>Titel, Regeltext und Stand der Vorphase - fuer Beamer und Spielleiter dasselbe Objekt.</summary>
        public SonderregelAnzeige Sonderregel { get; } = new();

        private SonderregelAblauf? sonderregel;
        private bool vorphaseGespielt;

        /// <summary>Die Vorphase laeuft - die Telefone zeigen sie, die Frage kommt danach.</summary>
        internal bool VorphaseLaeuft { get; private set; }

        private AsyncRelayCommand? closeVorphaseCommand;

        public ICommand CloseVorphaseCommand
            => closeVorphaseCommand ??= new AsyncRelayCommand(_ => SchliesseVorphaseAsync());

        /// <summary>
        /// Der Schritt nach jeder Bewertung (M35 C3b): die Regel bekommt die Bewertungen <b>aller</b> Karten der
        /// Zelle und legt je Karte ihren Vorschlag ab (E1) - Wette und Kopfgeld haengen an fremden Bewertungen.
        /// </summary>
        internal void WerteSonderregel()
        {
            if (ZellenWertung() is not { } werte || PlayersResultViewModel?.PlayerResultContextList is not { } karten)
                return;

            var eingaenge = karten.Where(k => k.Player != null).ToDictionary(k => k.Player.Id, k => k.SonderregelEingang);
            var vorschlaege = werte(eingaenge);

            foreach (var karte in karten.Where(k => k.Player != null))
                karte.UebernimmSonderregel(vorschlaege.GetValueOrDefault(karte.Player.Id));
        }

        /// <summary>Setzt die Regel dieser Zelle - aus der Vorphase oder, im Test, von aussen.</summary>
        internal void SetzeSonderregel(SonderregelAblauf regel)
        {
            sonderregel = regel;
            vorphaseGespielt = true;
        }

        /// <summary>Was der Spieler in der Vorphase abgegeben hat - fuer seine Karte.</summary>
        internal string SonderregelAbgabe(Guid spieler)
            => Teamregel is { } team ? TeamregelAbgabe(team, spieler) : sonderregel?.Abgabe(spieler) ?? joker?.Abgabe(spieler) ?? string.Empty;

        /// <summary>
        /// Startet die Vorphase, wenn die Zelle eine gespielte Regel traegt. <c>true</c>: die Frage geht
        /// erst nach der Vorphase auf die Telefone. Eine gespielte Zelle spielt keine Vorphase mehr. Eine Regel
        /// ohne Vorphase (Kopfgeld, M35 C3b) steht sofort, und die Frage geht gleich auf die Telefone.
        /// </summary>
        private async Task<bool> StarteVorphaseAsync()
        {
            if (vorphaseGespielt || Coordinate is not { IsDone: false } zelle || BuzzerControlsViewModel == null
                || !SpecialRuleProfiles.Applies(zelle, Question, Teamspiel) || SonderregelFabrik(zelle.SpecialRule) is not { } regel)
                return false;

            SetzeSonderregel(regel);
            var grundlage = Grundlage();
            regel.Oeffne(grundlage);

            if (!regel.HatVorphase)
            {
                regel.Schliesse(new Dictionary<Guid, object>());
                ZeigeVorphase();

                foreach (var karte in PlayersResultViewModel?.PlayerResultContextList ?? [])
                    karte.SonderregelGeaendert();

                WerteSonderregel();
                return false;
            }

            VorphaseLaeuft = true;

            BuzzerControlsViewModel.SetMinigameInfo(regel.Setup(grundlage));
            BuzzerControlsViewModel.MinigameRoundClosed = OnVorphaseZuAsync;
            BuzzerControlsViewModel.MinigameSubmitted = OnVorphaseAbgabeAsync;
            ZeigeVorphase();

            await BuzzerControlsViewModel.ResetRoundAsync(BuzzerControlsLayout.Minigame);
            await BuzzerControlsViewModel.StartMinigameRoundAsync();

            // Erst jetzt: das Ausspielen oben setzt selbst zurueck.
            BuzzerControlsViewModel.MinigameReset = OnVorphaseReset;
            OnPropertyChanged(nameof(CurrentBuzzerLayout));
            return true;
        }

        private Func<CellSpecialRule, SonderregelAblauf?>? sonderregelFabrik;

        /// <summary>Woher die Regel zur Zelle kommt - nach dem Spiel mit oder ohne Teams (M37 E4); ein Test setzt eine eigene ein.</summary>
        internal Func<CellSpecialRule, SonderregelAblauf?> SonderregelFabrik
        {
            get => sonderregelFabrik ?? (regel => SonderregelAblauf.Fuer(regel, Teamspiel));
            set => sonderregelFabrik = value;
        }

        private VorphaseGrundlage Grundlage()
        {
            var karten = PlayersResultViewModel?.PlayerResultContextList ?? [];

            return new(Coordinate?.CurrentPoints ?? 0,
                karten.ToDictionary(k => k.Player.Id, k => k.Kontostand),
                karten.ToDictionary(k => k.Player.Id, k => k.Player.CalculatedDisplayName),
                Ausloeser,
                Coordinate is { } zelle ? SpecialRuleProfiles.ShareOf(zelle) : 0,
                Coordinate?.SpecialRuleVariant ?? 0,
                karten.Select(k => k.Player.Id).ToList(),
                Teamspiel ? TeamaufstellungDerZelle(karten) : null)
            {
                Minuspunkte = Coordinate?.CurrentMinusPoints ?? 0,
            };
        }

        /// <summary>Der Spielleiter schliesst die Vorphase (E1) - wer nichts abgegeben hat, fuer den greift die Regel nicht.</summary>
        internal async Task SchliesseVorphaseAsync()
        {
            if (VorphaseLaeuft && BuzzerControlsViewModel != null)
                await BuzzerControlsViewModel.CloseRoundAsync(null);
        }

        /// <summary>Die Vorphase ist zu: Abgaben merken, Karten zeigen sie, dann die gewoehnliche Frage.</summary>
        internal async Task OnVorphaseZuAsync(MinigameRoundClosed runde)
        {
            if (sonderregel == null || !VorphaseLaeuft)
                return;

            sonderregel.Schliesse(runde.Submissions);
            VorphaseLaeuft = false;
            LoeseVorphase();
            ZeigeVorphase(runde.Submissions.Count);

            foreach (var karte in PlayersResultViewModel?.PlayerResultContextList ?? [])
                karte.SonderregelGeaendert();

            // Was schon ohne Bewertung feststeht (eine verlorene Wette), steht gleich auf den Karten.
            WerteSonderregel();

            OnPropertyChanged(nameof(CurrentBuzzerLayout));
            OnPropertyChanged(nameof(MinispielSteuerungVisibility));
            KandidatenGeaendert();

            await PrepareBuzzerlayoutAsync();
        }

        /// <summary>Gleichstand beim Experten (M35 C3b): unter diesen waehlt der Spielleiter, wer antwortet (E1).</summary>
        public IReadOnlyList<Player> SonderregelKandidaten
            => sonderregel is { VorphaseZu: true, Kandidaten.Count: > 1 } regel
                ? regel.Kandidaten.Select(SpielerVon).OfType<Player>().ToList()
                : [];

        public Visibility SonderregelKandidatVisibility => SonderregelKandidaten.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        public Player? GewaehlterKandidat
        {
            get => sonderregel?.Antwortende is { Count: 1 } a ? SonderregelKandidaten.FirstOrDefault(p => p.Id == a.First()) : null;
            set
            {
                if (value != null)
                    _ = WaehleKandidatSicherAsync(value.Id);
            }
        }

        /// <summary>Der Spielleiter waehlt unter den Gleichauf: Sperre am Server, Beamer und Karten folgen.</summary>
        internal async Task WaehleKandidatAsync(Guid spieler)
        {
            if (sonderregel == null || !sonderregel.WaehleKandidat(spieler))
                return;

            await SetzeAntwortendeAsync();
            Sonderregel.Zeige(sonderregel.Titel, sonderregel.Regeltext, Sonderregel.Stand, VorphaseLaeuft, sonderregel.Hinweis);

            foreach (var karte in PlayersResultViewModel?.PlayerResultContextList ?? [])
                karte.SonderregelGeaendert();

            WerteSonderregel();
            OnPropertyChanged(nameof(GewaehlterKandidat));
        }

        private async Task WaehleKandidatSicherAsync(Guid spieler)
        {
            try
            {
                await WaehleKandidatAsync(spieler);
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        private Player? SpielerVon(Guid id)
            => PlayersResultViewModel?.PlayerResultContextList.FirstOrDefault(k => k.Player?.Id == id)?.Player;

        private void KandidatenGeaendert()
        {
            OnPropertyChanged(nameof(SonderregelKandidaten));
            OnPropertyChanged(nameof(SonderregelKandidatVisibility));
            OnPropertyChanged(nameof(GewaehlterKandidat));
        }

        private Task OnVorphaseAbgabeAsync(MinigameSubmission abgabe)
        {
            ZeigeVorphase(BuzzerControlsViewModel?.MinigameState?.Rules?.CurrentSubmissions.Count ?? 0);
            return Task.CompletedTask;
        }

        /// <summary>„Runde zuruecksetzen" in der Vorphase: sie beginnt neu, eine Abgabe davor zaehlt nicht.</summary>
        private void OnVorphaseReset()
        {
            _ = NeustartVorphaseAsync();
        }

        private async Task NeustartVorphaseAsync()
        {
            try
            {
                if (VorphaseLaeuft && BuzzerControlsViewModel != null)
                    await BuzzerControlsViewModel.StartMinigameRoundAsync();

                ZeigeVorphase();
            }
            catch (Exception ex)
            {
                ExceptionManager.HandleException(ex);
            }
        }

        private void ZeigeVorphase(int abgegeben = 0)
        {
            if (sonderregel == null)
                return;

            var gesamt = PlayersResultViewModel?.PlayerResultContextList.Count ?? 0;
            var stand = !sonderregel.HatVorphase ? "Ohne Vorphase"
                : VorphaseLaeuft ? $"Vorphase: {abgegeben} von {gesamt} haben abgegeben"
                : $"Vorphase zu: {abgegeben} von {gesamt} haben abgegeben";

            Sonderregel.Zeige(sonderregel.Titel, sonderregel.Regeltext, stand, VorphaseLaeuft, sonderregel.Hinweis);
        }

        private void LoeseVorphase()
        {
            if (BuzzerControlsViewModel == null)
                return;

            BuzzerControlsViewModel.MinigameRoundClosed = null;
            BuzzerControlsViewModel.MinigameSubmitted = null;
            BuzzerControlsViewModel.MinigameReset = null;
        }

        /// <summary>Nach der Vorphase: nur, wen die Regel nennt, darf antworten (M35 C3b) - die anderen Telefone sind gesperrt.</summary>
        private async Task SetzeAntwortendeAsync()
        {
            if (sonderregel != null && BuzzerControlsViewModel != null)
                await BuzzerControlsViewModel.SetAnswerersAsync(sonderregel.Antwortende, sonderregel.Sperrsaetze);
        }

        private void ClearSonderregel()
        {
            if (VorphaseLaeuft)
                LoeseVorphase();

            // Die Sperre gilt nur dieser Frage - das Zuruecksetzen danach schickt es an die Telefone.
            if (sonderregel != null)
                BuzzerControlsViewModel?.ClearAnswerers();

            VorphaseLaeuft = false;
            Sonderregel.Leere();
            LeereFinale();
            LeereGleichGedacht();
            LeereTeamAbstimmung();
            KandidatenGeaendert();
        }
    }
}
