import { Layouts } from "./constants.js";
import { dom, resolvePlayerId, showError, getErrMsg, toast } from "./helpers.js";
import { LayoutManager } from "./layoutManager.js";
import { BuzzerLayout } from "./layouts/buzzerLayout.js";
import { KeySelectLayout } from "./layouts/keySelectLayout.js";
import { InputLayout } from "./layouts/inputLayout.js";

const state = {
    playerId: null,
    playerName: "—",
    round: 0,
    currentLayout: Layouts.None,
    layoutInfo: null,
    currentLayoutLocked: true,
    allLocked: false,
    winner: null,
    conn: null
};

const layoutManager = new LayoutManager(state);

function buildContext() {
    return {
        host: dom.host,
        playerId: state.playerId,
        playerName: state.playerName,
        round: state.round,
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
            return `Runde ${state.round}: kein Layout aktiv`;
    }
}

function applyServerState(serverState) {
    const previousLayout = state.currentLayout;

    state.playerName = serverState.playerName ?? state.playerName;
    state.round = serverState.round ?? state.round;
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

function registerLayouts() {
    layoutManager.register(Layouts.Buzzer, new BuzzerLayout({
        buzz: async () => {
            try {
                await state.conn.invoke("Buzz");
            } catch (e) {
                const m = getErrMsg(e);
                showError(m);
                toast("error", m);
            }
        }
    }));

    layoutManager.register(Layouts.KeySelect, new KeySelectLayout({
        submitSelection: async (payload) => {
            try {
                await state.conn.invoke("SelectionResults", payload);
            } catch (e) {
                const m = getErrMsg(e);
                showError(m);
                toast("error", m);
            }
        }
    }));

    layoutManager.register(Layouts.Input, new InputLayout({
        submitInput: async (payload) => {
            try {
                await state.conn.invoke("SubmitInput", payload);
            } catch (e) {
                const m = getErrMsg(e);
                showError(m);
                toast("error", m);
            }
        }
    }));
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
        .withAutomaticReconnect()
        .build();

    state.conn = conn;

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

    conn.on("Error", (errorMessage) => {
        showError(errorMessage);
        toast("error", errorMessage);
    });

    conn.onreconnecting(() => {
        dom.status.textContent = "Verbindung verloren… reconnecting";
        layoutManager.lockCurrent();
        toast("warning", "Verbindung verloren… reconnecting");
    });

    conn.onreconnected(() => {
        dom.status.textContent = "Wieder verbunden";
        toast("info", "Wieder verbunden");
    });

    conn.onclose((closeErr) => {
        const m = closeErr ? getErrMsg(closeErr) : "Verbindung geschlossen.";
        showError(m);
        dom.status.textContent = "Nicht verbunden: Verbindung geschlossen.";
        layoutManager.lockCurrent();
        toast("error", m);
    });

    try {
        await conn.start();
        showError("");
        dom.status.textContent = "Bereit";
        toast("debug", "Bereit");
    } catch (e) {
        const m = getErrMsg(e);
        showError(m);
        dom.status.textContent = "Nicht verbunden: Verbindung fehlgeschlagen.";
        layoutManager.lockCurrent();
        toast("error", m);
    }
}

start();