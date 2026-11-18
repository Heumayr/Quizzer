// Minispiel Tauziehen (M37 T9) am Telefon: gezaehlt wird lokal, nie ueber der Grenze von 20 je Sekunde; im Takt geht der
// Stand an den Server, auch ohne neuen Tipp - die Antwort bringt das Seil. Die Taste traegt die Teamfarbe nur als Klasse,
// Teamnamen nur als Text; das Seil steht nur bei zwei Teams. Nach dem Wiederverbinden zaehlt die Restzeit des Servers.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

const { TauziehenModul, knotenProzent, teamText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/tauziehen.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new TauziehenModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const taste = () => wurzel.querySelector(".tauziehen-taste");
const zahl = () => wurzel.querySelector(".tauziehen-zahl").textContent;
const text = () => wurzel.querySelector(".tauziehen-text").textContent;
const seil = () => wurzel.querySelector(".tauziehen-seil");
const knoten = () => wurzel.querySelector(".tauziehen-knoten");
const stand = () => wurzel.querySelector(".tauziehen-stand").textContent;
const tippe = (n = 1) => { for (let i = 0; i < n; i++) (taste().hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 })); };

const rot = { name: "Rot", symbol: "✚", colorIndex: 5, pull: "Ø 4,0 Tipps", isOwn: true, isWinner: false };
const blau = { name: "<b>Blau</b>", symbol: "★", colorIndex: 4, pull: "Ø 2,0 Tipps", isOwn: false, isWinner: false };

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };
const info = { durationSeconds: 5, maxTapsPerSecond: 20, reportIntervalMs: 500, elapsedMs: 0, teams: [rot, blau], rope: -0.5 };

// --- Start: Taste in der Teamfarbe, Seil mit beiden Teams ---
modul.starte(info, senden);
pruefe(taste().classList.contains("tauziehen-aktiv"), "Zu Beginn ist die Taste nicht im Ziehen.");
pruefe(taste().classList.contains("team-farbe-5"), "Die Taste traegt nicht die Farbe des eigenen Teams.");
pruefe(!taste().classList.contains("team-farbe-4"), "Die Taste traegt die Farbe des Gegners.");
pruefe(zahl() === "0", "Zu Beginn steht nicht 0 da: " + zahl());
pruefe(text().includes("Noch 5 s"), "Die Restzeit steht nicht da: " + text());
pruefe(!seil().hidden, "Bei zwei Teams fehlt das Seil.");
pruefe(knoten().style.left === "25%", "Der Knoten steht nicht beim staerkeren Team: " + knoten().style.left);
pruefe(wurzel.querySelector(".tauziehen-rechts").textContent === "★ <b>Blau</b>: Ø 2,0 Tipps",
    "Der Teamname steht nicht woertlich als Text da: " + wurzel.querySelector(".tauziehen-rechts").textContent);
pruefe(stand().startsWith("Euer Team ✚ Rot"), "Das eigene Team steht nicht vorn: " + stand());

// --- Die Grenze: nach 100 ms sind hoechstens 2 Tipps moeglich ---
uhr.vor(100);
tippe(5);
pruefe(zahl() === "2", "Ueber 20 Tipps je Sekunde wurde weitergezaehlt: " + zahl());

// --- Im Takt der Stand, ohne Zeit - die misst der Server ---
uhr.vor(400);
pruefe(gesendet.length === 1 && gesendet[0].taps === 2 && gesendet[0].isFinal === false,
    "Nach 0,5 s ging kein Stand raus: " + JSON.stringify(gesendet));
pruefe(!("elapsedMs" in gesendet[0]), "Das Telefon schickt eine Zeit mit - entscheiden soll die Serveruhr.");

uhr.vor(500);
pruefe(gesendet.length === 2 && gesendet[1].taps === 2, "Ohne neuen Tipp ging kein Stand raus - dann kaeme das Seil nicht zurueck.");

