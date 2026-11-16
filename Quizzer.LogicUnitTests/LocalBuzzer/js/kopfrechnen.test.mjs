// Minispiel Kopfrechnen (M35 C5) am Telefon: Ziffernblock, genau eine Abgabe je Aufgabe, die naechste
// Aufgabe kommt nur vom Server. Die Loesung kennt das Telefon nie.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { KopfrechnenModul, endeText, restText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/kopfrechnen.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new KopfrechnenModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const tasten = () => wurzel.querySelectorAll("kopfrechnen-taste");
const taste = (text) => tasten().find(t => t.textContent === text);
const text = (klasse) => wurzel.querySelector(klasse).textContent;
const tippe = (...folge) => folge.forEach(t => taste(t).click());

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

// --- Vor dem Start: kein Ziffernblock, nichts zu senden ---
pruefe(wurzel.querySelector("kopfrechnen-tasten").hidden === true, "Vor dem Start steht der Ziffernblock schon da.");
pruefe(tasten().length === 12, "Der Ziffernblock hat nicht zwoelf Tasten: " + tasten().length);
pruefe(taste("✓").getAttribute("aria-label") === "Antwort senden", "Die Sendetaste hat keine Beschriftung fuer Vorleser.");

// --- Start: die Aufgabe des Servers, eintippen, genau eine Abgabe ---
modul.starte({ durationSeconds: 30, remainingMs: 30000, taskNumber: 1, taskText: "7 + 5", ownCorrect: 0, ownAnswered: 0, lastCorrect: null }, senden);
pruefe(text("kopfrechnen-aufgabe") === "7 + 5 = ?", "Die Aufgabe steht nicht da: " + text("kopfrechnen-aufgabe"));
pruefe(text("kopfrechnen-stand").includes("Noch 30 s"), "Die Restzeit fehlt: " + text("kopfrechnen-stand"));

tippe("1", "3", "⌫", "2");
pruefe(text("kopfrechnen-eingabe") === "12", "Ziffern und Loeschen stimmen nicht: " + text("kopfrechnen-eingabe"));
tippe("✓", "✓");
await ruhe();
pruefe(gesendet.length === 1, "Nicht genau eine Abgabe: " + JSON.stringify(gesendet));
pruefe(gesendet[0]?.taskNumber === 1 && gesendet[0]?.answer === 12, "Falsche Abgabe: " + JSON.stringify(gesendet[0]));

// --- Waehrend die Antwort unterwegs ist, nimmt der Block nichts an ---
tippe("9");
pruefe(text("kopfrechnen-eingabe") === "12", "Waehrend des Wartens wurde weitergetippt.");

// --- Die Antwort des Servers: naechste Aufgabe, leeres Feld, Hinweis in Worten ---
modul.aktualisiere({ durationSeconds: 30, remainingMs: 28000, taskNumber: 2, taskText: "9 − 4", ownCorrect: 1, ownAnswered: 1, lastCorrect: true });
pruefe(text("kopfrechnen-aufgabe") === "9 − 4 = ?", "Die naechste Aufgabe kommt nicht an.");
pruefe(text("kopfrechnen-eingabe") === "…", "Das Feld ist nach der neuen Aufgabe nicht leer.");
pruefe(text("kopfrechnen-hinweis").includes("Richtig"), "Kein Hinweis in Worten: " + text("kopfrechnen-hinweis"));
pruefe(text("kopfrechnen-stand").startsWith("1 richtig"), "Der Stand fehlt: " + text("kopfrechnen-stand"));

// --- Verworfen (dieselbe Aufgabe kommt zurueck): die Eingabe bleibt, man darf erneut senden ---
gesendet = [];
tippe("5", "✓");
modul.aktualisiere({ durationSeconds: 30, remainingMs: 27000, taskNumber: 2, taskText: "9 − 4", ownCorrect: 1, ownAnswered: 1, lastCorrect: true });
pruefe(text("kopfrechnen-eingabe") === "5", "Eine verworfene Abgabe hat die Eingabe geloescht.");
tippe("✓");
await ruhe();
pruefe(gesendet.length === 2 && gesendet.every(g => g.taskNumber === 2), "Nach dem Verwerfen darf nicht erneut gesendet werden: " + JSON.stringify(gesendet));

// --- Bleibt die Antwort aus, ist der Block nach der Wartezeit wieder frei ---
gesendet = [];
uhr.vor(3000);
tippe("✓");
await ruhe();
pruefe(gesendet.length === 1, "Ohne Antwort des Servers bleibt das Telefon stecken.");

// --- Gesperrt: nichts ---
modul.aktualisiere({ remainingMs: 20000, taskNumber: 3, taskText: "2 × 3", ownCorrect: 1, ownAnswered: 2, lastCorrect: false });
pruefe(text("kopfrechnen-hinweis").includes("Falsch"), "Eine falsche Antwort sagt es nicht in Worten.");
gesendet = [];
modul.setLocked(true);
tippe("6", "✓");
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gesendet.");
modul.setLocked(false);

// --- Die Zeit laeuft am Telefon ab: nichts mehr senden ---
uhr.vor(21000);
pruefe(text("kopfrechnen-aufgabe") === "Die Zeit ist um.", "Nach Ablauf steht die Aufgabe noch da: " + text("kopfrechnen-aufgabe"));
tippe("6", "✓");
await ruhe();
pruefe(gesendet.length === 0, "Nach Ablauf der Zeit wurde gesendet.");

// --- Das Ende ---
modul.zeigeEnde({ ownCorrect: 5, ownAnswered: 7, isWinner: false, winnerNames: ["Anna"], winnerCorrect: 9 });
pruefe(text("kopfrechnen-hinweis").includes("Anna") && text("kopfrechnen-hinweis").includes("9"), "Der Sieger fehlt: " + text("kopfrechnen-hinweis"));
pruefe(wurzel.querySelector("kopfrechnen-tasten").hidden === true, "Nach dem Ende steht der Ziffernblock noch da.");
pruefe(endeText({ ownCorrect: 3, ownAnswered: 3, isWinner: true, winnerNames: ["Bert"] }).startsWith("🏆"), "Der Sieg steht nicht da.");
pruefe(endeText({ ownCorrect: 0, ownAnswered: 2, winnerNames: [] }).includes("Niemand"), "Ohne Sieger fehlt der Satz.");
pruefe(restText(1200) === "Noch 2 s", "Die Restzeit wird nicht aufgerundet: " + restText(1200));

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("kopfrechnen: alle Zusicherungen gruen.");
