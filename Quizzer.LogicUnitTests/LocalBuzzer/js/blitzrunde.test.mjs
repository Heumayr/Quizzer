// Minispiel Blitzrunde (M35 C5) am Telefon: zwei Knoepfe, genau ein Urteil je Aussage, die naechste
// Aussage kommt nur vom Server. Ob eine Antwort stimmte, steht erst nach der Runde da.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { BlitzrundeModul, endeText, restText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/blitzrunde.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new BlitzrundeModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const wahr = () => wurzel.querySelector("blitzrunde-wahr");
const falsch = () => wurzel.querySelector("blitzrunde-falsch");
const text = (klasse) => wurzel.querySelector(klasse).textContent;

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const aussage = (nummer, inhalt, beantwortet, rest = 30000) =>
    ({ durationSeconds: 30, remainingMs: rest, totalStatements: 4, statementNumber: nummer, statementText: inhalt, ownAnswered: beantwortet, ownCorrect: null, ownWrong: null });

// --- Vor dem Start: keine Knoepfe ---
pruefe(wurzel.querySelector("blitzrunde-tasten").hidden === true, "Vor dem Start stehen die Knoepfe schon da.");
pruefe(wahr().textContent.includes("Wahr") && falsch().textContent.includes("Falsch"), "Die Knoepfe sagen es nicht in Worten.");
pruefe(wahr().getAttribute("aria-label") && falsch().getAttribute("aria-label"), "Die Knoepfe haben keine Beschriftung fuer Vorleser.");

// --- Start: die Aussage des Servers, genau ein Urteil ---
modul.starte(aussage(1, "Der Mond ist ein Planet.", 0), senden);
pruefe(text("blitzrunde-aussage") === "Der Mond ist ein Planet.", "Die Aussage steht nicht da: " + text("blitzrunde-aussage"));
pruefe(text("blitzrunde-hinweis").includes("Aussage 1 von 4"), "Die Nummer fehlt: " + text("blitzrunde-hinweis"));
pruefe(text("blitzrunde-stand").includes("Noch 30 s"), "Die Restzeit fehlt: " + text("blitzrunde-stand"));

falsch().click();
wahr().click();
await ruhe();
pruefe(gesendet.length === 1, "Nicht genau ein Urteil: " + JSON.stringify(gesendet));
pruefe(gesendet[0]?.statementNumber === 1 && gesendet[0]?.answer === false, "Falsches Urteil: " + JSON.stringify(gesendet[0]));

// --- Die Antwort des Servers: naechste Aussage, kein Wort darueber, ob die letzte stimmte ---
modul.aktualisiere(aussage(2, "Wasser kocht bei 100 °C.", 1, 28000));
pruefe(text("blitzrunde-aussage") === "Wasser kocht bei 100 °C.", "Die naechste Aussage kommt nicht an.");
pruefe(text("blitzrunde-stand").startsWith("1 von 4 beantwortet"), "Der Fortschritt fehlt: " + text("blitzrunde-stand"));
pruefe(!/richtig|falsch\b/i.test(text("blitzrunde-hinweis").replace("wahr oder falsch", "")),
    "Waehrend der Runde sagt das Telefon, ob die Antwort stimmte: " + text("blitzrunde-hinweis"));

// --- Verworfen (dieselbe Aussage kommt zurueck): man darf erneut urteilen ---
gesendet = [];
wahr().click();
modul.aktualisiere(aussage(2, "Wasser kocht bei 100 °C.", 1, 27000));
wahr().click();
await ruhe();
pruefe(gesendet.length === 2 && gesendet.every(g => g.statementNumber === 2 && g.answer === true),
    "Nach dem Verwerfen darf nicht erneut geurteilt werden: " + JSON.stringify(gesendet));

// --- Bleibt die Antwort aus, sind die Knoepfe nach der Wartezeit wieder frei ---
gesendet = [];
uhr.vor(3000);
falsch().click();
await ruhe();
pruefe(gesendet.length === 1, "Ohne Antwort des Servers bleibt das Telefon stecken.");

// --- Gesperrt: nichts ---
modul.aktualisiere(aussage(3, "Berlin liegt an der Spree.", 2, 20000));
gesendet = [];
modul.setLocked(true);
wahr().click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gesendet.");
modul.setLocked(false);

// --- Die Zeit laeuft am Telefon ab: nichts mehr senden ---
uhr.vor(21000);
pruefe(text("blitzrunde-aussage") === "Die Zeit ist um.", "Nach Ablauf steht die Aussage noch da: " + text("blitzrunde-aussage"));
wahr().click();
await ruhe();
pruefe(gesendet.length === 0, "Nach Ablauf der Zeit wurde gesendet.");

// --- Alle beantwortet, Runde noch offen: noch keine Aufloesung ---
modul.zeigeErgebnis({ totalStatements: 4, ownAnswered: 4, ownCorrect: null, ownWrong: null });
pruefe(wurzel.querySelector("blitzrunde-tasten").hidden === true, "Nach dem Abgeben stehen die Knoepfe noch da.");
pruefe(text("blitzrunde-hinweis").includes("Auflösung"), "Vor dem Ende wird schon aufgeloest: " + text("blitzrunde-hinweis"));

// --- Das Ende: richtig und falsch in Worten und Zeichen ---
modul.zeigeEnde({ totalStatements: 4, ownAnswered: 3, ownCorrect: 2, ownWrong: 1 });
pruefe(text("blitzrunde-hinweis").includes("2 richtig") && text("blitzrunde-hinweis").includes("1 falsch")
    && text("blitzrunde-hinweis").includes("1 ohne Antwort"), "Das Ergebnis fehlt: " + text("blitzrunde-hinweis"));
pruefe(endeText({ totalStatements: 2, ownCorrect: 2, ownWrong: 0 }).startsWith("✓ 2 richtig"), "Das Ergebnis traegt kein Zeichen.");
pruefe(restText(1200) === "Noch 2 s", "Die Restzeit wird nicht aufgerundet: " + restText(1200));

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("blitzrunde: alle Zusicherungen gruen.");
