import { MinigameKinds, MinigamePhases, SpecialRules } from "../constants.js";
import { ReaktionModul } from "./reaktion.js";
import { TippsprintModul } from "./tippsprint.js";
import { StoppuhrModul } from "./stoppuhr.js";
import { DuellModul } from "./duell.js";
import { RaeuberModul } from "./raeuber.js";
import { TeilenModul } from "./teilen.js";
import { ZockerModul } from "./zocker.js";
import { GluecksradModul } from "./gluecksrad.js";
import { KopfrechnenModul } from "./kopfrechnen.js";
import { MemoryModul } from "./memory.js";
import { BlitzrundeModul } from "./blitzrunde.js";
import { BuchstabensalatModul } from "./buchstabensalat.js";
import { EinsatzModul } from "../sonderregeln/einsatz.js";
import { RisikoModul } from "../sonderregeln/risiko.js";
import { AuktionModul } from "../sonderregeln/auktion.js";
import { WahlModul } from "../sonderregeln/wahl.js";

// Der Rahmen der Minispiele am Telefon (M35 B3): Ueberschrift, Runde n von m, Warten,
// Countdown und Ergebnis. Was eine Art ausmacht, steckt in ihrem Modul unter minispiele/.
// Abgegeben wird ueber den einen Hub-Eingang SpielAktion: { kind, specialRule, durchgang, payload }.
// Die Vorphase einer Sonderregel (M35 C3) laeuft im selben Rahmen, ihr Modul liegt unter sonderregeln/.

const MODULE = new Map([
    [MinigameKinds.Reaction, (optionen) => new ReaktionModul(optionen)],
    [MinigameKinds.TapSprint, (optionen) => new TippsprintModul(optionen)],
    [MinigameKinds.Stopwatch, (optionen) => new StoppuhrModul(optionen)],
    [MinigameKinds.Duel, (optionen) => new DuellModul(optionen)],
    [MinigameKinds.RobberAndGuard, () => new RaeuberModul()],
    [MinigameKinds.ShareOrTake, () => new TeilenModul()],
    [MinigameKinds.Crash, () => new ZockerModul()],
    [MinigameKinds.WheelOfFortune, () => new GluecksradModul()],
    [MinigameKinds.MentalArithmetic, (optionen) => new KopfrechnenModul(optionen)],
    [MinigameKinds.Memory, (optionen) => new MemoryModul(optionen)],
    [MinigameKinds.LightningRound, (optionen) => new BlitzrundeModul(optionen)],
    [MinigameKinds.WordScramble, (optionen) => new BuchstabensalatModul(optionen)]
]);

const SONDERREGELN = new Map([
    [SpecialRules.Stake, () => new EinsatzModul()],
    [SpecialRules.Risk, () => new RisikoModul()],
    [SpecialRules.Auction, () => new AuktionModul()],
    [SpecialRules.Bet, () => new WahlModul()],
    [SpecialRules.Expert, () => new WahlModul()],
    [SpecialRules.Alliance, () => new WahlModul()]
]);

export function rundenText(serverInfo) {
    const gesamt = serverInfo?.totalRounds ?? 0;
    const runde = serverInfo?.currentRound ?? 0;

    if (gesamt <= 0) return "";
    return runde > 0 ? `Runde ${runde} von ${gesamt}` : `${gesamt} ${gesamt === 1 ? "Runde" : "Runden"}`;
}

export class MinigameLayout {
    constructor(actions, optionen = {}) {
        this.actions = actions;
        this.timer = optionen.timer ?? { setTimeout: (f, ms) => setTimeout(f, ms), clearTimeout: (h) => clearTimeout(h) };
        this.now = optionen.now;
        this.module = optionen.module ?? MODULE;
        this.sonderregeln = optionen.sonderregeln ?? SONDERREGELN;
        this.root = null;
        this.titel = null;
        this.runde = null;
        this.status = null;
        this.feld = null;
        this.modul = null;
        this.modulArt = null;
        this.signatur = "";
        this.countdownUhr = null;
        this.locked = true;
    }

    render(host, context) {
        const root = document.createElement("div");
        root.className = "layout layout-minispiel";

        const titel = document.createElement("h2");
        titel.className = "minispiel-titel";

        const runde = document.createElement("div");
        runde.className = "minispiel-runde";

        const status = document.createElement("p");
        status.className = "minispiel-status";
        status.setAttribute("aria-live", "polite");

        const feld = document.createElement("div");
        feld.className = "minispiel-feld";

        root.append(titel, runde, status, feld);
        host.appendChild(root);

        // Dasselbe Objekt rendert bei jedem Layoutwechsel neu - der alte Stand gilt nicht mehr.
        Object.assign(this, { root, titel, runde, status, feld, modul: null, modulArt: null, signatur: "" });
        this.update(context);
    }