// --- Die Antwort des Servers bewegt das Seil, die Zahl bleibt ---
modul.aktualisiere({ ...info, rope: 0.5, teams: [{ ...rot, pull: "Ø 1,0 Tipps" }, { ...blau, pull: "Ø 3,0 Tipps" }] });
pruefe(knoten().style.left === "75%", "Das Seil folgt dem Stand des Servers nicht: " + knoten().style.left);
pruefe(stand().includes("Ø 3,0 Tipps"), "Der Stand der Gegner kommt nicht an: " + stand());
pruefe(zahl() === "2", "Die Antwort hat die eigene Zahl veraendert.");

// --- Ende nach der Dauer: Endstand, danach zaehlt nichts mehr ---
tippe(4);
uhr.vor(4000);
const ende = gesendet.at(-1);
pruefe(ende?.isFinal === true && ende.taps === 6, "Nach der Dauer kam kein Endstand: " + JSON.stringify(ende));
pruefe(gesendet.filter(g => g.isFinal).length === 1, "Der Endstand ging mehr als einmal raus.");
pruefe(text().includes("Zeit um"), "Das Ende wird nicht gesagt: " + text());

const vorher = gesendet.length;
tippe(3);
uhr.vor(2000);
pruefe(gesendet.length === vorher && zahl() === "6", "Nach dem Ende wurde weitergezaehlt oder gesendet.");
pruefe(uhr.offen() === 0, "Nach dem Ende laufen noch Uhren.");

// --- Wiederverbinden: es geht beim letzten Stand weiter, die Restzeit kommt vom Server ---
gesendet = [];
modul.starte({ ...info, ownTaps: 7, elapsedMs: 3000 }, senden);
pruefe(zahl() === "7", "Nach dem Wiederverbinden beginnt das Ziehen wieder bei 0: " + zahl());
uhr.vor(1999);
pruefe(!gesendet.some(g => g.isFinal), "Der Endstand kam vor Ablauf der Restzeit.");
uhr.vor(1);
pruefe(gesendet.at(-1)?.isFinal === true && gesendet.at(-1).taps === 7, "Nach dem Wiederverbinden stimmt der Endstand nicht.");

// --- Gesperrt: kein Tipp zaehlt ---
gesendet = [];
modul.starte(info, senden);
modul.setLocked(true);
uhr.vor(1000);
tippe(5);
pruefe(zahl() === "0", "Ein gesperrtes Telefon hat gezaehlt.");
modul.setLocked(false);

// --- Drei Teams: kein Seil, der Stand als Text; ohne eigenes Team neutral ---
modul.zeigeErgebnis({ ownTaps: 42, rope: null, teams: [{ ...rot, isOwn: false }, blau, { name: "Gelb", symbol: "◆", colorIndex: 3, pull: "Ø 9,0 Tipps" }] });
pruefe(seil().hidden, "Bei drei Teams steht ein Seil da.");
pruefe(!taste().classList.contains("team-farbe-5"), "Ohne eigenes Team bleibt die alte Teamfarbe stehen.");
pruefe(stand().includes("◆ Gelb: Ø 9,0 Tipps"), "Der Stand des dritten Teams fehlt: " + stand());
pruefe(text().includes("42 Tipps"), "Das Rundenergebnis nennt die eigenen Tipps nicht: " + text());

// --- Eine Farbe ausserhalb der Palette setzt keine Klasse ---
modul.zeigeErgebnis({ ownTaps: 1, rope: 0, teams: [{ ...rot, colorIndex: 9 }, blau] });
pruefe(![...Array(12).keys()].some(i => taste().classList.contains(`team-farbe-${i}`)), "Eine Farbe ausserhalb der Palette wurde gesetzt.");

// --- Ende: wer gewonnen hat, als Text ---
modul.zeigeEnde({ isWinner: false, ownTaps: 42, rope: 0.2, teams: [rot, { ...blau, isWinner: true }] });
pruefe(text().includes("Gewonnen: ★ <b>Blau</b>") && text().includes("42 Tipps"), "Am Ende fehlt, wer gewonnen hat: " + text());

