using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was die Regeln einer Art vom Rahmen brauchen.</summary>
    /// <param name="Zufall">Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</param>
    /// <param name="NameVon">Anzeigename zu einer Spieler-Id.</param>
    /// <param name="Mitspieler">Die Mitspieler des laufenden Spiels - gegen sie wird jede Spielerwahl geprueft (E4).</param>
    public sealed record MinigameRulesContext(
        Func<int, int, int> Zufall, Func<Guid, string?> NameVon, Func<IReadOnlyList<Player>> Mitspieler);

    /// <summary>Die Regeln zur Art - die einzige Stelle am Server, die nach der Art verzweigt; Sonderregeln (M35 C3) eigens.</summary>
    internal static class MinigameRulesFactory
    {
        public static IMinigameRules? For(MinigameSetup setup, MinigameRulesContext kontext)
            => setup.SpecialRule != CellSpecialRule.None ? Sonderregeln.SpecialRuleRulesFactory.For(setup) : setup.Kind switch
        {
            MinigameKind.Reaction => new ReactionRules(kontext.Zufall, kontext.NameVon),
            MinigameKind.TapSprint => new TapSprintRules(setup.DurationSeconds, kontext.NameVon),
            MinigameKind.Stopwatch => new StopwatchRules(setup.DurationSeconds, kontext.NameVon),
            MinigameKind.Duel => new DuelRules(setup, kontext),
            MinigameKind.RobberAndGuard => new RobberRules(kontext),
            MinigameKind.ShareOrTake => new ShareOrTakeRules(setup, kontext),
            _ => null,
        };
    }
}
