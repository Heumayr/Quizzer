import { Layouts } from "./constants.js";
import { dom, resolvePlayerId, showError, getErrMsg, toast, nextRetryDelay, setConnectionIndicator } from "./helpers.js";
import { LayoutManager } from "./layoutManager.js";
import { BuzzerLayout } from "./layouts/buzzerLayout.js";
import { KeySelectLayout } from "./layouts/keySelectLayout.js";
import { InputLayout } from "./layouts/inputLayout.js";

const WAITING_FOR_HOST = "Verbunden – warte auf den Spielleiter";

const state = {
    playerId: null,
    playerName: "—",
    round: 0,
    resetCount: 0,
    currentLayout: Layouts.None,
    layoutInfo: null,
    currentLayoutLocked: true,
    allLocked: false,
    winner: null,
    replaced: false,
    conn: null
};

const layoutManager = new LayoutManager(state);

function buildContext() {
    return {
        host: dom.host,
        playerId: state.playerId,
        playerName: state.playerName,
        round: state.round,
        resetCount: state.resetCount,
        currentLayout: state.currentLayout,
        layoutInfo: state.layoutInfo,
        currentLayoutLocked: state.currentLayoutLocked,
        allLocked: state.allLocked,
        winner: state.winner
    };
}

function buildStatusText() {
    if (state.winner) {
        return `Runde ${state.round}: Gewinner: ${state.winner}`;
    }

    switch (state.currentLayout) {
        case Layouts.Buzzer:
            return `Runde ${state.round}: bereit`;
        case Layouts.KeySelect:
            return `Runde ${state.round}: Auswahl treffen`;
        case Layouts.Input:
            return `Runde ${state.round}: Eingabe erwartet`;
        default:
            return state.round > 0
                ? `Runde ${state.round}: warte auf den Spielleiter`
                : WAITING_FOR_HOST;
    }
}

function applyServerState(serverState) {
    const previousLayout = state.currentLayout;

    state.playerName = serverState.playerName ?? state.playerName;
    state.round = serverState.round ?? state.round;
    state.resetCount = serverState.resetCount ?? state.resetCount;
    state.currentLayout = serverState.layout ?? Layouts.None;
    state.layoutInfo = serverState.layoutInfo ?? null;
    state.currentLayoutLocked = serverState.currentLayoutLocked ?? true;
    state.allLocked = serverState.allLocked ?? false;
    state.winner = serverState.winner ?? null;

    dom.name.textContent = state.playerName || "—";
    dom.status.textContent = buildStatusText();

    const context = buildContext();

    if (previousLayout !== state.currentLayout) {
        layoutManager.switchTo(state.currentLayout, context);
    } else {
        layoutManager.update(context);
    }
}

// Meldet einen fehlgeschlagenen Hub-Aufruf und wirft ihn weiter, damit das Layout seine Sperre zuruecknehmen kann.
async function invokeOrReport(method, payload) {
    try {
        if (payload === undefined) {
            await state.conn.invoke(method);
        } else {
            await state.conn.invoke(method, payload);
        }
    } catch (e) {
        const m = getErrMsg(e);
        showError(m);
        toast("error", m);
        throw e;
    }
}

function registerLayouts() {
    layoutManager.register(Layouts.Buzzer, new BuzzerLayout({
        buzz: () => invokeOrReport("Buzz")
    }));

    layoutManager.register(Layouts.KeySelect, new KeySelectLayout({
        submitSelection: (payload) => invokeOrReport("SelectionResults", payload)
    }));

    layoutManager.register(Layouts.Input, new InputLayout({
        submitInput: (payload) => invokeOrReport("SubmitInput", payload)
    }));
}

// Endzustand ohne Verbindung: Grund zeigen, Toast ohne Ablauf, grosser Knopf zum Neuladen.
function showDisconnected(reason) {
    setConnectionIndicator("disconnected");
    showError(reason);
    dom.status.textContent = "Nicht verbunden";
    toast("error", reason, { timeout: 0 });
    layoutManager.showNotice(dom.host, {
        text: reason,
        buttonLabel: "Neu verbinden",
        onButton: () => location.reload()
    });
}

// Eine neuere Verbindung desselben Spielers hat diese abgeloest: bewusst beenden, nicht neu verbinden.
async function handleReplaced(text) {
    state.replaced = true;
    const message = text || "Dieser Spieler ist inzwischen auf einem anderen Gerät angemeldet.";

    setConnectionIndicator("disconnected");
    showError("");
    dom.status.textContent = "Verbindung beendet";
    layoutManager.showNotice(dom.host, {
        text: message,
        buttonLabel: "Hier weiterspielen",
        onButton: () => location.reload()
    });

    await state.conn.stop();
}

function registerConnectionEvents(conn) {
    conn.on("Assigned", (serverState) => {
        showError("");
        applyServerState(serverState);
        toast("info", `${state.playerName} verbunden`);
    });

    conn.on("StateChanged", (serverState) => {
        showError("");
        applyServerState(serverState);

        if (state.allLocked || state.currentLayoutLocked) {
            layoutManager.lockCurrent();
        }

        if (state.winner) {
            toast("warning", `Runde ${state.round}: Gewinner: ${state.winner}`);
        }
    });

    conn.on("Replaced", handleReplaced);

    conn.onreconnecting(() => {
        const m = "Verbindung verloren – verbinde neu …";
        setConnectionIndicator("reconnecting");
        dom.status.textContent = m;
        layoutManager.lockCurrent();
        toast("warning", m);
    });

    conn.onreconnected(() => {
        setConnectionIndicator("connected");
        dom.status.textContent = "Wieder verbunden";
        toast("info", "Wieder verbunden");
    });

    conn.onclose((closeErr) => {
        if (state.replaced) return;
        showDisconnected(closeErr ? getErrMsg(closeErr) : "Verbindung geschlossen.");
    });
}

async function start() {
    dom.status.textContent = "Verbinde…";

    state.playerId = resolvePlayerId();
    if (!state.playerId) {
        const m = "Nicht verbunden: keine gültige Player-ID.";
        dom.name.textContent = "—";
        dom.status.textContent = m;
        showError(m);
        toast("error", m);
        return;
    }

    registerLayouts();

    const conn = new signalR.HubConnectionBuilder()
        .withUrl(`/hub?playerid=${encodeURIComponent(state.playerId)}`)
        .withAutomaticReconnect({ nextRetryDelayInMilliseconds: nextRetryDelay })
        .build();

    state.conn = conn;
    registerConnectionEvents(conn);

    try {
        await conn.start();
        showError("");
        setConnectionIndicator("connected");
        dom.status.textContent = WAITING_FOR_HOST;
    } catch (e) {
        showDisconnected(getErrMsg(e));
    }
}

start();
