// Minispiel Staffel (M37 T11) am Telefon: nur der Laeufer hat ein Eingabefeld und schickt je Zug genau eine Antwort - mit dem
// Zug, den sein Telefon sah. Die anderen sehen, wer dran ist. Die Loesungen zeigt das Telefon erst nach der Runde, und nur, was
// der Server schickt. Namen nur als Text, die Teamfarbe nur als Klasse der Palette.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

const { StaffelModul, standText, letzterText, teamZeile, stueckText, siegerText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/staffel.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new StaffelModul({ timer: uhr.timer, now: uhr.now });

// Vor dem Einhaengen und ohne Start: nichts zu tun, nichts zu senden.
modul.aktualisiere({ isRunner: true, turn: 1 });
modul.mount(wurzel);
modul.setLocked(false);

const feld = () => wurzel.querySelector(".staffel-eingabe");
const knopf = () => wurzel.querySelector(".staffel-senden");
const zeile = () => wurzel.querySelector(".staffel-zeile");
const stand = () => wurzel.querySelector(".staffel-stand").textContent;
const hinweis = () => wurzel.querySelector(".staffel-hinweis").textContent;
const teams = () => wurzel.querySelectorAll(".staffel-team");
const teile = () => wurzel.querySelectorAll(".staffel-teil");
const enter = () => (feld().hoerer.keydown ?? []).forEach(f => f({ key: "Enter", preventDefault() {} }));

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const rot = { name: "<b>Rot</b>", symbol: "✚", colorIndex: 5, solved: 2, isOwn: true, isOnTurn: true, isWinner: false };
const blau = { name: "Blau", symbol: "★", colorIndex: 4, solved: 1, isOwn: false, isOnTurn: false, isWinner: false };
const laeufer = { isRunner: true, turn: 7, part: 3, totalParts: 8, runnerName: "Anna", runnerTeam: "✚ Rot", teams: [rot, blau] };

// --- Ein Zuschauer, dessen Modul nie gestartet wurde, bekommt kein Feld ---
pruefe(zeile().hidden === true, "Vor dem Start steht ein Eingabefeld da.");
pruefe(hinweis() === "Gleich geht es los", "Vor dem Start fehlt der Hinweis: " + hinweis());

// --- Der Laeufer: sein Teil, ein Feld, genau eine Antwort je Zug ---
modul.starte(laeufer, senden);
pruefe(zeile().hidden === false, "Der Läufer hat kein Eingabefeld.");
pruefe(stand() === "Du bist dran: Teil 3 von 8", "Der Läufer sieht nicht seinen Teil: " + stand());

feld().value = "  Erde  ";
knopf().click();
feld().value = "Erde";
knopf().click();
enter();
await ruhe();
pruefe(gesendet.length === 1, "Nicht genau eine Antwort gesendet: " + JSON.stringify(gesendet));
pruefe(gesendet[0]?.turn === 7 && gesendet[0]?.answer === "Erde", "Zug oder Antwort stimmen nicht: " + JSON.stringify(gesendet[0]));
pruefe(Object.keys(gesendet[0] ?? {}).every(k => k === "turn" || k === "answer"), "Die Antwort schickt mehr als Zug und Text: " + JSON.stringify(gesendet[0]));

// Dasselbe Update noch einmal (ein anderes Team sah etwas Neues): derselbe Zug gibt das Feld nicht frei.
modul.aktualisiere(laeufer);
feld().value = "Erde";
knopf().click();
await ruhe();
pruefe(gesendet.length === 1, "Derselbe Zug lässt eine zweite Antwort zu.");

// Bleibt die Antwort des Servers aus, darf er nach der Wartezeit noch einmal - mit demselben Zug.
uhr.vor(3000);
feld().value = "Erde";
enter();
await ruhe();
pruefe(gesendet.length === 2 && gesendet[1].turn === 7, "Nach der Wartezeit geht keine zweite Antwort mit demselben Zug: " + JSON.stringify(gesendet));

// Ein neuer Zug fuer denselben Laeufer (allein im Team) gibt das Feld frei; mehr als 80 Zeichen gehen nicht hinaus.
modul.aktualisiere({ ...laeufer, turn: 9, part: 4 });
feld().value = "x".repeat(100);
knopf().click();
await ruhe();
pruefe(gesendet.length === 3 && gesendet[2].turn === 9 && gesendet[2].answer.length === 80,
    "Der neue Zug sendet nicht oder ungekürzt: " + JSON.stringify(gesendet[2]));

// Gesperrt: nichts.
modul.aktualisiere({ ...laeufer, turn: 11, part: 5 });
modul.setLocked(true);
feld().value = "Mars";
knopf().click();
await ruhe();
pruefe(gesendet.length === 3, "Ein gesperrtes Telefon hat gesendet.");
modul.setLocked(false);

// --- Die anderen: wer dran ist, kein Feld; der Name nur als Text ---
gesendet = [];
modul.aktualisiere({ isRunner: false, turn: 11, part: 5, totalParts: 8, runnerName: "<b>Bert</b>", runnerTeam: "✚ Rot", teams: [rot, blau],
    lastPart: 4, lastCorrect: true, lastTeam: "✚ Rot" });
pruefe(zeile().hidden === true, "Wer nicht dran ist, hat ein Eingabefeld.");
pruefe(stand() === "<b>Bert</b> ist dran (✚ Rot) – Teil 5 von 8", "Die anderen sehen nicht, wer dran ist: " + stand());
pruefe(hinweis() === "Teil 4: ✓ ✚ Rot", "Der letzte Treffer steht nicht da: " + hinweis());
feld().value = "Mars";
knopf().click();
enter();
await ruhe();
pruefe(gesendet.length === 0, "Wer nicht dran ist, hat gesendet - mit dem Zug aus der Nachricht.");

// --- Die Teams: Name als Text, Farbe nur als Klasse ---
pruefe(teams().length === 2, "Nicht beide Teams stehen da.");
pruefe(teams()[0].textContent === "✚ <b>Rot</b>: 2 Teile · läuft · euer Team", "Die eigene Zeile stimmt nicht: " + teams()[0].textContent);
pruefe(teams()[1].textContent === "★ Blau: 1 Teil", "Die fremde Zeile stimmt nicht: " + teams()[1].textContent);
pruefe(teams()[0].classList.contains("team-farbe-5") && teams()[1].classList.contains("team-farbe-4"), "Die Teamfarben stimmen nicht.");

modul.aktualisiere({ isRunner: false, part: 5, totalParts: 8, runnerName: "Bert", teams: [{ ...blau, colorIndex: 99 }, { ...blau, colorIndex: -1 }] });
pruefe(![...Array(100).keys()].some(i => teams()[0].classList.contains(`team-farbe-${i}`) || teams()[1].classList.contains(`team-farbe-${i}`)),
    "Eine Farbe außerhalb der Palette wurde gesetzt.");

// --- Waehrend der Runde keine Loesung, selbst wenn eine in der Nachricht stuende ---
modul.aktualisiere({ isRunner: false, part: 5, totalParts: 8, runnerName: "Bert", teams: [rot], resolution: [{ number: 1, solution: "Merkur", team: "✚ Rot" }] });
pruefe(teile().length === 0, "Während der Runde zeigt das Telefon eine Lösung.");

// --- Was zuletzt geschah: ohne Antwort und ohne Loesung ---
pruefe(letzterText({ lastPart: 3, lastCorrect: false, lastTeam: "★ Blau" }) === "Teil 3: ✗ ★ Blau lag daneben.", "Der Fehler wird nicht gesagt.");
pruefe(letzterText({ lastPart: 3, lastPassed: true, lastTeam: "★ Blau" }) === "Teil 3: ★ Blau hat weitergegeben.", "Das Weitergeben wird nicht gesagt.");
pruefe(letzterText({ lastPart: 3, lastAccepted: true, lastCorrect: true, lastTeam: "★ Blau" }).includes("vom Spielleiter gewertet"), "Das Gelten lassen wird nicht gesagt.");
pruefe(letzterText({ lastPart: 0 }) === "", "Vor dem ersten Versuch steht ein letzter da.");
pruefe(standText({ part: 0, totalParts: 3 }) === "Die Staffel ist durch.", "Ohne Läufer fehlt der Satz.");

// --- Nach der Runde: die Aufloesung vom Server; ein Laeufer, dessen Runde endet, sendet nichts mehr ---
modul.aktualisiere({ ...laeufer, turn: 13, part: 8 });
pruefe(zeile().hidden === false, "Vor dem Ende ist das Telefon nicht Läufer.");
modul.zeigeEnde({ isWinner: true, teams: [{ ...rot, isOnTurn: false, isWinner: true }, blau], lastPart: 3, lastCorrect: true, lastTeam: "✚ Rot",
    resolution: [{ number: 1, solution: "Merkur", team: "✚ Rot" }, { number: 2, solution: "<i>Venus</i>", team: null }] });
pruefe(zeile().hidden === true, "Nach der Runde steht noch ein Eingabefeld da.");
pruefe(hinweis().includes("Euer Team gewinnt"), "Das Siegerteam erfährt es nicht: " + hinweis());
pruefe(teile().length === 2, "Nach der Runde fehlt die Auflösung.");
pruefe(teile()[0].textContent === "Teil 1: Merkur – ✚ Rot", "Der gelöste Teil stimmt nicht: " + teile()[0].textContent);
pruefe(teile()[1].textContent === "Teil 2: <i>Venus</i> – verfehlt", "Der verfehlte Teil steht nicht als Text da: " + teile()[1].textContent);
pruefe(teile()[1].classList.contains("staffel-verfehlt"), "Der verfehlte Teil ist nicht markiert.");
pruefe(teams()[0].textContent.includes("★ Sieger"), "Das Siegerteam ist nicht mit Text markiert: " + teams()[0].textContent);

modul.zeigeEnde({ isWinner: false, teams: [rot, { ...blau, isWinner: true }] });
pruefe(hinweis() === "Gewonnen: ★ Blau.", "Der Verlierer erfährt nicht, wer gewonnen hat: " + hinweis());
pruefe(siegerText([]) === "Kein Team liegt vorn – keine Punkte.", "Ohne Sieger fehlt der Satz.");
pruefe(teamZeile({ symbol: "▲", name: "Gelb", solved: 1 }) === "▲ Gelb: 1 Teil", "Ein Team mit einem Teil: " + teamZeile({ symbol: "▲", name: "Gelb", solved: 1 }));
pruefe(stueckText({ number: 3, solution: "Erde" }) === "Teil 3: Erde – verfehlt", "Ein Teil ohne Team heißt nicht verfehlt.");

// Nach der Runde sendet ein Tipp nichts mehr.
gesendet = [];
feld().value = "Mars";
knopf().click();
await ruhe();
pruefe(gesendet.length === 0, "Nach der Runde wurde gesendet.");

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufräumen laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("staffel: alle Zusicherungen gruen.");
