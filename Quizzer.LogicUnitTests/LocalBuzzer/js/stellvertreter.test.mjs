// Minispiel Stellvertreter-Duell (M37 T10) am Telefon: wer fuer sein Team waehlt, sieht die Mitglieder und das Los und
// schickt genau eine Wahl - beim Los ohne Zahl. Danach spielt nur der Stellvertreter, die anderen schauen zu. Namen nur als
// Text, die Teamfarbe nur als Klasse der Palette, der fremde Zug nur, wenn der Server ihn schickt.

import { baueDom } from "./dom-ersatz.mjs";
import { baueUhr } from "./uhr-ersatz.mjs";

const { wurzel } = baueDom();

if (!globalThis.navigator.vibrate) {
    Object.defineProperty(globalThis.navigator, "vibrate", { value: () => {} });
}

const { StellvertreterModul, teamText, siegerText } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/stellvertreter.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const uhr = baueUhr();
const modul = new StellvertreterModul({ timer: uhr.timer, now: uhr.now });
modul.mount(wurzel);
modul.setLocked(false);

const text = () => wurzel.querySelector(".stellvertreter-text").textContent;
const wahlTasten = () => wurzel.querySelectorAll(".stellvertreter-taste");
const zeilen = () => wurzel.querySelectorAll(".stellvertreter-team");
const zuege = () => wurzel.querySelectorAll(".duell-zug");

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };

const rot = { name: "<b>Rot</b>", symbol: "✚", colorIndex: 5, proxyName: null, byLot: false, isOwn: true, isWinner: false };
const blau = { name: "Blau", symbol: "★", colorIndex: 4, proxyName: "Carl", byLot: true, isOwn: false, isWinner: false };
const wahl = {
    choosesProxy: true,
    options: [{ id: "a-1", name: "Anna" }, { id: "b-2", name: "<i>Bert</i>" }],
    teams: [rot, blau]
};

// --- Die Wahl: die Mitglieder und das Los, genau ein Tipp geht raus ---
modul.starte(wahl, senden);
pruefe(wahlTasten().length === 3, "Statt zwei Mitgliedern und dem Los stehen " + wahlTasten().length + " Tasten da.");
pruefe(wahlTasten()[1].textContent === "<i>Bert</i>", "Der Name steht nicht wörtlich als Text da: " + wahlTasten()[1].textContent);
pruefe(wahlTasten()[2].classList.contains("stellvertreter-los") && wahlTasten()[2].textContent.includes("Los"), "Das Los fehlt.");
pruefe(zuege().length === 0, "Während der Wahl stehen schon Züge da.");

wahlTasten()[1].click();
wahlTasten()[0].click();
wahlTasten()[2].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].chosen === "b-2" && gesendet[0].lot === false,
    "Nicht genau eine Wahl (Bert) gesendet: " + JSON.stringify(gesendet));
pruefe(text().includes("<i>Bert</i>"), "Die eigene Wahl steht nicht als Text da: " + text());

// --- Das Los: nur „lot“, keine Zahl, kein Name ---
gesendet = [];
modul.starte(wahl, senden);
wahlTasten()[2].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].lot === true && gesendet[0].chosen === null,
    "Das Los geht nicht als Los raus: " + JSON.stringify(gesendet));
pruefe(Object.keys(gesendet[0] ?? {}).every(k => k === "lot" || k === "chosen"), "Das Los schickt mehr als „lot“ und „chosen“: " + JSON.stringify(gesendet));

// --- Gesperrt: keine Wahl ---
gesendet = [];
modul.starte(wahl, senden);
modul.setLocked(true);
wahlTasten()[0].click();
await ruhe();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gewählt.");
modul.setLocked(false);

// --- Die Teams: Name als Text, Farbe nur als Klasse, das eigene benannt ---
pruefe(zeilen().length === 2, "Nicht beide Teams stehen in der Liste.");
pruefe(zeilen()[0].textContent === "✚ <b>Rot</b>: wählt … · euer Team", "Die eigene Zeile stimmt nicht: " + zeilen()[0].textContent);
pruefe(zeilen()[1].textContent === "★ Blau: Carl (gelost)", "Die fremde Zeile stimmt nicht: " + zeilen()[1].textContent);
pruefe(zeilen()[0].classList.contains("team-farbe-5") && !zeilen()[0].classList.contains("team-farbe-4"), "Die Teamfarbe stimmt nicht.");
pruefe(zeilen()[1].classList.contains("team-farbe-4"), "Blau trägt nicht seine Farbe.");

