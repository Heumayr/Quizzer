// Minispiel Tippsprint (M35 C1) am Telefon: gezaehlt wird lokal, Zwischenstaende im Takt des
// Servers (nur bei Aenderung), Endstand nach der Dauer, nie ueber der Grenze von 20 je Sekunde,
// nach dem Wiederverbinden geht es beim letzten Stand weiter.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

const { TippsprintModul, tippText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/tippsprint.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new TippsprintModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const flaeche = () => wurzel.querySelector(".tippsprint-taste");
const zahl = () => wurzel.querySelector(".tippsprint-zahl").textContent;
const text = () => wurzel.querySelector(".tippsprint-text").textContent;
const symbol = () => wurzel.querySelector(".tippsprint-symbol").textContent;
const tippe = (n = 1) => { for (let i = 0; i < n; i++) (flaeche().hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 })); };

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };
const info = { durationSeconds: 5, maxTapsPerSecond: 20, reportIntervalMs: 500 };

// --- Start: Flaeche offen, Symbol und Text dabei ---
modul.starte(info, senden);
pruefe(flaeche().classList.contains("tippsprint-aktiv"), "Zu Beginn ist die Flaeche nicht im Sprint.");
pruefe(symbol() !== "", "Im Sprint fehlt das Symbol - dann traegt nur die Farbe die Information.");
pruefe(zahl() === "0", "Zu Beginn steht nicht 0 da: " + zahl());
pruefe(text().includes("Noch 5 s"), "Die Restzeit steht nicht da: " + text());

// --- Die Grenze: nach 100 ms sind hoechstens 2 Tipps moeglich ---
uhr.vor(100);
tippe(5);
pruefe(zahl() === "2", "Ueber 20 Tipps je Sekunde wurde weitergezaehlt: " + zahl());

// --- Zwischenstand im Takt, mit der am Telefon gemessenen Zeit ---
uhr.vor(400);
pruefe(gesendet.length === 1, "Nach 0,5 s ging kein Zwischenstand raus: " + JSON.stringify(gesendet));
pruefe(gesendet[0]?.taps === 2 && gesendet[0]?.elapsedMs === 500 && gesendet[0]?.isFinal === false,
    "Der Zwischenstand stimmt nicht: " + JSON.stringify(gesendet[0]));

uhr.vor(500);
pruefe(gesendet.length === 1, "Ohne neuen Tipp ging trotzdem ein Zwischenstand raus.");

tippe(3);
uhr.vor(500);
pruefe(gesendet.length === 2 && gesendet[1].taps === 5 && gesendet[1].elapsedMs === 1500,
    "Der zweite Zwischenstand stimmt nicht: " + JSON.stringify(gesendet[1]));

// --- Ende nach der Dauer: Endstand, danach zaehlt nichts mehr ---
tippe(4);
uhr.vor(3500);
const ende = gesendet.at(-1);
pruefe(ende?.isFinal === true && ende.taps === 9 && ende.elapsedMs === 5000,
    "Nach der Dauer kam kein Endstand: " + JSON.stringify(ende));
pruefe(gesendet.filter(g => g.isFinal).length === 1, "Der Endstand ging mehr als einmal raus.");
pruefe(text().includes("Zeit um"), "Das Ende wird nicht gesagt: " + text());
pruefe(zahl() === "9", "Nach dem Ende steht nicht der Endstand da: " + zahl());

const vorher = gesendet.length;
tippe(3);
uhr.vor(2000);
pruefe(gesendet.length === vorher && zahl() === "9", "Nach dem Ende wurde weitergezaehlt oder gesendet.");
pruefe(uhr.offen() === 0, "Nach dem Ende laufen noch Uhren.");

// --- Wiederverbinden: es geht beim letzten Stand weiter, die Restzeit zaehlt ---
gesendet = [];
modul.starte({ ...info, ownTaps: 7, ownElapsedMs: 2000 }, senden);
pruefe(zahl() === "7", "Nach dem Wiederverbinden beginnt der Sprint wieder bei 0: " + zahl());
uhr.vor(2999);
pruefe(!gesendet.some(g => g.isFinal), "Der Endstand kam vor Ablauf der Restzeit.");
uhr.vor(1);
pruefe(gesendet.at(-1)?.isFinal === true && gesendet.at(-1).taps === 7 && gesendet.at(-1).elapsedMs === 5000,
    "Nach dem Wiederverbinden stimmt der Endstand nicht: " + JSON.stringify(gesendet.at(-1)));

// --- Gesperrt: kein Tipp zaehlt ---
gesendet = [];
modul.starte(info, senden);
modul.setLocked(true);
uhr.vor(1000);
tippe(5);
pruefe(zahl() === "0", "Ein gesperrtes Telefon hat gezaehlt.");
modul.setLocked(false);

// --- Ergebnis und Ende ---
modul.zeigeErgebnis({ ownTaps: 42 });
pruefe(text().includes("42 Tipps"), "Das Rundenergebnis nennt die eigenen Tipps nicht: " + text());

modul.zeigeEnde({ isWinner: false, winnerNames: ["Bert"], winnerTaps: 55, ownTaps: 42 });
pruefe(text().includes("Bert") && text().includes("55 Tipps") && text().includes("42 Tipps"),
    "Am Ende fehlt, wer gewonnen hat: " + text());

modul.zeigeEnde({ isWinner: true, winnerNames: ["Anna"], winnerTaps: 55, ownTaps: 55 });
pruefe(text().includes("Gewonnen!"), "Der Sieger erfaehrt es nicht: " + text());
pruefe(tippText(1) === "1 Tipp", "Ein einzelner Tipp heisst nicht '1 Tipp'.");

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufraeumen laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("tippsprint: alle Zusicherungen gruen.");
