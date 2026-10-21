export const Layouts = Object.freeze({
    None: 0,
    Buzzer: 1,
    KeySelect: 2,
    Input: 3
});

// Seit der Sicherheitspruefung 2026-10-21 der Zugangsschluessel, nicht mehr die Spieler-Id.
// Neuer Name, damit ein altes Cookie mit einer Id einfach nicht mehr gelesen wird.
export const COOKIE_NAME = "zugang";
export const COOKIE_DAYS = 365;