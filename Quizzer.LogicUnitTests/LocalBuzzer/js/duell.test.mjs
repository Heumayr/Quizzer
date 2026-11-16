// Minispiel Duell (M35 C2) am Telefon: der Ausloeser waehlt den Gegner, dann Schere-Stein-Papier
// (ein Zug, genau einmal) oder die Reaktion aus B3. Den fremden Zug zeigt das Telefon nur, wenn der
// Server ihn schickt - also erst nach der Runde.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

if (!globalThis.navigator.vibrate) {
    Object.defineProperty(globalThis.navigator, "vibrate", { value: () => {} });
}

const { DuellModul, zugName } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/duell.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new DuellModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const kopf = () => wurzel.querySelector(".duell-kopf").textContent;
const text = () => wurzel.querySelector(".duell-text").textContent;
const zuege = () => wurzel.querySelectorAll(".duell-zug");
const wahlTasten = () => wurzel.querySelectorAll(".spielerwahl-taste");

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

// --- Der Ausloeser waehlt seinen Gegner ---
modul.starte({ choosesOpponent: true, challengerName: "Anna", options: [{ id: "b-1", name: "Bert" }] }, senden);
pruefe(kopf().includes("Anna wählt einen Gegner"), "Der Kopf sagt nicht, wer wählt: " + kopf());
pruefe(wahlTasten().length === 1 && zuege().length === 0, "Statt der Spielerwahl stehen Züge da.");
wahlTasten()[0].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].chosen === "b-1", "Die Wahl des Gegners geht nicht raus: " + JSON.stringify(gesendet));

// --- Schere-Stein-Papier: drei Zuege, einer geht raus, genau einmal ---
gesendet = [];
const runde = { discipline: 0, challengerName: "Anna", opponentName: "Bert", isDuellist: true };
modul.starte(runde, senden);
pruefe(kopf() === "Duell: Anna gegen Bert", "Der Kopf nennt das Paar nicht: " + kopf());
pruefe(zuege().length === 3 && zuege()[1].textContent.includes("Stein"), "Die drei Züge fehlen.");

zuege()[1].click();
zuege()[0].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].move === 2, "Nicht genau ein Zug (Stein = 2) gesendet: " + JSON.stringify(gesendet));
pruefe(text().includes("Stein"), "Der eigene Zug steht nicht als Text da: " + text());

// --- Gesperrt (Zuschauer): kein Zug ---
gesendet = [];
modul.starte(runde, senden);
modul.setLocked(true);
zuege()[0].click();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gezogen.");
modul.setLocked(false);

// --- Ergebnis: der fremde Zug nur, wenn der Server ihn schickt ---
modul.zeigeErgebnis({ ...runde, ownMove: 2, otherMove: null, ownWins: 0, otherWins: 0 });
pruefe(text().includes("warte auf die Auflösung") && !text().includes("Gegner:"),
    "Ohne fremden Zug zeigt das Telefon trotzdem einen: " + text());

modul.zeigeErgebnis({ ...runde, ownMove: 2, otherMove: 1, ownWins: 1, otherWins: 0 });
pruefe(text().includes("Gegner: Schere") && text().includes("1:0"), "Nach der Runde fehlen Gegnerzug oder Stand: " + text());

modul.zeigeErgebnis({ discipline: 0, challengerName: "Anna", opponentName: "Bert", isDuellist: false });
pruefe(text().includes("Beamer"), "Ein Zuschauer bekommt keinen Hinweis: " + text());

// --- Reaktion: das Modul aus B3 misst ---
gesendet = [];
modul.starte({ discipline: 1, challengerName: "Anna", opponentName: "Bert", isDuellist: true, reaction: { waitMs: 2000 } }, senden);
pruefe(wurzel.querySelector(".reaktion-taste") != null, "Die Reaktion des Duells hat keine Taste.");
uhr.vor(2000);
uhr.vor(250);
(wurzel.querySelector(".reaktion-taste").hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 }));
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].milliseconds === 250, "Die Reaktion misst nicht am Telefon: " + JSON.stringify(gesendet));

// --- Ende ---
modul.zeigeEnde({ discipline: 0, challengerName: "Anna", opponentName: "Bert", isWinner: true, ownWins: 2, otherWins: 1 });
pruefe(text().includes("Gewonnen!") && text().includes("2:1"), "Der Sieger erfährt es nicht: " + text());

modul.zeigeEnde({ discipline: 0, challengerName: "Anna", opponentName: "Bert", isDraw: true });
pruefe(text().includes("Unentschieden"), "Ein Unentschieden wird nicht gesagt: " + text());

modul.zeigeEnde({ discipline: 0, challengerName: "Anna", opponentName: "Bert", winnerName: "Bert" });
pruefe(text().includes("Bert"), "Der Verlierer erfährt nicht, wer gewonnen hat: " + text());

pruefe(zugName(3) === "Papier" && zugName(undefined) === "nichts", "Die Zugnamen stimmen nicht.");

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufräumen laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("duell: alle Zusicherungen gruen.");