    update(context) {
        const serverInfo = context?.layoutInfo ?? {};
        const artInfo = serverInfo.kindInfo ?? {};

        this.titel.textContent = serverInfo.title || "Minispiel";
        this.runde.textContent = serverInfo.specialRule ? "Vor der Frage" : rundenText(serverInfo);

        // Neu aufbauen nur, wenn sich Durchgang, Phase, eigene Abgabe oder Sperre aendern - sonst
        // startete ein Update mitten in der Wartezeit die Uhr neu.
        const gesperrt = !!(context?.currentLayoutLocked || context?.allLocked);
        const signatur = [serverInfo.durchgang, serverInfo.phase, context?.resetCount, serverInfo.ownSubmitted, gesperrt].join("|");
        if (signatur === this.signatur) {
            // Mitten in der Runde: neuer Stand nur fuer dieses Telefon (Kopfrechnen, Memory - M35 C5).
            this.modul?.aktualisiere?.(artInfo);
            return;
        }

        this.signatur = signatur;
        this.zeige(serverInfo, artInfo, gesperrt);
    }

    zeige(serverInfo, artInfo, gesperrt) {
        this.stoppeCountdown();
        const modul = this.modulFuer(serverInfo);

        switch (serverInfo.phase) {
            case MinigamePhases.Running:
                if (serverInfo.ownSubmitted) {
                    // Abgegeben, auch nach dem Wiederverbinden: sie kommt zurueck, kein zweiter Versuch.
                    this.status.textContent = "Abgegeben – warte auf die anderen.";
                    modul?.zeigeErgebnis(artInfo);
                } else if (gesperrt) {
                    // Sperre je Spieler bzw. Zuschauer der Art (M35 C2): diese Runde spielen andere.
                    this.status.textContent = serverInfo.watcherText || "Diese Runde spielen andere – du schaust zu.";
                    modul?.leeren();
                } else {
                    this.countdown(serverInfo, artInfo, modul);
                }
                break;

            case MinigamePhases.RoundOver:
                this.status.textContent = `Runde ${serverInfo.currentRound} vorbei – warte auf die nächste.`;
                modul?.zeigeErgebnis(artInfo);
                break;

            case MinigamePhases.Finished:
                this.status.textContent = serverInfo.aborted
                    ? "Der Spielleiter hat das Minispiel beendet."
                    : serverInfo.specialRule ? "Abgegeben ist abgegeben – gleich kommt die Frage." : "Minispiel vorbei.";
                modul?.zeigeEnde(artInfo);
                break;

            default:
                this.status.textContent = serverInfo.rulesText || "Warte auf den Spielleiter – gleich geht es los.";
                modul?.leeren();
        }
    }

    // Zaehlt herunter und uebergibt dann an das Modul - mit dem Durchgang, den das Telefon gesehen hat.
    countdown(serverInfo, artInfo, modul) {
        const kind = serverInfo.kind;
        const specialRule = serverInfo.specialRule ?? SpecialRules.None;
        const durchgang = serverInfo.durchgang;
        const senden = (payload) => this.actions.submitAction({ kind, specialRule, durchgang, payload });
        let rest = Math.max(0, serverInfo.countdownSeconds ?? 0);

        const tick = () => {
            if (rest <= 0) {
                this.countdownUhr = null;
                this.status.textContent = specialRule ? (serverInfo.rulesText || "Los!") : "Los!";
                modul?.starte(artInfo, senden);
                return;
            }

            this.status.textContent = `Gleich geht es los: ${rest}`;
            rest--;
            this.countdownUhr = this.timer.setTimeout(tick, 1000);
        };

        modul?.leeren();
        tick();
    }

    // Eine Sonderregel (M35 C3) hat Vorrang vor der Art - ihre Vorphase traegt keine.
    modulFuer(serverInfo) {
        const regel = serverInfo.specialRule ?? SpecialRules.None;
        const schluessel = regel ? `regel-${regel}` : serverInfo.kind;
        if (this.modulArt === schluessel && this.modul) return this.modul;

        this.modul?.dispose?.();
        this.feld.innerHTML = "";
        this.modul = null;
        this.modulArt = schluessel;

        const fabrik = regel ? this.sonderregeln.get(regel) : this.module.get(serverInfo.kind);
        if (!fabrik) {
            this.feld.innerHTML = `<div class="layout-empty">Dieses Minispiel kennt das Telefon noch nicht.</div>`;
            return null;
        }

        this.modul = fabrik({ timer: this.timer, now: this.now });
        this.modul.mount(this.feld);
        this.modul.setLocked?.(this.locked);
        return this.modul;
    }

    setLocked(locked) {
        this.locked = !!locked;
        this.modul?.setLocked?.(this.locked);
    }

    stoppeCountdown() {
        if (this.countdownUhr != null) this.timer.clearTimeout(this.countdownUhr);
        this.countdownUhr = null;
    }

    dispose() {
        this.stoppeCountdown();
        this.modul?.dispose?.();
        this.modul = null;
    }
}
