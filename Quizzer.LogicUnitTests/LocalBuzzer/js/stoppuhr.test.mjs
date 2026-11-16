// Minispiel Stoppuhr (M35 C1) am Telefon: Start durch den Spieler, die Uhr ist 3 s zu sehen und
// verschwindet dann, gestoppt wird mit einem Tipp, gemessen ab dem eigenen Start. Wer nicht
// stoppt, gibt ueber der Grenze selbst auf.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

const { StoppuhrModul, VERDECKT_TEXT, sekunden } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/stoppuhr.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new StoppuhrModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const flaeche = () => wurzel.querySelector(".stoppuhr-taste");
const anzeige = () => wurzel.querySelector(".stoppuhr-anzeige").textContent;
const text = () => wurzel.querySelector(".stoppuhr-text").textContent;
const symbol = () => wurzel.querySelector(".stoppuhr-symbol").textContent;
const tippe = () => (flaeche().hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 }));

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };
const info = { targetSeconds: 10, visibleMs: 3000, maxMs: 20000 };

// --- Bereit: die Uhr laeuft erst mit dem eigenen Tipp ---
modul.starte(info, senden);
pruefe(flaeche().classList.contains("stoppuhr-bereit"), "Vor dem Start ist die Flaeche nicht bereit.");
pruefe(text().includes("10 Sekunden"), "Die Zielzeit steht nicht da: " + text());
pruefe(symbol() !== "", "Vor dem Start fehlt das Symbol.");

uhr.vor(5000);
pruefe(flaeche().classList.contains("stoppuhr-bereit") && gesendet.length === 0,
    "Ohne Tipp ist die Uhr von selbst losgelaufen.");

// --- Gestartet: 3 s sichtbar, die Anzeige laeuft mit ---
tippe();
pruefe(flaeche().classList.contains("stoppuhr-sichtbar"), "Nach dem Start ist die Uhr nicht zu sehen.");

uhr.vor(1500);
pruefe(anzeige() === "1,5 s", "Die sichtbare Uhr laeuft nicht mit: " + anzeige());

uhr.vor(1499);
pruefe(anzeige() !== VERDECKT_TEXT, "Die Uhr verschwindet vor 3 s.");

uhr.vor(1);
pruefe(flaeche().classList.contains("stoppuhr-verdeckt"), "Nach 3 s ist die Uhr nicht verdeckt.");
pruefe(anzeige() === VERDECKT_TEXT, "Die verdeckte Uhr zeigt noch eine Zeit: " + anzeige());
pruefe(symbol() !== "" && text().includes("weg"), "Der Wechsel wird nicht mit Symbol und Text gesagt: " + text());

uhr.vor(1000);
pruefe(anzeige() === VERDECKT_TEXT, "Die verdeckte Uhr zeigt nach einer Sekunde wieder eine Zeit.");

// --- Stopp: gemessen ab dem eigenen Start, ein zweiter Tipp sendet nichts ---
uhr.vor(6230);
tippe();
pruefe(gesendet.length === 1 && gesendet[0].milliseconds === 10230,
    "Gemessen wurde nicht ab dem eigenen Start: " + JSON.stringify(gesendet));
pruefe(text().includes("10,23 s"), "Die gestoppte Zeit steht nicht da: " + text());

tippe();
pruefe(gesendet.length === 1, "Ein zweiter Tipp hat noch einmal gesendet.");
pruefe(uhr.offen() === 0, "Nach dem Stopp laufen noch Uhren.");

// --- Wer nicht stoppt, gibt ueber der Grenze selbst auf ---
gesendet = [];
modul.starte(info, senden);
tippe();
uhr.vor(20000 + 200);
pruefe(gesendet.length === 1 && gesendet[0].milliseconds > 20000,
    "Ohne Stopp gibt das Telefon nicht ueber der Grenze auf: " + JSON.stringify(gesendet));
pruefe(text().includes("Zu spät"), "Das Aufgeben wird nicht gesagt: " + text());

// --- Gesperrt: weder Start noch Stopp ---
gesendet = [];
modul.starte(info, senden);
modul.setLocked(true);
tippe();
pruefe(flaeche().classList.contains("stoppuhr-bereit"), "Ein gesperrtes Telefon hat die Uhr gestartet.");
modul.setLocked(false);

// --- Ergebnis und Ende ---
modul.zeigeErgebnis({ ownMilliseconds: 10230, ownDistanceMs: 230 });
pruefe(text().includes("10,23 s") && text().includes("0,23 s daneben"), "Das Ergebnis nennt Zeit und Abstand nicht: " + text());

modul.zeigeErgebnis({ ownMilliseconds: 25000, ownDistanceMs: null });
pruefe(text().includes("Zu spät"), "Zu spaet wird im Ergebnis nicht gesagt: " + text());

modul.zeigeEnde({ isWinner: false, winnerNames: ["Bert"], winnerDistanceMs: 40, ownMilliseconds: 10230, ownDistanceMs: 230 });
pruefe(text().includes("Bert") && text().includes("0,04 s daneben"), "Am Ende fehlt, wer gewonnen hat: " + text());

modul.zeigeEnde({ isWinner: true, winnerNames: ["Anna"], winnerDistanceMs: 0, ownMilliseconds: 10000, ownDistanceMs: 0 });
pruefe(text().includes("Gewonnen!") && text().includes("genau getroffen"), "Der Sieger erfaehrt es nicht: " + text());
pruefe(sekunden(9876) === "9,88 s", "Die Sekunden werden nicht deutsch geschrieben: " + sekunden(9876));

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufraeumen laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("stoppuhr: alle Zusicherungen gruen.");
