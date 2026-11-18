using LocalBuzzer.Service.Base;
using LocalBuzzer.Service.Base.States;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;
using Quizzer.DataModels.Models.QuestionTypes;
using Quizzer.DataModels.Questions.Minispiele;
using Quizzer.Logic.Controller.TypedControllers;
using Quizzer.UnitTests.PlayThrough;
using Quizzer.Views.GameViews;
using PrePhase = LocalBuzzer.Service.Base.States.Joker.MinigamePrePhase;

namespace Quizzer.UnitTests.Views.GameViews
{
    /// <summary>
    /// Gemeinsamer Aufbau der Teamregel-Tests mit echtem Server (M37 T6): zwei Teams in der Datenbank, die Plätze zugeteilt -
    /// das Öffnen der Zelle stempelt -, auf Wunsch ein Joker. Rot sind die Plätze in <see cref="rotePlaetze"/>, die übrigen Blau.
    /// </summary>
    public abstract class TeamregelServerTestBasis : SonderregelServerTestBasis
    {
        protected GameTeam rot = null!;
        protected GameTeam blau = null!;

        protected int[] rotePlaetze = [0, 1];

        protected (int Spieler, JokerKind Art)? jokerFuer;

        protected PlayerJoker? joker;

        private async Task TeamsAnlegenAsync(TestGameBuilder welt)
        {
            // Nach Kennung liegt Blau vorn, nach SortOrder Rot - eine Reihenfolge nach Kennung fällt so auf (V27).
            var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
            rot = new GameTeam { Id = a.CompareTo(b) > 0 ? a : b, GameId = welt.Game.Id, Name = "Rot", ColorIndex = 5, SortOrder = 0 };
            blau = new GameTeam { Id = a.CompareTo(b) > 0 ? b : a, GameId = welt.Game.Id, Name = "Blau", ColorIndex = 4, SortOrder = 1 };

            using (var ctrl = new GameTeamsController())
            {
                await ctrl.InsertAsync(rot);
                await ctrl.InsertAsync(blau);
                await ctrl.SaveChangesAsync();
            }

            welt.Game.IsTeamGame = true;
            welt.Game.GameTeams.AddRange([rot, blau]);

            await using var context = new Quizzer.Logic.Context.DataContext();

            for (var i = 0; i < welt.Players.Count; i++)
            {
                var team = rotePlaetze.Contains(i) ? rot : blau;
                welt.Game.PlayerXGames.Single(x => x.PlayerId == welt.Players[i].Id).GameTeamId = team.Id;
                context.PlayerXGames.Single(x => x.GameId == welt.Game.Id && x.PlayerId == welt.Players[i].Id).GameTeamId = team.Id;
            }

            await context.SaveChangesAsync();

            if (jokerFuer is { } wer)
            {
                using var ctrl = new PlayerJokersController();
                joker = await ctrl.GrantAsync(welt.Game.Id, welt.Players[wer.Spieler].Id, wer.Art, "verdient", DateTimeOffset.Now);
                await ctrl.SaveChangesAsync();
            }
        }

        private protected Task<TestableCurrentQuestionViewModel> OeffneTeamregelAsync(CellSpecialRule regel, int spieler = 4, QuestionType typ = QuestionType.Default)
        {
            VorDemOeffnen = TeamsAnlegenAsync;
            return OeffneAsync(regel, spieler, typ: typ);
        }

        /// <summary>Ein Minispiel im Teamspiel (M37 T9): die Art mit ihren Vorgaben, ohne Regel an der Zelle.</summary>
        private protected Task<TestableCurrentQuestionViewModel> OeffneTeamMinispielAsync(MinigameKind art, Action<MinigameQuestion>? einstellen = null, int spieler = 4)
        {
            FrageEinrichten = frage =>
            {
                MinigameProfiles.For(art).ApplyDefaults((MinigameQuestion)frage);
                einstellen?.Invoke((MinigameQuestion)frage);
            };

            return OeffneTeamregelAsync(CellSpecialRule.None, spieler, QuestionType.Minigame);
        }

        protected LayoutStateManager Hub => serverVm!.BuzzerControlsViewModel!.BuzzerController!.StateManager;

        protected Guid Id(int spieler) => world!.Players[spieler].Id;

        protected string Name(int spieler) => world!.Players[spieler].CalculatedDisplayName;

        /// <summary>Löst in der Joker-Runde einen Joker ein, auf Wunsch gegen ein Ziel.</summary>
        protected Task JokerAsync(int telefon, Guid? jokerId, Guid? ziel = null)
            => phones[telefon].InvokeAsync("SpielAktion", new
            {
                kind = 0,
                specialRule = 0,
                prePhase = (int)PrePhase.Joker,
                durchgang = Zustand.Durchgang,
                payload = new { jokerId, target = ziel },
            });

        /// <summary>Die Joker-Runde ist zu, und die Frage steht mit so vielen Antwortenden an den Telefonen.</summary>
        private protected async Task NachDerJokerRundeAsync(TestableCurrentQuestionViewModel vm, int antwortende)
        {
            Assert.IsTrue(await WarteAsync(() => !vm.VorphaseLaeuft), "Die Joker-Runde schließt nicht.");
            Assert.IsTrue(await WarteAsync(() => Hub.CurrentLayout == BuzzerControlsLayout.Input && Hub.AnswerLock.Answerers?.Count == antwortende),
                $"Nach der Joker-Runde dürfen nicht {antwortende} antworten: {Hub.AnswerLock.Answerers?.Count}.");
        }

        /// <summary>Ein Telefon schreibt; gewartet wird, bis die Antwort auf der Karte steht.</summary>
        private protected async Task SchreibeAsync(TestableCurrentQuestionViewModel vm, int telefon, string text)
        {
            await phones[telefon].InvokeAsync("SubmitInput", new { value = text });
            Assert.IsTrue(await WarteAsync(() => Karte(vm, telefon).InputResult?.Value == BuzzerInputState.Kuerze(text)),
                $"Die Antwort von Platz {telefon} kam nicht auf die Karte.");
        }
    }
}
