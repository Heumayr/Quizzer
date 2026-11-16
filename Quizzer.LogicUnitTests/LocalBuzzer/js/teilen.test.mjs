// Minispiel Teilen oder Nehmen (M35 C2) am Telefon: erst die Partnerwahl, dann geheim teilen oder
// nehmen - genau einmal. Den Ausgang kennt das Telefon erst, wenn der Server die fremde Wahl schickt.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { TeilenModul, ausgang } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/teilen.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new TeilenModul();
modul.mount(wurzel);
modul.setLocked(false);

const kopf = () => wurzel.querySelector(".teilen-kopf").textContent;
const text = () => wurzel.querySelector(".teilen-text").textContent;
const tasten = () => wurzel.querySelectorAll(".teilen-taste");

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

// --- Partnerwahl ---
modul.starte({ choosesPartner: true, chooserName: "Anna", options: [{ id: "b-1", name: "Bert" }] }, senden);
pruefe(kopf().includes("Anna wählt einen Partner"), "Der Kopf sagt nicht, wer wählt: " + kopf());
wurzel.querySelector(".spielerwahl-taste").click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].chosen === "b-1", "Die Partnerwahl geht nicht raus: " + JSON.stringify(gesendet));

// --- Teilen oder nehmen: genau eine Wahl ---
gesendet = [];
const paar = { chooserName: "Anna", partnerName: "Bert", isPlayer: true };
modul.starte(paar, senden);
pruefe(tasten().length === 2, "Teilen und Nehmen fehlen.");
tasten()[1].click();
tasten()[0].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].take === true, "Nicht genau ein Nehmen gesendet: " + JSON.stringify(gesendet));
pruefe(text().includes("nimmst"), "Die eigene Wahl steht nicht als Text da: " + text());

// --- Gesperrt: nichts ---
gesendet = [];
modul.starte(paar, senden);
modul.setLocked(true);
tasten()[0].click();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat entschieden.");
modul.setLocked(false);

// --- Ohne fremde Wahl kein Ausgang ---
modul.zeigeErgebnis({ ...paar, ownTake: false, otherTake: null });
pruefe(text().includes("teilst") && text().includes("Auflösung"), "Vor der Auflösung steht ein Ausgang da: " + text());

// Ein Zuschauer bekommt keine Wahl in den Mund gelegt.
modul.zeigeErgebnis({ chooserName: "Anna", partnerName: "Bert", isPlayer: false, ownTake: null, otherTake: null });
pruefe(text().includes("Beamer"), "Der Zuschauer sieht während der Runde keinen Hinweis: " + text());

// --- Die vier Faelle ---
const faelle = [
    [false, false, "Hälfte"], [true, false, "Du nimmst alles"], [false, true, "Gegenüber nimmt alles"], [true, true, "niemand"]
];
for (const [eigene, fremde, erwartet] of faelle) {
    modul.zeigeEnde({ ...paar, ownTake: eigene, otherTake: fremde });
    pruefe(text().includes(erwartet), `Teilen/Nehmen ${eigene}/${fremde}: ${text()}`);
}

pruefe(ausgang(null, true).includes("Spielleiter"), "Eine fehlende Wahl wird nicht gesagt.");

modul.zeigeEnde({ chooserName: "Anna", partnerName: "Bert", isPlayer: false });
pruefe(text().includes("Beamer"), "Ein Zuschauer bekommt keinen Hinweis: " + text());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("teilen: alle Zusicherungen gruen.");
