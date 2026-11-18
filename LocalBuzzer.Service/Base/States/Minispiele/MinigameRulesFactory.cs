using Quizzer.DataModels.Enumerations;
using Quizzer.DataModels.Models.Base;

namespace LocalBuzzer.Service.Base.States.Minispiele
{
    /// <summary>Was die Regeln einer Art vom Rahmen brauchen.</summary>
    /// <param name="Zufall">Wie <see cref="Random.Next(int, int)"/> - eigene Quelle nur fuer Tests.</param>
    /// <param name="NameVon">Anzeigename zu einer Spieler-Id.</param>
    /// <param name="Mitspieler">Die Mitspieler des laufenden Spiels - gegen sie wird jede Spielerwahl geprueft (E4).</param>
    /// <param name="Uhr">Die Serveruhr, monoton (M35 C4) - sie entscheidet alles Zeitkritische; eigene nur fuer Tests.</param>
    /// <param name="TeamVon">Das Team eines Spielers aus der Aufstellung des Hubs (M37 T4); <c>null</c> ohne Teamspiel oder Team.</param>
    /// <param name="Teamspiel">Ob die Aufstellung des Hubs ein Teamspiel ist (M37 T8) - das Glücksrad dreht dann ohne Robin Hood.</param>
    /// <param name="Team">Der Eintrag der Aufstellung zu einem Spieler - Name, Farbe und Symbol seines Teams (M37 T9, Tauziehen).</param>
    public sealed record MinigameRulesContext(
        Func<int, int, int> Zufall, Func<Guid, string?> NameVon, Func<IReadOnlyList<Player>> Mitspieler,
        Func<TimeSpan>? Uhr = null, Func<Guid, Guid?>? TeamVon = null, Func<bool>? Teamspiel = null,
        Func<Guid, TeamRosterEntry?>? Team = null)
    {
        /// <summary>Ob gerade ein Teamspiel läuft - ohne Angabe nie.</summary>
        public bool IstTeamspiel => Teamspiel?.Invoke() == true;

        /// <summary>Jetzt an der Serveruhr - ohne eigene Uhr die des Systems.</summary>
        public TimeSpan Jetzt() => Uhr?.Invoke() ?? TimeProvider.System.GetElapsedTime(0);

        /// <summary>Ob zwei Spieler im selben Team spielen - ohne Teamspiel nie, ein Spieler ohne Team ist sein eigenes (fail-closed).</summary>
        public bool Teamkollegen(Guid a, Guid b) => TeamVon is { } von && von(a) is { } team && von(b) == team;

        /// <summary>
        /// Ob ein Spieler ein Ziel des anderen sein darf (M37 T8): ein Mitspieler des laufenden Spiels, nicht er selbst - und im
        /// Teamspiel nur ein Gegner. Räuber, Duell, Teilen, Kartoffel und Glücksrad prüfen hier, wie die Joker-Sperre.
        /// </summary>
        public bool IstZiel(Guid wer, Guid ziel) => ziel != wer && Mitspieler().Any(p => p.Id == ziel) && !Teamkollegen(wer, ziel);
    }

    /// <summary>Die Regeln zur Art - die einzige Stelle am Server, die nach der Art verzweigt; Sonderregeln (M35 C3) und Joker-Runde (C7b) eigens.</summary>
    internal static class MinigameRulesFactory
    {
        public static IMinigameRules? For(MinigameSetup setup, MinigameRulesContext kontext)
            => setup.PrePhase == Joker.MinigamePrePhase.Joker ? new Joker.JokerRules(setup, kontext)
            : setup.SpecialRule != CellSpecialRule.None ? Sonderregeln.SpecialRuleRulesFactory.For(setup, kontext) : setup.Kind switch
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
            MinigameKind.HotPotato => new HotPotatoRules(setup, kontext),
            MinigameKind.TugOfWar => new TugOfWarRules(setup, kontext),
            MinigameKind.ProxyDuel => new ProxyDuelRules(setup, kontext),
            _ => null,
        };
    }
}
