// Spielerwahl (M35 C2) am Telefon: je Mitspieler eine Taste mit seinem Namen, ein Tipp sendet
// genau einmal { chosen: id }, gesperrt sendet nichts, und die Wahl steht als Text da.

import { baueDom } from "./dom-ersatz.mjs";

const { wurzel } = baueDom();

const { Spielerwahl } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/minispiele/spielerwahl.js");

let fehler = 0;

// Auch was erst nach einem await gesendet wuerde, soll vor der Zusicherung da sein.
const ruhe = () => new Promise(r => setImmediate(r));

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

const tasten = () => wurzel.querySelectorAll(".spielerwahl-taste");
const frage = () => wurzel.querySelector(".spielerwahl-frage").textContent;

let gesendet = [];
const senden = async (payload) => { gesendet.push(payload); };
const optionen = [{ id: "b-1", name: "Bert" }, { id: "c-2", name: "Carla" }];

const wahl = new Spielerwahl();
wahl.zeige(wurzel, "Wähle deinen Gegner:", optionen, senden);

pruefe(frage() === "Wähle deinen Gegner:", "Die Frage steht nicht da: " + frage());
pruefe(tasten().length === 2 && tasten()[0].textContent === "Bert" && tasten()[1].textContent === "Carla",
    "Nicht je Mitspieler eine Taste mit seinem Namen: " + tasten().map(t => t.textContent).join(", "));

// --- Gesperrt: nichts geht raus ---
tasten()[1].click();
pruefe(gesendet.length === 0, "Ein gesperrtes Telefon hat gewählt.");

// --- Ein Tipp: genau eine Wahl, die Id der Taste ---
wahl.setLocked(false);
tasten()[1].click();
tasten()[0].click();

await ruhe();
pruefe(gesendet.length === 1 && gesendet[0].chosen === "c-2",
    "Gesendet wurde nicht genau einmal die gewählte Id: " + JSON.stringify(gesendet));
pruefe(frage().includes("Carla"), "Die Wahl steht nicht als Text da (nie nur Farbe): " + frage());
pruefe(tasten().every(t => t.disabled), "Nach der Wahl lassen sich die Tasten noch drücken.");
pruefe(tasten()[1].classList.contains("spielerwahl-aktiv"), "Die gewählte Taste ist nicht markiert.");

// --- Niemand da ---
wurzel.innerHTML = "";
wahl.zeige(wurzel, "Wähle deinen Gegner:", [], senden);
pruefe(tasten().length === 0 && frage().includes("Spielleiter"), "Ohne Mitspieler fehlt der Hinweis: " + frage());

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("spielerwahl: alle Zusicherungen gruen.");
