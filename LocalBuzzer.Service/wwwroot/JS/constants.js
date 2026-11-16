export const Layouts = Object.freeze({
    None: 0,
    Buzzer: 1,
    KeySelect: 2,
    Input: 3,
    Minigame: 4
});

// M35 B3: die Zahlen von MinigameKind, MinigamePhase und ReactionOutcome am Server.
export const MinigameKinds = Object.freeze({
    Majority: 1,
    Reaction: 2,
    TapSprint: 3,
    Stopwatch: 4,
    Duel: 5,
    RobberAndGuard: 6,
    ShareOrTake: 7
});

export const MinigamePhases = Object.freeze({
    Waiting: 0,
    Running: 1,
    RoundOver: 2,
    Finished: 3
});

// M35 C3: die Zahlen von CellSpecialRule am Server - die Vorphase einer Sonderregel laeuft im Minispiel-Rahmen.
export const SpecialRules = Object.freeze({
    None: 0,
    Stake: 1,
    Risk: 2,
    Auction: 3,
    Bet: 4
});

export const ReactionOutcomes = Object.freeze({
    Valid: 1,
    FalseStart: 2,
    TooSlow: 3
});

// M35 C2: die Zuege von SchereSteinPapier und die Disziplinen des Duells (DuellEvaluator) am Server.
export const SchereSteinPapier = Object.freeze({
    Schere: 1,
    Stein: 2,
    Papier: 3
});

export const DuelDisciplines = Object.freeze({
    RockPaperScissors: 0,
    Reaction: 1
});

// Seit der Sicherheitspruefung 2026-10-21 der Zugangsschluessel, nicht mehr die Spieler-Id.
// Neuer Name, damit ein altes Cookie mit einer Id einfach nicht mehr gelesen wird.
export const COOKIE_NAME = "zugang";
export const COOKIE_DAYS = 365;