modul.zeigeEnde({ isWinner: true, ownTaps: 55, rope: -0.2, teams: [{ ...rot, isWinner: true }, blau] });
pruefe(text().includes("Gewonnen!"), "Das Siegerteam erfaehrt es nicht: " + text());

modul.zeigeEnde({ isWinner: false, teams: [rot, blau], rope: 0 });
pruefe(text().includes("Kein Team"), "Ohne Sieger steht kein Satz da: " + text());

// --- Hilfen ---
pruefe(knotenProzent(-1) === 0 && knotenProzent(1) === 100 && knotenProzent(0) === 50 && knotenProzent(7) === 100,
    "Der Knoten verlaesst das Seil.");
pruefe(teamText({ symbol: "●", name: "Orange" }) === "● Orange", "Ohne Zug stimmt der Teamtext nicht.");

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufraeumen laufen noch Uhren.");

// --- V24: im Rahmen neu verbunden - gezaehlt wird nur der Rest des Countdowns, der Endstand kommt zur Serverzeit ---
if (!globalThis.navigator.vibrate) {
    Object.defineProperty(globalThis.navigator, "vibrate", { value: () => {} });
}

const { MinigameLayout } = await import("../../../LocalBuzzer.Service/wwwroot/JS/minispiele/rahmen.js");

// Wiederverbunden "rest" ms vor Ende des Countdowns bzw. "gezogen" ms nach seinem Ende; Dauer 10 s.
function neuVerbunden(rest, gezogen) {
    const wirt = document.createElement("div");
    const senden = [];
    const start = uhr.now();
    const rahmen = new MinigameLayout(
        { submitAction: async (aktion) => { senden.push({ t: uhr.now() - start, ...aktion.payload }); } },
        { timer: uhr.timer, now: uhr.now });

    rahmen.render(wirt, {
        resetCount: 1, currentLayoutLocked: false, allLocked: false,
        layoutInfo: {
            kind: 17, title: "Tauziehen", phase: 1, currentRound: 1, totalRounds: 1, durchgang: 9,
            countdownSeconds: 3, countdownRemainingMs: rest, ownSubmitted: false,
            kindInfo: { ...info, durationSeconds: 10, elapsedMs: gezogen, ownTaps: 4 }
        }
    });
    rahmen.setLocked(false);
    const status = wirt.querySelector(".minispiel-status").textContent;

    uhr.vor(20000);
    rahmen.dispose();
    return { status, ende: senden.find(s => s.isFinal)?.t, erste: senden[0]?.t };
}

const mitten = neuVerbunden(0, 4000);
pruefe(!mitten.status.startsWith("Gleich geht es los"), "Mitten im Ziehen zaehlt das Telefon den Countdown neu: " + mitten.status);
pruefe(mitten.ende === 6000, `Der Endstand kommt nicht mit dem Server nach 6 s, sondern nach ${mitten.ende} ms (Frist 7,5 s).`);
pruefe(mitten.erste === 500, "Nach dem Wiederverbinden meldet das Telefon nicht im Takt: " + mitten.erste);

const imCountdown = neuVerbunden(1400, 0);
pruefe(imCountdown.status === "Gleich geht es los: 2", "Im Countdown zaehlt das Telefon nicht den Rest: " + imCountdown.status);
pruefe(imCountdown.ende === 11400, `Im Countdown verbunden endet das Ziehen nicht mit dem Server nach 11,4 s: ${imCountdown.ende}`);

const frisch = neuVerbunden(3000, 0);
pruefe(frisch.status === "Gleich geht es los: 3" && frisch.ende === 13000,
    `Zu Beginn des Durchgangs zaehlt das Telefon nicht die vollen 3 s: ${frisch.status}, Ende ${frisch.ende}`);
pruefe(uhr.offen() === 0, "Nach dem Wiederverbinden laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("tauziehen: alle Zusicherungen gruen.");
