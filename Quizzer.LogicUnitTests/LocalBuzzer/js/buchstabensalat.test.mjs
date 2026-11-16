// Minispiel Buchstabensalat (M35 C5) am Telefon: Eingabe, genau eine Abgabe je Versuch, der naechste
// Begriff kommt nur vom Server - auch, wenn ein anderer geloest hat. Den Begriff kennt das Telefon nie.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { BuchstabensalatModul, endeText, restText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/buchstabensalat.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new BuchstabensalatModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const feld = () => wurzel.querySelector("buchstabensalat-eingabe");
const knopf = () => wurzel.querySelector("buchstabensalat-senden");
const text = (klasse) => wurzel.querySelector(klasse).textContent;
const tippe = (wert) => { feld().value = wert; };
const enter = () => (feld().hoerer.keydown ?? []).forEach(f => f({ key: "Enter", preventDefault() {} }));

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const stand = (nummer, wirrwarr, mehr = {}) => ({
    durationSeconds: 90, remainingMs: 30000, totalTerms: 3, termNumber: nummer, scramble: wirrwarr,
    ownSolved: 0, lastWrong: false, lastSolved: null, lastSolver: null, isWinner: false, winnerNames: [], ...mehr
});

// --- Vor dem Start: keine Eingabe ---
pruefe(wurzel.querySelector("buchstabensalat-zeile").hidden === true, "Vor dem Start steht die Eingabe schon da.");
pruefe(feld().getAttribute("aria-label") === "Deine Lösung", "Das Feld hat keine Beschriftung fuer Vorleser.");
pruefe(feld().getAttribute("autocomplete") === "off" && feld().getAttribute("spellcheck") === "false",
    "Die Telefontastatur darf das Wort nicht vorschlagen oder korrigieren.");

// --- Start: das Wirrwarr des Servers, eine Abgabe je Versuch ---
modul.starte(stand(1, "SÄKE"), senden);
pruefe(text("buchstabensalat-wirrwarr") === "SÄKE", "Das Wirrwarr steht nicht da: " + text("buchstabensalat-wirrwarr"));
pruefe(text("buchstabensalat-stand").includes("Begriff 1 von 3") && text("buchstabensalat-stand").includes("Noch 30 s"),
    "Nummer oder Restzeit fehlen: " + text("buchstabensalat-stand"));

tippe("   ");
knopf().click();
await ruhe();
pruefe(gesendet.length === 0, "Eine leere Eingabe wurde gesendet.");

tippe("  Kaese ");
knopf().click();
enter();
await ruhe();
pruefe(gesendet.length === 1, "Nicht genau eine Abgabe: " + JSON.stringify(gesendet));
pruefe(gesendet[0]?.termNumber === 1 && gesendet[0]?.guess === "Kaese", "Falsche Abgabe: " + JSON.stringify(gesendet[0]));

// --- Daneben: die Eingabe bleibt zum Verbessern, der Hinweis sagt es in Worten ---
modul.aktualisiere(stand(1, "SÄKE", { lastWrong: true }));
pruefe(feld().value === "  Kaese ", "Nach einem Fehlversuch ist die Eingabe weg.");
pruefe(text("buchstabensalat-hinweis").includes("✗") && text("buchstabensalat-hinweis").includes("Leider nicht"),
    "Ein Fehlversuch sagt es nicht mit Zeichen und Wort: " + text("buchstabensalat-hinweis"));

// --- Ein anderer loest: der naechste Begriff kommt ohne eigenes Zutun, das Feld ist leer ---
gesendet = [];
tippe("Kä");
modul.aktualisiere(stand(2, "DNUH", { lastSolved: "Käse", lastSolver: "Anna" }));
pruefe(text("buchstabensalat-wirrwarr") === "DNUH", "Der naechste Begriff kommt nicht an.");
pruefe(feld().value === "", "Die Eingabe zum alten Begriff steht noch da.");
pruefe(text("buchstabensalat-hinweis").includes("Anna") && text("buchstabensalat-hinweis").includes("Käse"),
    "Wer geloest hat, steht nicht da: " + text("buchstabensalat-hinweis"));

// --- Selbst geloest: Zeichen und Wort ---
tippe("Hund");
enter();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].termNumber === 2, "Enter sendet nicht: " + JSON.stringify(gesendet));
modul.aktualisiere(stand(3, "MALPE", { ownSolved: 1, lastSolved: "Hund", lastSolver: "Bert" }));
pruefe(text("buchstabensalat-hinweis").startsWith("✓ Richtig"), "Die eigene Loesung wird nicht bestaetigt: " + text("buchstabensalat-hinweis"));
pruefe(text("buchstabensalat-stand").includes("1 gelöst"), "Der eigene Stand fehlt: " + text("buchstabensalat-stand"));

// --- Bleibt die Antwort aus, ist das Senden nach der Wartezeit wieder frei ---
gesendet = [];
tippe("Lampe");
knopf().click();
knopf().click();
await ruhe();
pruefe(gesendet.length === 1, "Waehrend des Wartens wurde ein zweites Mal gesendet.");
uhr.vor(3000);
knopf().click();
await ruhe();
pruefe(gesendet.length === 2, "Ohne Antwort des Servers bleibt das Telefon stecken.");

// --- Gesperrt: nichts ---
modul.aktualisiere(stand(3, "MALPE", { ownSolved: 1, lastWrong: true, remainingMs: 20000 }));
gesendet = [];
modul.setLocked(true);
knopf().click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gesendet.");
modul.setLocked(false);

// --- Die Zeit laeuft am Telefon ab: nichts mehr senden ---
uhr.vor(21000);
pruefe(text("buchstabensalat-wirrwarr") === "Die Zeit ist um.", "Nach Ablauf steht das Wirrwarr noch da.");
knopf().click();
await ruhe();
pruefe(gesendet.length === 0, "Nach Ablauf der Zeit wurde gesendet.");

// --- Das Ende ---
modul.zeigeEnde({ ownSolved: 1, isWinner: false, winnerNames: ["Anna"] });
pruefe(text("buchstabensalat-hinweis").includes("Anna"), "Wer die meisten hat, fehlt: " + text("buchstabensalat-hinweis"));
pruefe(wurzel.querySelector("buchstabensalat-zeile").hidden === true, "Nach dem Ende steht die Eingabe noch da.");
pruefe(endeText({ ownSolved: 2, isWinner: true, winnerNames: ["Bert"] }).startsWith("🏆"), "Der Sieg steht nicht da.");
pruefe(endeText({ ownSolved: 0, winnerNames: [] }).includes("Niemand"), "Ohne Loeser fehlt der Satz.");
pruefe(restText(1200) === "Noch 2 s", "Die Restzeit wird nicht aufgerundet: " + restText(1200));

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("buchstabensalat: alle Zusicherungen gruen.");
