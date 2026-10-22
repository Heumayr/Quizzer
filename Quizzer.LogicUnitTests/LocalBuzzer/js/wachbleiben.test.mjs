// Bildschirm wach halten: im http-WLAN ueber das Video, unter https ueber Wake Lock - und nach
// dem Zurueckkommen aus dem Hintergrund neu.

const { wachHalten } = await import("../../../LocalBuzzer.Service/wwwroot/JS/wachbleiben.js");

let fehler = 0;
function pruefe(bedingung, satz) {
    if (bedingung) return;
    console.error("ROT: " + satz);
    fehler++;
}

function baueDoc() {
    const hoerer = {};
    const doc = {
        visibilityState: "visible",
        body: { kinder: [], appendChild(k) { this.kinder.push(k); return k; } },
        createElement(tag) {
            return {
                tag, attribute: {}, kinder: [], gespielt: 0, muted: false, loop: false,
                setAttribute(n, w) { this.attribute[n] = w; },
                appendChild(k) { this.kinder.push(k); return k; },
                async play() { this.gespielt++; if (doc.playWirft) throw new Error("NotAllowedError"); },
            };
        },
        addEventListener(n, fn) { (hoerer[n] ??= []).push(fn); },
        removeEventListener(n, fn) { hoerer[n] = (hoerer[n] ?? []).filter(f => f !== fn); },
        feuere(n) { (hoerer[n] ?? []).slice().forEach(fn => fn({})); },
        hoerer,
    };
    return doc;
}

const warte = () => new Promise(r => setTimeout(r, 0));

// --- http: kein sicherer Kontext, also Video ---
{
    const doc = baueDoc();
    const w = wachHalten({ doc, nav: { wakeLock: { request: async () => { throw new Error("darf nicht"); } } }, win: { isSecureContext: false } });

    pruefe(w.modus === "aus", "vor dem ersten Tipp laeuft schon etwas");
    pruefe(doc.body.kinder.length === 0, "das Video entsteht vor dem ersten Tipp");

    doc.feuere("pointerdown");
    await warte();

    const video = doc.body.kinder[0];
    pruefe(w.modus === "video", "nach dem Tipp laeuft kein Video, modus=" + w.modus);
    pruefe(video && video.muted && video.loop, "das Video ist nicht stumm oder laeuft nicht in Schleife");
    pruefe(video && "playsinline" in video.attribute, "ohne playsinline geht iOS in den Vollbildspieler");
    pruefe(video && video.kinder.some(k => k.attribute.src === "/media/wach.mp4"), "die mp4-Quelle fehlt (iOS)");

    // Ein zweiter Tipp baut kein zweites Video.
    doc.feuere("click");
    doc.feuere("pointerdown");
    await warte();
    pruefe(doc.body.kinder.length === 1, "es entstehen mehrere Videos");

    // Zurueck aus dem Hintergrund: weiter abspielen.
    const vorher = video.gespielt;
    doc.feuere("visibilitychange");
    await warte();
    pruefe(video.gespielt === vorher + 1, "nach dem Zurueckkommen wird nicht neu abgespielt");
}

// --- https: Wake Lock, kein Video ---
{
    const doc = baueDoc();
    let angefordert = 0;
    const lock = { released: false };
    const w = wachHalten({ doc, nav: { wakeLock: { request: async (art) => { angefordert++; pruefe(art === "screen", "falsche Art: " + art); return lock; } } }, win: { isSecureContext: true } });

    doc.feuere("pointerdown");
    await warte();
    pruefe(w.modus === "wakelock", "unter https kein Wake Lock, modus=" + w.modus);
    pruefe(doc.body.kinder.length === 0, "unter https entsteht trotzdem ein Video");

    lock.released = true; // geht beim Verstecken der Seite weg
    doc.feuere("visibilitychange");
    await warte();
    pruefe(angefordert === 2, "nach dem Zurueckkommen nicht neu angefordert");
}

// --- Wake Lock abgelehnt: dann doch das Video ---
{
    const doc = baueDoc();
    const w = wachHalten({ doc, nav: { wakeLock: { request: async () => { throw new Error("NotAllowedError"); } } }, win: { isSecureContext: true } });
    await w.an();
    pruefe(w.modus === "video", "nach abgelehntem Wake Lock kein Video, modus=" + w.modus);
}

// --- Abspielen verweigert: kein Absturz, ehrlich "aus" ---
{
    const doc = baueDoc();
    doc.playWirft = true;
    const w = wachHalten({ doc, nav: {}, win: { isSecureContext: false } });
    await w.an();
    pruefe(w.modus === "aus", "verweigertes Abspielen meldet trotzdem wach");
}

if (fehler > 0) process.exit(1);
console.log("gruen: wachbleiben");
