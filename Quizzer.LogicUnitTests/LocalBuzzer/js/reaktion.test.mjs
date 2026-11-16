// Minispiel Reaktion (M35 B3) am Telefon: rot, nach der Wartezeit vom Server lokal gruen mit
// Symbol und "JETZT!", Zeit gemessen am Telefon ab dem Umschalten, Fruehstart vor gruen.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

let vibriert = 0;
Object.defineProperty(globalThis.navigator, "vibrate", { value: () => { vibriert++; }, configurable: true });

const { ReaktionModul, JETZT_TEXT, WARTEN_TEXT, AUFGEBEN_NACH_MS } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/reaktion.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new ReaktionModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const flaeche = () => wurzel.querySelector(".reaktion-taste");
const text = () => wurzel.querySelector(".reaktion-text").textContent;
const symbol = () => wurzel.querySelector(".reaktion-symbol").textContent;
const tippe = () => (flaeche().hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 }));

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

// --- Rot, dann nach genau der Wartezeit gruen ---
modul.starte({ waitMs: 2500 }, senden);

pruefe(flaeche().classList.contains("reaktion-warten"), "Zu Beginn ist die Flaeche nicht im Wartezustand.");
pruefe(text() === WARTEN_TEXT, "Im Wartezustand steht nicht der Warte-Text: " + text());
pruefe(symbol() !== "", "Im Wartezustand fehlt das Symbol - dann traegt nur die Farbe die Information.");

uhr.vor(2499);
pruefe(!flaeche().classList.contains("reaktion-jetzt"), "Vor Ablauf der Wartezeit ist es schon gruen.");

uhr.vor(1);
pruefe(flaeche().classList.contains("reaktion-jetzt"), "Nach der Wartezeit schaltet die Flaeche nicht auf gruen.");
pruefe(text() === JETZT_TEXT, "Beim Umschalten steht nicht 'JETZT!' da: " + text());
pruefe(symbol() !== "" && symbol() !== "✋", "Beim Umschalten wechselt das Symbol nicht mit: " + symbol());
pruefe(vibriert === 1, "Beim Umschalten vibriert das Telefon nicht.");

// --- Die Zeit misst das Telefon ab seinem Umschalten ---
uhr.vor(321);
tippe();

pruefe(gesendet.length === 1, "Der Tipp kam nicht beim Server an.");
pruefe(gesendet[0]?.falseStart === false && gesendet[0]?.milliseconds === 321,
    "Gemessen wurde nicht ab dem Umschalten auf gruen: " + JSON.stringify(gesendet[0]));
pruefe(text() === "321 ms", "Die eigene Zeit steht nicht da: " + text());

tippe();
pruefe(gesendet.length === 1, "Ein zweiter Tipp hat noch einmal gesendet.");

// --- Fruehstart: Tippen vor gruen kostet die Runde ---
gesendet = [];
modul.starte({ waitMs: 3000 }, senden);
uhr.vor(1000);
tippe();

pruefe(gesendet.length === 1 && gesendet[0].falseStart === true && gesendet[0].milliseconds === null,
    "Ein Tipp vor gruen ist kein Fruehstart: " + JSON.stringify(gesendet));
pruefe(text().includes("Zu früh"), "Der Fruehstart wird nicht gesagt: " + text());

uhr.vor(5000);
pruefe(!flaeche().classList.contains("reaktion-jetzt"), "Nach dem Fruehstart wird es trotzdem noch gruen.");
tippe();
pruefe(gesendet.length === 1, "Nach dem Fruehstart laesst sich noch einmal abgeben.");

// --- Wer nicht tippt, gibt nach der Grenze selbst auf (die Runde haengt nicht) ---
gesendet = [];
modul.starte({ waitMs: 2000 }, senden);
uhr.vor(2000 + AUFGEBEN_NACH_MS);

pruefe(gesendet.length === 1 && gesendet[0].milliseconds > 5000,
    "Ohne Tipp gibt das Telefon nicht ueber der Grenze auf: " + JSON.stringify(gesendet));

// --- Gesperrt: kein Tipp zaehlt ---
gesendet = [];
modul.starte({ waitMs: 2000 }, senden);
modul.setLocked(true);
uhr.vor(2100);
tippe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat abgegeben.");
modul.setLocked(false);

// --- Ergebnis und Ende ---
modul.zeigeErgebnis({ ownOutcome: 1, ownMilliseconds: 287, bestMilliseconds: 250 });
pruefe(text().includes("287 ms") && text().includes("250 ms"), "Das Rundenergebnis nennt nicht beide Zeiten: " + text());

modul.zeigeErgebnis({ ownOutcome: 2, ownMilliseconds: null });
pruefe(text().includes("Frühstart"), "Ein Fruehstart im Ergebnis wird nicht gesagt: " + text());

modul.zeigeEnde({ isWinner: false, winnerNames: ["Bert"], winnerMilliseconds: 199, bestMilliseconds: 250 });
pruefe(text().includes("Bert") && text().includes("199 ms"), "Am Ende fehlt, wer gewonnen hat: " + text());

modul.zeigeEnde({ isWinner: true, winnerNames: ["Anna"], winnerMilliseconds: 199, bestMilliseconds: 199 });
pruefe(text().includes("Gewonnen!"), "Der Sieger erfaehrt es nicht: " + text());

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufraeumen laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("reaktion: alle Zusicherungen gruen.");
