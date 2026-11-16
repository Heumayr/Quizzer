// Minispiel Heisse Kartoffel (M35 C6) am Telefon: nur der Halter sieht Aussage und Knoepfe, je Tipp genau eine Abgabe mit
// der Nummer des Servers; weitergeben erst, wenn der Server „richtig" geschickt hat. Die anderen sehen nur, wer haelt. Keine
// Uhr: das Modul stellt nichts, was eine Restzeit zeigen koennte. Nach der Runde die Aufloesung in Worten.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();
const uhr = baueUhr();

const { KartoffelModul, halterText, endeText, aufloesungText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/kartoffel.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const neu = () => {
    wurzel.children = [];
    const modul = new KartoffelModul({ timer: uhr.timer });
    modul.mount(wurzel);
    modul.setLocked(false);
    return modul;
};

const text = (klasse) => wurzel.querySelector(klasse).textContent;
const tasten = () => wurzel.querySelector("kartoffel-tasten");
const wahr = () => wurzel.querySelector("kartoffel-wahr");
const falsch = () => wurzel.querySelector("kartoffel-falsch");
const ziele = () => wurzel.querySelectorAll("kartoffel-ziel");

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const stand = (mehr = {}) => ({
    holderName: "Anna", passerName: null, isHolder: false, statementNumber: 0, statementText: null, lastWrong: false,
    choosing: false, options: [], exploded: false, fuseSeconds: null, resolution: [], ...mehr
});

// --- Wer nicht haelt: nur der Name, keine Knoepfe, nichts geht hinaus ---
let modul = neu();
modul.starte(stand(), senden);
pruefe(text("kartoffel-halter").includes("Anna hat die heiße Kartoffel"), "Wer haelt, steht nicht da: " + text("kartoffel-halter"));
pruefe(tasten().hidden === true, "Wer nicht haelt, bekommt wahr/falsch.");
pruefe(text("kartoffel-aussage") === "", "Wer nicht haelt, sieht eine Aussage.");
wahr().click();
falsch().click();
await ruhe();
pruefe(gesendet.length === 0, "Ein Telefon ohne Kartoffel sendet.");
pruefe(uhr.offen() === 0, "Das Modul stellt eine Uhr - eine Restzeit verriete den Zuender.");
modul.dispose();

// --- Der Halter: Aussage und Knoepfe; ein Tipp, eine Abgabe mit der Nummer des Servers ---
modul = neu();
modul.starte(stand({ isHolder: true, statementNumber: 3, statementText: "Der Mond ist ein Planet." }), senden);
pruefe(text("kartoffel-halter").includes("Du hast die heiße Kartoffel"), "Der Halter erfaehrt es nicht: " + text("kartoffel-halter"));
pruefe(text("kartoffel-aussage") === "Der Mond ist ein Planet.", "Die Aussage fehlt: " + text("kartoffel-aussage"));
pruefe(tasten().hidden === false, "Der Halter hat keine Knoepfe.");
pruefe(wahr().getAttribute("aria-label") === "Die Aussage ist wahr" && wahr().textContent.includes("Wahr"),
    "Der Knopf sagt nicht in Worten, wofuer er steht.");
pruefe(ziele().length === 0, "Vor der richtigen Antwort stehen Ziele da.");

falsch().click();
falsch().click();
wahr().click();
await ruhe();
pruefe(gesendet.length === 1, `Ein Urteil ging ${gesendet.length}-mal hinaus.`);
pruefe(JSON.stringify(gesendet[0]) === JSON.stringify({ statementNumber: 3, answer: false }),
    "Die Abgabe traegt nicht Nummer und Urteil: " + JSON.stringify(gesendet[0]));

// Daneben: die naechste Aussage kommt vom Server, und es geht weiter.
modul.aktualisiere(stand({ isHolder: true, statementNumber: 4, statementText: "Berlin liegt an der Spree.", lastWrong: true }));
pruefe(text("kartoffel-hinweis").includes("Daneben"), "Der Fehlgriff steht nicht in Worten da: " + text("kartoffel-hinweis"));
pruefe(text("kartoffel-aussage") === "Berlin liegt an der Spree.", "Die naechste Aussage fehlt.");
wahr().click();
await ruhe();
pruefe(gesendet.length === 2 && gesendet[1].statementNumber === 4 && gesendet[1].answer === true,
    "Nach der Antwort des Servers geht das naechste Urteil nicht oder mit alter Nummer: " + JSON.stringify(gesendet[1]));

// Richtig: jetzt die Ziele des Servers, keine Knoepfe mehr - weitergeben genau einmal, mit derselben Nummer.
modul.aktualisiere(stand({ isHolder: true, statementNumber: 4, choosing: true,
    options: [{ id: "id-bert", name: "Bert" }, { id: "id-carl", name: "Carl" }] }));
pruefe(tasten().hidden === true, "Beim Weitergeben stehen wahr/falsch noch da.");
pruefe(ziele().length === 2 && ziele()[0].textContent.includes("Bert"), "Die Ziele des Servers fehlen.");
pruefe(text("kartoffel-hinweis").includes("Richtig"), "Dass es richtig war, steht nicht da: " + text("kartoffel-hinweis"));
wahr().click();
await ruhe();
pruefe(gesendet.length === 2, "Beim Weitergeben geht noch ein Urteil hinaus.");
ziele()[0].click();
ziele()[1].click();
await ruhe();
pruefe(gesendet.length === 3, `Das Weitergeben ging ${gesendet.length - 2}-mal hinaus.`);
pruefe(JSON.stringify(gesendet[2]) === JSON.stringify({ statementNumber: 4, passTo: "id-bert" }),
    "Die Weitergabe traegt nicht Nummer und Ziel: " + JSON.stringify(gesendet[2]));

// Weg ist sie: Bert haelt, dieses Telefon hat nichts mehr zu tun.
modul.aktualisiere(stand({ holderName: "Bert", passerName: "Anna" }));
pruefe(text("kartoffel-halter").includes("Bert hat die heiße Kartoffel"), "Der neue Halter steht nicht da.");
pruefe(tasten().hidden === true && ziele().length === 0, "Nach dem Weitergeben bleiben Knoepfe stehen.");
wahr().click();
await ruhe();
pruefe(gesendet.length === 3, "Nach dem Weitergeben sendet das Telefon noch.");
pruefe(uhr.offen() === 0, "Nach dem Weitergeben laeuft eine Uhr.");
modul.dispose();

// --- Waehrend des Countdowns gewandert: der neueste Stand gilt, nicht der vom Beginn ---
modul = neu();
modul.leeren();
modul.aktualisiere(stand({ holderName: "Carl", isHolder: true, statementNumber: 2, statementText: "Neu." }));
modul.starte(stand({ isHolder: false }), senden);
pruefe(text("kartoffel-aussage") === "Neu." && tasten().hidden === false, "Nach dem Countdown gilt ein alter Stand.");
modul.dispose();

// --- Nach der Runde: wer sie hatte, und die Aussagen samt Wahrheit ---
modul = neu();
modul.starte(stand({ isHolder: true, statementNumber: 1, statementText: "X" }), senden);
modul.zeigeErgebnis(stand({ holderName: "Bert", passerName: "Anna", exploded: true, fuseSeconds: 41,
    resolution: [{ text: "Der Mond ist ein Planet.", isTrue: false }, { text: "Berlin liegt an der Spree.", isTrue: true }] }));
pruefe(text("kartoffel-halter") === "💥 BUMM nach 41 s! Bei Bert ist sie geplatzt – Anna hat sie zugeschoben.",
    "Der Knall steht nicht in Worten da: " + text("kartoffel-halter"));
const zeilen = wurzel.querySelectorAll("kartoffel-ergebnis").map(z => z.textContent);
pruefe(zeilen.length === 2 && zeilen[0] === "✗ falsch – Der Mond ist ein Planet." && zeilen[1] === "✓ wahr – Berlin liegt an der Spree.",
    "Die Aufloesung fehlt: " + JSON.stringify(zeilen));
pruefe(tasten().hidden === true, "Nach der Runde stehen Knoepfe da.");
wahr().click();
await ruhe();
pruefe(gesendet.length === 3, "Nach der Runde sendet das Telefon.");

pruefe(endeText({ exploded: true, isHolder: true, fuseSeconds: 30 }) === "💥 BUMM nach 30 s! Bei dir ist sie geplatzt.", "Der eigene Knall fehlt.");
pruefe(endeText({ exploded: false }).includes("ohne Knall"), "Ohne Knall steht ein Knall da.");
pruefe(halterText({}) === "Gleich geht es los", "Ohne Halter steht ein Name da.");
pruefe(aufloesungText({ text: "A", isTrue: true }) === "✓ wahr – A", "Die Wahrheit steht nicht in Zeichen und Wort da.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("kartoffel: alle Zusicherungen gruen.");
