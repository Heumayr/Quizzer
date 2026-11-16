// Minispiel Memory (M35 C5) am Telefon: das Feld kennt nur, was der Server schickt - verdeckte Karten
// zeigen "?", offene ihr Motiv, gefundene das Motiv mit "gefunden". Eine gefundene oder die eine offene
// Karte fragt den Server gar nicht erst.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { MemoryModul, endeText, sekundenText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/memory.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new MemoryModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const karten = () => wurzel.querySelectorAll("memory-karte");
const hinweis = () => wurzel.querySelector("memory-hinweis").textContent;
const stand = () => wurzel.querySelector("memory-stand").textContent;

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const hund = { index: 3, symbol: "🐶", name: "Hund" };
const katze = { index: 7, symbol: "🐱", name: "Katze" };

function zustand(extra = {}) {
    return { cardCount: 12, totalPairs: 6, remainingMs: 60000, openCards: [], foundCards: [], ownPairs: 0, ownFinishedMs: null, ...extra };
}

// --- Vor dem Start: kein Feld ---
pruefe(karten().length === 0, "Vor dem Start liegen schon Karten.");

// --- Start: zwoelf verdeckte Karten, kein Motiv ---
modul.starte(zustand(), senden);
pruefe(karten().length === 12, "Das Feld hat nicht zwoelf Karten: " + karten().length);
pruefe(karten().every(k => k.textContent === "?" && k.classList.contains("memory-verdeckt")), "Eine Karte ist vor dem ersten Umdrehen offen.");
pruefe(karten()[0].getAttribute("aria-label") === "Karte 1, verdeckt", "Die verdeckte Karte hat keine Beschriftung fuer Vorleser.");
pruefe(stand().includes("0 von 6 Paaren") && stand().includes("Noch 60 s"), "Stand oder Restzeit fehlen: " + stand());

// --- Umdrehen fragt den Server - das Motiv kommt erst mit der Antwort ---
karten()[3].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].card === 3, "Das Umdrehen geht nicht als Karte hinaus: " + JSON.stringify(gesendet));
pruefe(karten()[3].textContent === "?", "Das Telefon zeigt ein Motiv, bevor der Server es geschickt hat.");

modul.aktualisiere(zustand({ openCards: [hund] }));
pruefe(karten()[3].textContent === "🐶" && karten()[3].getAttribute("aria-label") === "Hund", "Die offene Karte zeigt nicht ihr Motiv.");

// --- Die eine offene Karte noch einmal: nichts senden ---
gesendet = [];
karten()[3].click();
await ruhe();
pruefe(gesendet.length === 0, "Die offene Karte fragt den Server noch einmal.");

// --- Zwei ohne Paar: beide offen, in Worten ---
modul.aktualisiere(zustand({ openCards: [hund, katze] }));
pruefe(karten()[7].textContent === "🐱" && karten()[3].textContent === "🐶", "Zwei offene ohne Paar sind nicht beide zu sehen.");
pruefe(hinweis().includes("Kein Paar"), "Kein Paar steht nicht in Worten da: " + hinweis());

// --- Ein Paar gefunden: Motiv bleibt, "gefunden" fuer Vorleser, nicht mehr antippbar ---
const hund2 = { index: 9, symbol: "🐶", name: "Hund" };
modul.aktualisiere(zustand({ foundCards: [hund, hund2], ownPairs: 1 }));
pruefe(karten()[9].classList.contains("memory-gefunden") && karten()[9].getAttribute("aria-label") === "Hund – gefunden",
    "Die gefundene Karte ist nicht als gefunden beschriftet.");
pruefe(karten()[7].textContent === "?", "Die geschlossene Karte zeigt noch ihr Motiv.");
pruefe(hinweis().includes("Paar"), "Ein Paar steht nicht in Worten da: " + hinweis());
gesendet = [];
karten()[9].click();
await ruhe();
pruefe(gesendet.length === 0, "Eine gefundene Karte fragt den Server.");

// --- Gesperrt: nichts ---
modul.setLocked(true);
karten()[0].click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat umgedreht.");
modul.setLocked(false);

// --- Die Zeit laeuft am Telefon ab: nichts mehr senden ---
uhr.vor(61000);
pruefe(hinweis() === "Die Zeit ist um.", "Nach Ablauf steht kein Hinweis: " + hinweis());
karten()[0].click();
await ruhe();
pruefe(gesendet.length === 0, "Nach Ablauf der Zeit wurde umgedreht.");

// --- Fertig, die anderen spielen noch; dann das Ende ---
modul.zeigeErgebnis(zustand({ ownPairs: 6, ownFinishedMs: 42300 }));
pruefe(hinweis().includes("42,3 s"), "Die eigene Zeit fehlt: " + hinweis());
modul.zeigeEnde(zustand({ ownPairs: 2, isWinner: false, winnerNames: ["Anna"] }));
pruefe(hinweis().includes("2 von 6 Paaren") && hinweis().includes("Anna"), "Das Ende nennt Stand oder Sieger nicht: " + hinweis());
pruefe(endeText(zustand({ ownFinishedMs: 30000, isWinner: true })).startsWith("🏆"), "Der Sieg steht nicht da.");
pruefe(endeText(zustand({ winnerNames: [] })).includes("Niemand"), "Ohne Sieger fehlt der Satz.");
pruefe(sekundenText(41000) === "41,0 s", "Die Schreibweise weicht vom Beamer ab: " + sekundenText(41000));

// --- Nach dem Ende schickt das Feld nichts mehr ---
gesendet = [];
karten()[0].click();
await ruhe();
pruefe(gesendet.length === 0, "Nach dem Ende wurde umgedreht.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("memory: alle Zusicherungen gruen.");
