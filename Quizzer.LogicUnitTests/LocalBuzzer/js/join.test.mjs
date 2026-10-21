// Selbst anmelden am Telefon (M10): Name waehlen oder Gast, dann wartet das Telefon auf die
// Bestaetigung des Spielleiters. Haengt hier etwas, steht ein Gast mit leerem Telefon am Tisch.

import { baueDom } from "./dom-ersatz.mjs";

baueDom();

const { JoinFlow, PENDING_KEY, Texts } = await import(
    "../../../LocalBuzzer.Service/wwwroot/JS/join.js");

let fehler = 0;

function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

function antwort(status, body) {
    return { ok: status >= 200 && status < 300, status, json: async () => body };
}

function speicher() {
    const m = new Map();
    return { getItem: k => m.get(k) ?? null, setItem: (k, v) => m.set(k, String(v)), removeItem: k => m.delete(k) };
}

// Ein Server zum Mitschreiben: antwortet der Reihe nach mit den Statusmeldungen.
function server({ kandidaten = [], anfrage = antwort(200, { requestId: "r-1" }), status = [] } = {}) {
    const aufrufe = [];
    const folge = [...status];
    const fetchImpl = async (url, opt) => {
        aufrufe.push({ url, opt });
        if (url === "/join/candidates") return antwort(200, kandidaten);
        if (url === "/join/request") return anfrage;
        if (url.startsWith("/join/status/")) return folge.shift() ?? antwort(200, { status: "pending" });
        return antwort(404, null);
    };
    return { fetchImpl, aufrufe };
}

function host() { return document.createElement("div"); }

const sofort = async () => { };

// 1. Name waehlen, Spielleiter nimmt an -> das Telefon geht mit der Kennung zum Buzzer.
{
    const s = server({
        kandidaten: [{ playerId: "p-1", name: "Anna" }, { playerId: "p-2", name: "Bert" }],
        status: [antwort(200, { status: "pending" }), antwort(200, { status: "accepted", playerId: "p-2" })]
    });
    let ziel = null;
    const h = host();
    const flow = new JoinFlow({ host: h, fetchImpl: s.fetchImpl, navigate: u => { ziel = u; }, storage: speicher(), wait: sofort });

    await flow.start();
    const knoepfe = h.querySelectorAll(".join-candidate");
    pruefe(knoepfe.length === 2, "zwei Namen als Knoepfe, gefunden: " + knoepfe.length);
    pruefe(knoepfe[1].textContent === "Bert", "der zweite Knopf heisst Bert");

    knoepfe[1].click();
    await new Promise(r => setTimeout(r, 10));

    const post = s.aufrufe.find(a => a.url === "/join/request");
    pruefe(post && JSON.parse(post.opt.body).playerId === "p-2", "die Anfrage nennt Berts Kennung");
    pruefe(post && !("password" in JSON.parse(post.opt.body)), "kein Kennwort in der Anfrage");
    pruefe(ziel === "/?id=p-2", "nach dem Annehmen geht es zu /?id=p-2, war: " + ziel);
}

// 2. Gast: der eingetippte Name geht mit, keine Kennung.
{
    const s = server({ status: [antwort(200, { status: "accepted", playerId: "g-9" })] });
    let ziel = null;
    const h = host();
    const flow = new JoinFlow({ host: h, fetchImpl: s.fetchImpl, navigate: u => { ziel = u; }, storage: speicher(), wait: sofort });

    await flow.start();
    h.querySelector(".join-guest-name").value = "  Carla  ";
    h.querySelector(".join-guest").click();
    await new Promise(r => setTimeout(r, 10));

    const body = JSON.parse(s.aufrufe.find(a => a.url === "/join/request").opt.body);
    pruefe(body.guestName === "Carla" && body.playerId === null, "Gast Carla ohne Kennung, war: " + JSON.stringify(body));
    pruefe(ziel === "/?id=g-9", "der Gast verbindet unter der neuen Kennung");
}

// 3. Abgelehnt -> zurueck zur Auswahl mit Hinweis, die gespeicherte Anfrage ist weg.
{
    const store = speicher();
    const s = server({ kandidaten: [{ playerId: "p-1", name: "Anna" }], status: [antwort(200, { status: "declined" })] });
    const h = host();
    const flow = new JoinFlow({ host: h, fetchImpl: s.fetchImpl, navigate: () => { }, storage: store, wait: sofort });

    await flow.start();
    h.querySelector(".join-candidate").click();
    await new Promise(r => setTimeout(r, 10));

    pruefe(h.querySelector(".join-message")?.textContent === Texts.declined, "der Hinweis sagt: abgelehnt");
    pruefe(store.getItem(PENDING_KEY) === null, "keine Anfrage bleibt gespeichert");
}

// 4. Zu viele offene Anfragen -> verstaendlicher Hinweis statt stiller Leere.
{
    const s = server({ kandidaten: [{ playerId: "p-1", name: "Anna" }], anfrage: antwort(429, null) });
    const h = host();
    const flow = new JoinFlow({ host: h, fetchImpl: s.fetchImpl, navigate: () => { }, storage: speicher(), wait: sofort });

    await flow.start();
    h.querySelector(".join-candidate").click();
    await new Promise(r => setTimeout(r, 10));

    pruefe(h.querySelector(".join-message")?.textContent === Texts.tooMany, "Hinweis bei 429");
}

// 5. Neu geladen waehrend des Wartens -> es wird weiter gewartet, nicht neu gefragt.
{
    const store = speicher();
    store.setItem(PENDING_KEY, "r-7");
    const s = server({ status: [antwort(200, { status: "accepted", playerId: "p-7" })] });
    let ziel = null;
    const flow = new JoinFlow({ host: host(), fetchImpl: s.fetchImpl, navigate: u => { ziel = u; }, storage: store, wait: sofort });

    await flow.start();
    pruefe(!s.aufrufe.some(a => a.url === "/join/request"), "keine zweite Anfrage nach dem Neuladen");
    pruefe(s.aufrufe.some(a => a.url === "/join/status/r-7"), "es wird die gespeicherte Anfrage abgefragt");
    pruefe(ziel === "/?id=p-7", "und danach verbunden");
}

if (fehler > 0) {
    console.error(`${fehler} Pruefung(en) rot.`);
    process.exit(1);
}

console.log("join.test.mjs: alles gruen");
