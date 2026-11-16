// Minispiel Raeuber und Waechter (M35 C2) am Telefon: Schild oder ein Opfer je Mitspieler, genau
// eine Wahl geht raus, das Telefon zeigt nur die eigene - die Aufloesung kommt vom Server.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { RaeuberModul } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/raeuber.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const modul = new RaeuberModul();
modul.mount(wurzel);

const text = () => wurzel.querySelector(".raeuber-text").textContent;
const schild = () => wurzel.querySelector(".raeuber-schild");
const opfer = () => wurzel.querySelectorAll(".raeuber-opfer");
const zeilen = () => wurzel.querySelectorAll(".raeuber-ergebnis").map(z => z.textContent);

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };
const info = { options: [{ id: "b-1", name: "Bert" }, { id: "c-2", name: "Carla" }] };

// --- Gesperrt: nichts ---
modul.starte(info, senden);
schild().click();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gewählt.");

// --- Ein Opfer je Mitspieler, ein Raub geht genau einmal raus ---
modul.setLocked(false);
modul.starte(info, senden);
pruefe(schild() != null && opfer().length === 2 && opfer()[0].textContent.includes("Bert"),
    "Schild und je Mitspieler ein Opfer fehlen: " + opfer().map(o => o.textContent).join(", "));

opfer()[1].click();
schild().click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].victim === "c-2" && gesendet[0].shield === false,
    "Nicht genau ein Raub bei Carla gesendet: " + JSON.stringify(gesendet));
pruefe(text().includes("Carla"), "Die Wahl steht nicht als Text da: " + text());

// --- Das Schild ---
gesendet = [];
modul.starte(info, senden);
schild().click();
await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].shield === true && gesendet[0].victim === null,
    "Das Schild geht nicht raus: " + JSON.stringify(gesendet));

// --- Abgegeben: nur die eigene Wahl ---
modul.zeigeErgebnis({ ownShield: false, ownVictimName: "Bert" });
pruefe(text().includes("Bert") && text().includes("Auflösung"), "Die eigene Wahl steht nicht da: " + text());

modul.zeigeErgebnis({ ownShield: true, ownVictimName: null });
pruefe(text().includes("Schild"), "Das eigene Schild steht nicht da: " + text());

// --- Aufloesung: je Satz eine Zeile ---
modul.zeigeEnde({ ownResults: ["Du hast Bert bestohlen – kein Schild.", "Carla hat dich bestohlen."] });
pruefe(zeilen().length === 2 && zeilen()[1].includes("Carla"), "Die Auflösung fehlt: " + zeilen().join(" | "));

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("raeuber: alle Zusicherungen gruen.");
