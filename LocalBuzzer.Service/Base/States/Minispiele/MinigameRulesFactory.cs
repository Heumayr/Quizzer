using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was die Regeln einer Art vom Rahmen brauchen.</summary>
    /// <param name="Zufall">Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</param>
    /// <param name="NameVon">Anzeigename zu einer Spieler-Id.</param>
    /// <param name="Mitspieler">Die Mitspieler des laufenden Spiels - gegen sie wird jede Spielerwahl geprueft (E4).</param>
    /// <param name="Uhr">Die Serveruhr, monoton (M35 C4) - sie entscheidet alles Zeitkritische; eigene nur fuer Tests.</param>
    public sealed record MinigameRulesContext(
        Func<int, int, int> Zufall, Func<Guid, string?> NameVon, Func<IReadOnlyList<Player>> Mitspieler,
        Func<TimeSpan>? Uhr = null)
    {
        /// <summary>Jetzt an der Serveruhr - ohne eigene Uhr die des Systems.</summary>
        public TimeSpan Jetzt() => Uhr?.Invoke() ?? TimeProvider.System.GetElapsedTime(0);
    }

    /// <summary>Die Regeln zur Art - die einzige Stelle am Server, die nach der Art verzweigt; Sonderregeln (M35 C3) eigens.</summary>
    internal static class MinigameRulesFactory
    {
        public static IMinigameRules? For(MinigameSetup setup, MinigameRulesContext kontext)
            => setup.SpecialRule != CellSpecialRule.None ? Sonderregeln.SpecialRuleRulesFactory.For(setup, kontext) : setup.Kind switch
        {
            MinigameKind.Reaction => new ReactionRules(kontext.Zufall, kontext.NameVon),
            MinigameKind.TapSprint => new TapSprintRules(setup.DurationSeconds, kontext.NameVon),
            MinigameKind.Stopwatch => new StopwatchRules(setup.DurationSeconds, kontext.NameVon),
            MinigameKind.Duel => new DuelRules(setup, kontext),
            MinigameKind.RobberAndGuard => new RobberRules(kontext),
            MinigameKind.ShareOrTake => new ShareOrTakeRules(setup, kontext),
            MinigameKind.Crash => new CrashRules(setup, kontext),
            MinigameKind.WheelOfFortune => new WheelOfFortuneRules(setup, kontext),
            MinigameKind.MentalArithmetic => new MentalArithmeticRules(setup, kontext),
            MinigameKind.Memory => new MemoryRules(setup, kontext),
            MinigameKind.LightningRound => new LightningRoundRules(setup, kontext),
            MinigameKind.WordScramble => new WordScrambleRules(setup, kontext),
            MinigameKind.ExplainIt => new ExplainItRules(setup, kontext),
            MinigameKind.Bluff => new BluffRules(setup, kontext),
            _ => null,
        };
    }
}
