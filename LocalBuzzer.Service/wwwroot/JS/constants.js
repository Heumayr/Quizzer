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
    Stopwatch: 4
});

export const MinigamePhases = Object.freeze({
    Waiting: 0,
    Running: 1,
    RoundOver: 2,
    Finished: 3
});

export const ReactionOutcomes = Object.freeze({
    Valid: 1,
    FalseStart: 2,
    TooSlow: 3
});

// Seit der Sicherheitspruefung 2026-10-21 der Zugangsschluessel, nicht mehr die Spieler-Id.
// Neuer Name, damit ein altes Cookie mit einer Id einfach nicht mehr gelesen wird.
export const COOKIE_NAME = "zugang";
export const COOKIE_DAYS = 365;