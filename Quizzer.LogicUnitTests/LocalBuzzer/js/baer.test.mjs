// Minispiel Erklaerbaer (M35 C6) am Telefon (Modul baer.js - der Umlaut-Waechter liest Klassennamen als Anzeigetext): der Erklaerer sieht den Begriff und kann nicht senden; die anderen
// tippen, genau eine Abgabe je Versuch. Nach der Runde steht die Aufloesung in Worten da.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { ErklaerbaerModul, ergebnisText, restText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/baer.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const neu = () => {
    wurzel.children = [];
    const modul = new ErklaerbaerModul({ timer: uhr.timer, now: uhr.now });
    modul.mount(wurzel);
    modul.setLocked(false);
    return modul;
};

const feld = () => wurzel.querySelector("baer-eingabe");
const knopf = () => wurzel.querySelector("baer-senden");
const text = (klasse) => wurzel.querySelector(klasse).textContent;
const tippe = (wert) => { feld().value = wert; };
const enter = () => (feld().hoerer.keydown ?? []).forEach(f => f({ key: "Enter", preventDefault() {} }));

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const stand = (mehr = {}) => ({
    durationSeconds: 60, remainingMs: 30000, explainerName: "Anna", isExplainer: false, term: null,
    lastWrong: false, solution: null, solverName: null, isSolver: false, ...mehr
});

// --- Der Erklaerer: der Begriff gross, keine Eingabe, nichts wird gesendet ---
let modul = neu();
pruefe(wurzel.querySelector("baer-zeile").hidden === true, "Vor dem Start steht die Eingabe schon da.");
modul.starte(stand({ isExplainer: true, term: "Leuchtturm" }), senden);
pruefe(text("baer-begriff") === "Leuchtturm", "Der Erklaerer sieht seinen Begriff nicht: " + text("baer-begriff"));
pruefe(wurzel.querySelector("baer-zeile").hidden === true, "Der Erklaerer bekommt ein Eingabefeld.");
pruefe(text("baer-stand").includes("Du erklärst") && text("baer-stand").includes("Noch 30 s"),
    "Rolle oder Restzeit fehlen: " + text("baer-stand"));
tippe("Leuchtturm");
knopf().click();
enter();
await ruhe();
pruefe(gesendet.length === 0, "Das Telefon des Erklaerers sendet einen Rateversuch.");
modul.dispose();

// --- Ein Ratender: kein Begriff, eine Abgabe je Versuch ---
modul = neu();
modul.starte(stand({ term: "Leuchtturm" }), senden);
pruefe(text("baer-begriff") === "", "Ein Ratender sieht einen Begriff.");
pruefe(wurzel.querySelector("baer-zeile").hidden === false, "Der Ratende hat kein Eingabefeld.");
pruefe(text("baer-hinweis").includes("Anna erklärt"), "Wer erklaert, fehlt: " + text("baer-hinweis"));
pruefe(feld().getAttribute("aria-label") === "Dein Rateversuch", "Das Feld hat keine Beschriftung fuer Vorleser.");

tippe("   ");
knopf().click();
await ruhe();
pruefe(gesendet.length === 0, "Eine leere Eingabe wurde gesendet.");

tippe("  Turm ");
knopf().click();
enter();
await ruhe();
pruefe(gesendet.length === 1, `Ein Versuch ging ${gesendet.length}-mal hinaus.`);
pruefe(JSON.stringify(gesendet[0]) === JSON.stringify({ guess: "Turm" }), "Die Abgabe traegt nicht genau den Versuch: " + JSON.stringify(gesendet[0]));

// Die Antwort des Servers: daneben - in Worten, und es darf weiter geraten werden.
modul.aktualisiere(stand({ lastWrong: true, remainingMs: 20000 }));
pruefe(text("baer-hinweis").includes("Leider nicht"), "Der Fehlversuch steht nicht da: " + text("baer-hinweis"));
tippe("Leuchtturm");
knopf().click();
await ruhe();
pruefe(gesendet.length === 2, "Nach der Antwort des Servers laesst sich nicht weiter raten.");

// --- Nach der Runde: Aufloesung in Worten, keine Eingabe mehr ---
modul.zeigeErgebnis(stand({ solution: "Leuchtturm", solverName: "Bert" }));
pruefe(text("baer-hinweis") === "Gesucht war „Leuchtturm“. Bert hat es erraten.", "Die Aufloesung fehlt: " + text("baer-hinweis"));
pruefe(wurzel.querySelector("baer-zeile").hidden === true, "Nach der Runde steht die Eingabe noch da.");
pruefe(ergebnisText({ solution: "Käse", isSolver: true }).includes("✓ Du hast es erraten"), "Der eigene Treffer steht nicht da.");
pruefe(ergebnisText({ solution: "Käse" }).includes("Niemand"), "Ohne Treffer fehlt der Satz.");
pruefe(restText(1200) === "Noch 2 s", "Die Restzeit wird nicht aufgerundet: " + restText(1200));

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("baer: alle Zusicherungen gruen.");
