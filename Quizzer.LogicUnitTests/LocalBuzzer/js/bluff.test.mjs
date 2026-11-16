// Minispiel Bluff (M35 C6) am Telefon: Phase 1 genau ein Bluff je Abgabe, Phase 2 je Antwort eine Taste mit der
// Nummer des Servers; das Telefon kennt weder Urheber noch die echte. Nach der Wahl die Aufloesung in Worten.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { BluffModul, endeText } = await import("../../../LocalBuzzer.Service/wwwroot/JS/minispiele/bluff.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new BluffModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const feld = () => wurzel.querySelector("bluff-eingabe");
const text = (klasse) => wurzel.querySelector(klasse).textContent;
const tasten = () => wurzel.querySelectorAll("bluff-option");

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const stand = (mehr = {}) => ({
    durationSeconds: 60, remainingMs: 30000, stage: 1, ownBluff: null, options: [], ownChoice: null,
    realAnswer: null, foundReal: false, fooledCount: 0, ownStruck: false, ...mehr
});

// --- Phase 1: ein Bluff, genau eine Abgabe ---
modul.starte(stand(), senden);
pruefe(wurzel.querySelector("bluff-zeile").hidden === false, "In Phase 1 fehlt das Eingabefeld.");
pruefe(tasten().length === 0, "In Phase 1 stehen schon Antworten da.");
pruefe(feld().getAttribute("aria-label") === "Dein Bluff", "Das Feld hat keine Beschriftung fuer Vorleser.");
feld().value = "  Paris ";
wurzel.querySelector("bluff-senden").click();
wurzel.querySelector("bluff-senden").click();
await ruhe();
pruefe(gesendet.length === 1, `Ein Bluff ging ${gesendet.length}-mal hinaus.`);
pruefe(JSON.stringify(gesendet[0]) === JSON.stringify({ bluff: "Paris" }), "Die Abgabe traegt nicht genau den Bluff: " + JSON.stringify(gesendet[0]));

modul.zeigeErgebnis(stand({ ownBluff: "Paris" }));
pruefe(text("bluff-hinweis").includes("Dein Bluff: „Paris“"), "Der eigene Bluff steht nach der Abgabe nicht da: " + text("bluff-hinweis"));

// --- Phase 2: je Antwort eine Taste, gesendet wird die Nummer des Servers ---
gesendet = [];
modul.starte(stand({ stage: 2, options: [{ number: 3, text: "Rom" }, { number: 1, text: "Wien" }] }), senden);
pruefe(wurzel.querySelector("bluff-zeile").hidden === true, "In Phase 2 steht das Eingabefeld noch da.");
pruefe(tasten().length === 2 && tasten()[0].textContent === "Rom", "Die Antworten stehen nicht in der Reihenfolge des Servers.");
tasten()[1].click();
tasten()[0].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].choice === 1 && !("bluff" in gesendet[0]), "Die Wahl traegt nicht genau die Nummer: " + JSON.stringify(gesendet));

modul.zeigeErgebnis(stand({ stage: 2, options: [{ number: 1, text: "Wien" }], ownChoice: 1 }));
pruefe(text("bluff-hinweis").includes("Gewählt: „Wien“"), "Die eigene Wahl steht nicht da: " + text("bluff-hinweis"));

// --- Nach der Wahl: Aufloesung in Worten ---
modul.zeigeEnde(stand({ stage: 2, realAnswer: "Rom", foundReal: true, fooledCount: 2 }));
pruefe(text("bluff-hinweis") === "Echt war „Rom“. ✓ Du hast sie gefunden! 2 sind auf deinen Bluff hereingefallen.",
    "Die Aufloesung fehlt: " + text("bluff-hinweis"));
pruefe(tasten().length === 0, "Nach dem Ende stehen noch Antworten da.");
pruefe(endeText({ realAnswer: "Rom", ownStruck: true }).includes("gestrichen"), "Ein gestrichener Bluff wird nicht genannt.");
pruefe(endeText({ realAnswer: "Rom", fooledCount: 1 }).includes("1 ist"), "Ein einzelner Reinfall ist falsch gebeugt.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("bluff: alle Zusicherungen gruen.");
