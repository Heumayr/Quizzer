// Die Verdrahtung in index.js (M37 P8, P6-8): der Stand des Servers geht als Kontext an das Layout - die Team-Abstimmung
// (team.vote) als teamVote. Die anderen Node-Tests bauen den Kontext selbst und saehen eine fehlende Zeile hier nicht.
//
// index.js laedt mit Seiteneffekten (start(): Verbindung aufbauen) - ersetzt werden deshalb nur, was es von aussen braucht:
// SignalR, die Adresse mit Kennung und was wachbleiben.js am Dokument anfasst. Der Hub ist ein Ersatz, der die Ereignisse
// "Assigned" und "StateChanged" von Hand ausloest.

import { baueDom } from "./dom-ersatz.mjs";

const { dokument } = baueDom();
dokument.addEventListener = () => {};
dokument.removeEventListener = () => {};
dokument.body = dokument.createElement("body");
globalThis.location = { search: "?key=pruefschluessel-0123456789" };

const ereignis = {};

globalThis.signalR = {
    HubConnectionBuilder: class {
        withUrl() { return this; }
        withAutomaticReconnect() { return this; }
        build() {
            return {
                on: (name, fn) => { ereignis[name] = fn; },
                onreconnecting() {}, onreconnected() {}, onclose() {},
                start: async () => {},
                invoke: async () => {}
            };
        }
    }
};

const { Layouts } = await import("../../../LocalBuzzer.Service/wwwroot/JS/constants.js");
await import("../../../LocalBuzzer.Service/wwwroot/JS/index.js");
await new Promise(r => setTimeout(r, 0));

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;

    console.error("ROT: " + satz);
    fehler++;
}

const stand = (resetCount, vote) => ({
    playerName: "Anna",
    round: 1,
    resetCount,
    layout: Layouts.KeySelect,
    layoutInfo: {
        questionId: "77777777-7777-7777-7777-777777777777",
        keysAndDesignations: { A: "Rom", B: "Paris" },
        showDesignations: true,
        maxAllowedSelections: 1
    },
    currentLayoutLocked: false,
    allLocked: false,
    team: { name: "Rot", symbol: "▲", colorIndex: 0, speaker: "", vote }
});

const laeuft = own => ({ own, voted: own.length, of: 2, closed: false, result: "" });

const host = dokument.getElementById("layout_host");
const knopf = () => host.querySelector(".commit-btn");
const gewaehlt = () => host.querySelectorAll(".key-btn").filter(b => b.classList.contains("selected")).map(b => b.dataset.key).join("");

pruefe(typeof ereignis.Assigned === "function" && typeof ereignis.StateChanged === "function",
    "index.js meldet sich nicht fuer Assigned und StateChanged an - der Ersatz misst nichts.");

// --- Wiederkommen mitten in der Abstimmung: der erste Stand baut das Layout mit der gezaehlten Stimme ---
ereignis.Assigned(stand(1, laeuft(["A"])));

pruefe(knopf()?.textContent === "Abgegeben ✓" && gewaehlt() === "A",
    "Beim Aufbau kommt die gezaehlte Stimme nicht im Layout an: " + gewaehlt() + " / " + knopf()?.textContent);

// --- Neue Runde ohne Stimme, dann zaehlt der Server eine: der folgende Stand erreicht das Layout ueber update ---
ereignis.StateChanged(stand(2, laeuft([])));
pruefe(knopf()?.textContent === "Auswahl bestätigen", "Die neue Runde ohne Stimme zeigt: " + knopf()?.textContent);

ereignis.StateChanged(stand(2, laeuft(["B"])));

pruefe(knopf()?.textContent === "Abgegeben ✓" && gewaehlt() === "B",
    "Ein Stand mit gezaehlter Stimme kommt nicht im Layout an: " + gewaehlt() + " / " + knopf()?.textContent);

if (fehler > 0) {
    console.error(`${fehler} Zusicherung(en) rot.`);
    process.exit(1);
}

console.log("indexVerdrahtung: alle Zusicherungen gruen.");
