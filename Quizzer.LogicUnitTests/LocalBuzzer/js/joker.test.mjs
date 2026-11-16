// Joker-Runde am Telefon (M35 C7b): nur die eigenen Joker und „Keinen Joker", die Sperre fragt nach dem Ziel,
// gesendet wird genau einmal { jokerId, target }, gesperrt nichts. Der Rahmen waehlt das Modul ueber prePhase
// und schickt prePhase mit. Nach dem Schliessen: wer welchen Joker eingesetzt hat, und wer gesperrt ist.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

const { JokerModul, eigeneWahlText, KEIN_JOKER } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/joker/joker.js");
const { MinigameLayout } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/rahmen.js");
const { PrePhases } = await import("../../../LocalBuzzer.Service/wwwroot/JS/constants.js");

let fehler = 0;

const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const optionen = [
    { id: "d-1", name: "Doppelt", effect: "Zählt doppelt.", needsTarget: false },
    { id: "s-1", name: "Sperre", effect: "Sperrt einen.", needsTarget: true },
];
const ziele = [{ id: "b-2", name: "Bert" }, { id: "c-3", name: "Clara" }];
const info = { options: optionen, targets: ziele };

const joker = () => [...wurzel.querySelectorAll(".joker-knopf")];
const keiner = () => wurzel.querySelector(".joker-keiner");
const zielTasten = () => [...wurzel.querySelectorAll(".joker-ziel")];
const text = () => wurzel.querySelector(".joker-text")?.textContent ?? "";

function neu(senden) {
    wurzel.innerHTML = "";
    const modul = new JokerModul();
    modul.mount(wurzel);
    modul.starte(info, senden);
    return modul;
}

// --- Gesperrt: nichts geht raus ---
const gesendet = [];
let modul = neu(async (p) => { gesendet.push(p); });

pruefe(joker().length === 2 && keiner() != null, "Nicht je eigenem Joker eine Taste plus „Keinen Joker“.");
pruefe(keiner()?.textContent === KEIN_JOKER, "Die Taste ohne Joker heißt anders: " + keiner()?.textContent);
joker()[0].click();
keiner().click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat einen Joker gesetzt.");

// --- Sperre: erst das Ziel, dann genau eine Abgabe ---
modul.setLocked(false);
joker()[1].click();
await ruhe();
pruefe(gesendet.length === 0, "Die Sperre ging ohne Ziel raus.");
pruefe(zielTasten().length === 2, "Die Sperre zeigt nicht die Ziele des Servers.");
pruefe(text().includes("Wen sperrst du"), "Die Sperre fragt nicht nach dem Ziel: " + text());

wurzel.querySelector(".joker-abbruch").click();
pruefe(joker().length === 2 && zielTasten().length === 0, "„Zurück“ bringt die Joker nicht zurück.");

joker()[1].click();
zielTasten()[1].click();
zielTasten()[0]?.click();
await ruhe();
pruefe(gesendet.length === 1, "Nicht genau eine Abgabe: " + JSON.stringify(gesendet));
pruefe(gesendet[0]?.jokerId === "s-1" && gesendet[0]?.target === "c-3", "Nicht die gewählte Sperre gegen Clara: " + JSON.stringify(gesendet[0]));
pruefe(text().includes("Sperre gegen Clara"), "Nach der Abgabe steht die Wahl nicht als Satz da: " + text());

// --- Doppelt und „Keinen Joker": ohne Ziel ---
const zwei = [];
modul = neu(async (p) => { zwei.push(p); });
modul.setLocked(false);
joker()[0].click();
await ruhe();
pruefe(zwei.length === 1 && zwei[0].jokerId === "d-1" && zwei[0].target === null, "Doppelt ging nicht ohne Ziel raus: " + JSON.stringify(zwei));

const drei = [];
modul = neu(async (p) => { drei.push(p); });
modul.setLocked(false);
keiner().click();
await ruhe();
pruefe(drei.length === 1 && drei[0].jokerId === null && drei[0].target === null, "„Keinen Joker“ ging anders raus: " + JSON.stringify(drei));