modul.aktualisiere({ teams: [rot, { ...blau, colorIndex: 99 }, { ...blau, name: "Gelb", colorIndex: -1 }] });
pruefe(zeilen().length === 3, "Ein neues Team erscheint ohne neue Runde nicht.");
pruefe(![...Array(100).keys()].some(i => zeilen()[1].classList.contains(`team-farbe-${i}`)), "Eine Farbe außerhalb der Palette wurde gesetzt.");
pruefe(wahlTasten().length === 3, "Das Auffrischen der Liste hat die Wahl weggeräumt.");

// --- Zuschauer: weder Taste noch Zug ---
modul.starte({ choosesProxy: false, isProxy: false, discipline: 1, teams: [{ ...rot, proxyName: "Bert" }, blau] }, senden);
pruefe(wahlTasten().length === 0 && zuege().length === 0 && wurzel.querySelector(".reaktion-taste") == null,
    "Ein Zuschauer bekommt Tasten.");
pruefe(text().includes("Beamer"), "Ein Zuschauer bekommt keinen Hinweis: " + text());

// --- Schere-Stein-Papier für den Stellvertreter: ein Zug, genau einmal ---
gesendet = [];
const runde = { isProxy: true, discipline: 0, teams: [{ ...rot, proxyName: "Bert" }, blau] };
modul.starte(runde, senden);
pruefe(zuege().length === 3, "Die drei Züge fehlen.");
zuege()[0].click();
zuege()[2].click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].move === 1, "Nicht genau ein Zug (Schere = 1) gesendet: " + JSON.stringify(gesendet));

modul.zeigeErgebnis({ ...runde, ownMove: 1, otherMove: null, ownWins: 0, otherWins: 0 });
pruefe(text().includes("warte auf die Auflösung") && !text().includes("Gegner:"), "Ohne fremden Zug zeigt das Telefon einen: " + text());
modul.zeigeErgebnis({ ...runde, ownMove: 1, otherMove: 3, ownWins: 1, otherWins: 0 });
pruefe(text().includes("Gegner: Papier") && text().includes("1:0"), "Nach der Runde fehlen Gegnerzug oder Stand: " + text());

// --- Reaktion für den Stellvertreter: das Modul aus B3 misst ---
gesendet = [];
modul.starte({ isProxy: true, discipline: 1, teams: [rot, blau], reaction: { waitMs: 2000 } }, senden);
pruefe(wurzel.querySelector(".reaktion-taste") != null, "Die Reaktion des Stellvertreters hat keine Taste.");
uhr.vor(2000);
uhr.vor(180);
(wurzel.querySelector(".reaktion-taste").hoerer.pointerdown ?? []).forEach(fn => fn({ button: 0 }));
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].milliseconds === 180, "Die Reaktion misst nicht am Telefon: " + JSON.stringify(gesendet));

// --- Ende ---
modul.zeigeEnde({ isWinner: true, teams: [{ ...rot, isWinner: true }, blau] });
pruefe(text().includes("Euer Team gewinnt"), "Das Siegerteam erfährt es nicht: " + text());
pruefe(zeilen()[0].textContent.includes("★ Sieger"), "Das Siegerteam ist nicht mit Text markiert: " + zeilen()[0].textContent);

modul.zeigeEnde({ isDraw: true, teams: [rot, blau] });
pruefe(text().includes("Unentschieden"), "Ein Unentschieden wird nicht gesagt: " + text());

modul.zeigeEnde({ teams: [rot, { ...blau, isWinner: true }] });
pruefe(text() === "Gewonnen: ★ Blau.", "Der Verlierer erfährt nicht, wer gewonnen hat: " + text());

pruefe(teamText({ symbol: "▲", name: "Gelb" }) === "▲ Gelb: wählt …", "Ein Team ohne Stellvertreter wählt nicht: " + teamText({ symbol: "▲", name: "Gelb" }));
pruefe(siegerText([]) === "Das Duell ist vorbei.", "Ohne Sieger fehlt der Satz.");

modul.dispose();
pruefe(uhr.offen() === 0, "Nach dem Aufräumen laufen noch Uhren.");

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("stellvertreter: alle Zusicherungen gruen.");
