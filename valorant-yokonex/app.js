(() => {
  "use strict";
  const GAME_ID = 21640;
  const FEATURES = ["gep_internal", "me", "game_info", "match_info", "kill", "death"];
  const CONFIG_URL = "http://127.0.0.1:43002/v1/game-integrations/valorant/adapter-config";
  const core = window.ValorantYokonexCore;
  const state = { playerId: "", playerName: "", rosterLocalName: "", map: "", round: 0, score: {}, mode: {}, sessionId: "", lastHealth: null, lastExplicitDeathAt: 0, matchOutcome: "", matchActive: false };
  const recent = new Map();
  const pending = [];
  let config = { enabled: false, endpoint: "ws://127.0.0.1:43002/valorant", mappings: {} };
  let socket = null;
  let sent = 0;
  let reconnectTimer = null;
  let gepRegistered = false;

  function setStatus(title, detail, online = false) {
    document.querySelector("#status").textContent = title;
    document.querySelector("#detail").textContent = detail;
    document.querySelector("#status-dot").classList.toggle("online", online);
  }

  async function loadConfig() {
    try {
      const response = await fetch(CONFIG_URL, { cache: "no-store" });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      config = await response.json();
      if (!config.enabled) return setStatus("联动未启用", "请在 Yokonex-Gamehub 中启用");
      connect();
    } catch (error) {
      setStatus("网关不可用", "请先启动 Yokonex-Gamehub");
      setTimeout(loadConfig, 2000);
    }
  }

  function connect() {
    if (!config.enabled || socket?.readyState === WebSocket.OPEN || socket?.readyState === WebSocket.CONNECTING) return;
    socket = new WebSocket(config.endpoint);
    socket.onopen = () => {
      setStatus("联动运行中", "正在接收 VALORANT 事件", true);
      const now = Date.now();
      while (pending.length) {
        const item = pending.shift();
        if (now - item.createdAt <= 3000) socket.send(JSON.stringify(item.payload));
      }
    };
    socket.onclose = () => {
      setStatus("网关已断开", "正在重新连接");
      clearTimeout(reconnectTimer);
      reconnectTimer = setTimeout(connect, 1200);
    };
    socket.onerror = () => socket.close();
  }

  function sessionId() {
    if (!state.sessionId) state.sessionId = `valorant-${Date.now()}`;
    return state.sessionId;
  }

  function emit(eventKey, matchValue = "", data = {}) {
    const commandId = String(config.mappings[eventKey] || "").trim();
    if (!config.enabled || !commandId) return;
    const now = Date.now();
    const duplicateKey = `${eventKey}|${JSON.stringify(data)}`;
    if (now - (recent.get(duplicateKey) || 0) < 500) return;
    recent.set(duplicateKey, now);
    for (const [key, createdAt] of recent) if (now - createdAt > 3000) recent.delete(key);
    const payload = {
      source: "valorant",
      eventKey,
      commandId,
      occurredAt: new Date().toISOString(),
      sessionId: sessionId(),
      eventId: `${sessionId()}-${eventKey}-${now}`,
      matchValue: matchValue || undefined,
      data,
    };
    if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(payload));
    else if (["valorant.match_start", "valorant.match_end", "valorant.player_kill", "valorant.player_headshot", "valorant.player_death", "valorant.spike_detonated", "valorant.spike_defused"].includes(eventKey)) {
      pending.push({ payload, createdAt: now });
      while (pending.length > 20) pending.shift();
      connect();
    } else return;
    document.querySelector("#sent").textContent = String(++sent);
  }

  function updateInfo(item) {
    const value = core.parseValue(item.value);
    if (item.key === "player_id") state.playerId = String(value || "");
    if (item.key === "player_name") state.playerName = String(value || "");
    if (item.key.startsWith("roster_") && value?.local) {
      state.playerId = String(value.player_id || state.playerId);
      state.rosterLocalName = String(value.name || "");
    }
    if (item.key === "map") state.map = String(value || "");
    if (item.key === "round_number") state.round = Number(value || 0);
    if (item.key === "score" || item.key === "match_score") state.score = value || {};
    if (item.key === "game_mode") state.mode = value || {};
    if (item.key === "match_outcome") state.matchOutcome = String(value || "");
    if (item.key === "pseudo_match_id" || item.key === "match_id") state.sessionId = String(value || "");
    if (item.key === "health") {
      const health = Number(value);
      if (state.lastHealth > 0 && health === 0 && Date.now() - state.lastExplicitDeathAt > 1000) emit("valorant.player_death", "health_fallback", { round: state.round, map: state.map, fallback: true });
      state.lastHealth = health;
    }
    if (item.key === "round_phase" && value === "shopping") emit("valorant.round_start", String(state.round), { round: state.round, score: state.score });
    if (item.key === "round_phase" && value === "end") emit("valorant.round_end", String(state.round), { round: state.round, score: state.score });
    if (item.key === "state" && ["LeavingMap", "Aborted"].includes(value) && state.matchActive) {
      emit("valorant.match_end", String(state.mode?.mode || ""), { map: state.map, score: state.score, outcome: state.matchOutcome, fallback: true });
      state.matchActive = false;
      state.sessionId = "";
    }
    document.querySelector("#player").textContent = state.rosterLocalName || state.playerName || "尚未识别";
  }

  function handleEvent(item) {
    const value = core.parseValue(item.value);
    if (item.key === "match_start") {
      state.sessionId = state.sessionId || `valorant-${Date.now()}`;
      state.matchActive = true;
      emit("valorant.match_start", state.map, { map: state.map, mode: state.mode });
    } else if (item.key === "match_end") {
      emit("valorant.match_end", String(state.mode?.mode || ""), { map: state.map, score: state.score, outcome: state.matchOutcome || "" });
      state.matchActive = false;
      state.sessionId = "";
    } else if (item.key === "spike_detonated" || item.key === "spike_defused") {
      emit(`valorant.${item.key}`, "", { round: state.round, map: state.map });
    } else if (item.key === "kill_feed" && value && typeof value === "object") {
      if (core.isLocalName(value.victim, state)) state.lastExplicitDeathAt = Date.now();
      for (const event of core.killEvents(value, state)) emit(event.eventKey, event.matchValue, event.data);
    }
  }

  function registerGep() {
    if (gepRegistered) return;
    if (!window.overwolf?.games?.events) return setStatus("Overwolf API 不可用", "请通过 Overwolf 加载此扩展");
    gepRegistered = true;
    overwolf.games.events.onInfoUpdates2.addListener((update) => {
      for (const category of Object.values(update.info || {})) {
        if (!category || typeof category !== "object") continue;
        for (const [key, value] of Object.entries(category)) updateInfo({ key, value });
      }
    });
    overwolf.games.events.onNewEvents.addListener((batch) => (batch.events || []).forEach(handleEvent));
    overwolf.games.events.setRequiredFeatures(FEATURES, (result) => {
      if (!result?.success) setStatus("事件暂不可用", result?.error || "GEP 注册失败");
      else loadConfig();
    });
  }

  function checkGame() {
    if (!window.overwolf?.games) return setStatus("Overwolf API 不可用", "请通过 Overwolf 加载此扩展");
    overwolf.games.getRunningGameInfo((info) => {
      if (info?.isRunning && Number(info.id) === GAME_ID) registerGep();
      else setStatus("等待游戏启动", "启动 VALORANT 后自动连接");
    });
  }

  if (window.overwolf?.games?.onGameInfoUpdated) {
    overwolf.games.onGameInfoUpdated.addListener((change) => {
      if (change?.gameInfo?.isRunning && Number(change.gameInfo.id) === GAME_ID) registerGep();
    });
  }
  checkGame();
  setInterval(loadConfig, 5000);
})();
