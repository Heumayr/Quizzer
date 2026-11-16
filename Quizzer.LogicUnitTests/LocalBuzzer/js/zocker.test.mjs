// Minispiel Zocker (M35 C4) am Telefon: ein Knopf, genau ein Ausstieg. Den Absturzpunkt kennt das
// Telefon erst, wenn der Server ihn nach der Runde schickt.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { ZockerModul, ausgang, multiplikatorText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/zocker.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new ZockerModul();
modul.mount(wurzel);
modul.setLocked(false);

const taste = () => wurzel.querySelector(".zocker-taste");
const text = () => wurzel.querySelector(".zocker-text").textContent;

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

// --- Vor dem Start: kein Knopf ---
pruefe(taste().hidden === true, "Vor dem Start steht der Knopf schon da.");

// --- Genau ein Ausstieg ---
modul.starte({ ownMultiplier: null, crashMultiplier: null, crashed: false }, senden);
pruefe(taste().hidden === false, "Nach dem Start fehlt der Knopf.");
taste().click();
taste().click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].cashOut === true, "Nicht genau ein Ausstieg gesendet: " + JSON.stringify(gesendet));
pruefe(taste().disabled === true, "Nach dem Ausstieg ist der Knopf noch bedienbar.");
pruefe(text().includes("gesendet"), "Der Satz sagt nicht, dass der Ausstieg unterwegs ist: " + text());

// --- Gesperrt: nichts ---
gesendet = [];
modul.starte({}, senden);
modul.setLocked(true);
taste().click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon ist ausgestiegen.");
modul.setLocked(false);

// --- Nach dem Leeren schickt der Knopf nichts mehr ---
modul.leeren();
taste().click();
await ruhe();
pruefe(gesendet.length === 0, "Ein geleertes Modul schickt noch.");

// --- Abgegeben: der eigene Wert, ohne Absturzpunkt ---
modul.zeigeErgebnis({ ownMultiplier: 185, crashMultiplier: null, crashed: false });
pruefe(text().includes("×1,85"), "Der eigene Ausstieg steht nicht da: " + text());
pruefe(taste().hidden === true, "Nach der Abgabe steht der Knopf noch da.");

// --- Die Ausgaenge ---
modul.zeigeEnde({ ownMultiplier: null, crashMultiplier: 240, crashed: true });
pruefe(text().includes("Abgestürzt bei ×2,40"), "Der Absturz steht nicht da: " + text());

modul.zeigeEnde({ ownMultiplier: 185, crashMultiplier: 240, crashed: true });
pruefe(text().includes("×1,85") && text().includes("abgestürzt ist es bei ×2,40"), "Ausstieg vor dem Absturz: " + text());

modul.zeigeEnde({ ownMultiplier: 185, crashMultiplier: 240, crashed: false });
pruefe(text().includes("wäre"), "Alle draussen - der Satz sagt nicht, dass es nur abgestürzt wäre: " + text());

pruefe(ausgang({ ownMultiplier: null, crashMultiplier: null }).includes("Spielleiter"), "Ohne Absturzpunkt und Ausstieg fehlt der Hinweis.");
pruefe(multiplikatorText(1000) === "×10,00", "Die Schreibweise weicht vom Beamer ab: " + multiplikatorText(1000));

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("zocker: alle Zusicherungen gruen.");