// --- Ein Fehler beim Senden: die Tasten gehen wieder auf ---
const vier = [];
let einmalFehler = true;
modul = neu(async (p) => { vier.push(p); if (einmalFehler) { einmalFehler = false; throw new Error("weg"); } });
modul.setLocked(false);
joker()[0].click();
await ruhe();
joker()[0].click();
await ruhe();
pruefe(vier.length === 2, "Nach einem Sendefehler geht kein zweiter Versuch: " + vier.length);

// --- Abgegeben bzw. wiederverbunden: die eigene Wahl als Satz, mit Namen aus Joker und Ziel ---
modul.zeigeErgebnis({ ...info, ownJokerId: "s-1", ownTarget: "b-2" });
pruefe(text() === "Dein Joker: Sperre gegen Bert", "Die eigene Wahl kommt nicht als Satz zurück: " + text());
pruefe(joker().length === 0, "Nach der Abgabe stehen noch Tasten.");
pruefe(eigeneWahlText({ ...info, ownJokerId: null }) === "Kein Joker eingesetzt.", "Ohne Joker fehlt der Satz.");

// --- Nach dem Schliessen: alle Joker, und der Gesperrte erfährt es ---
modul.zeigeEnde({ ...info, results: [{ player: "Anna", joker: "Sperre", target: "Bert" }, { player: "Clara", joker: "Doppelt", target: null }], blockedBy: "Anna" });
const zeilen = [...wurzel.querySelectorAll(".joker-ergebnis")].map(z => z.textContent);
pruefe(zeilen.length === 2 && zeilen[0] === "Anna: Sperre gegen Bert" && zeilen[1] === "Clara: Doppelt", "Die Joker stehen nicht da: " + zeilen.join(" | "));
pruefe((wurzel.querySelector(".joker-gesperrt")?.textContent ?? "").includes("Joker von Anna"), "Der Gesperrte erfährt nicht, wer ihn gesperrt hat.");

modul.zeigeEnde({ results: [], blockedBy: null });
pruefe(text() === "Niemand hat einen Joker eingesetzt.", "Ohne Joker fehlt der Satz am Ende: " + text());
pruefe(wurzel.querySelector(".joker-gesperrt") == null, "Ohne Sperre steht trotzdem eine da.");

// --- Der Rahmen: prePhase waehlt das Modul und geht mit jeder Abgabe mit ---
const uhr = baueUhr();
const aktionen = [];
const rahmen = new MinigameLayout({ submitAction: async (a) => { aktionen.push(a); } }, { timer: uhr.timer, now: uhr.now });
const laufend = {
    resetCount: 1, currentLayoutLocked: false, allLocked: false,
    layoutInfo: {
        kind: 0, specialRule: 0, prePhase: PrePhases.Joker, title: "Joker-Runde", rulesText: "Wer setzt einen Joker?", phase: 1,
        currentRound: 1, totalRounds: 1, durchgang: 41, countdownSeconds: 0, aborted: false, ownSubmitted: false, kindInfo: info
    }
};

wurzel.innerHTML = "";
rahmen.render(wurzel, laufend);
rahmen.setLocked(false);
pruefe(wurzel.querySelector(".minispiel-runde").textContent === "Vor der Frage", "Die Joker-Runde zählt als Minispiel-Runde.");
pruefe(keiner() != null, "Der Rahmen zeigt für prePhase nicht das Joker-Modul.");
keiner().click();
await ruhe();
pruefe(aktionen.length === 1 && aktionen[0].prePhase === PrePhases.Joker && aktionen[0].durchgang === 41 && aktionen[0].kind === 0,
    "Die Abgabe trägt prePhase oder Durchgang nicht: " + JSON.stringify(aktionen));

rahmen.update({ ...laufend, currentLayoutLocked: true, layoutInfo: { ...laufend.layoutInfo, phase: 3, kindInfo: { results: [] } } });
pruefe(wurzel.querySelector(".minispiel-status").textContent.startsWith("Joker-Runde zu"), "Am Ende steht nicht, dass die Joker-Runde zu ist.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("joker: alle Zusicherungen gruen.");
