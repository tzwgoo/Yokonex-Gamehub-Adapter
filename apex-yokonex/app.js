(() => {
  "use strict";
  const GAME_ID = 21566;
  const FEATURES = ["gep_internal", "me", "game_info", "match_info", "team", "kill", "damage", "death", "revive", "match_state", "match_summary", "rank"];
  const CONFIG_URL = "http://127.0.0.1:43002/v1/game-integrations/apex/adapter-config";
  const core = window.ApexYokonexCore;
  const state = { player: "", map: "", mode: "", phase: "", sessionId: "", teamState: "", victory: false, summary: {}, matchActive: false };
  const recent = new Map();
  const pending = [];
  let config = { enabled: false, endpoint: "ws://127.0.0.1:43002/v1/events", mappings: {} };
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
      if (!config.enabled) {
        if (socket?.readyState === WebSocket.OPEN) socket.close();
        return setStatus("联动未启用", "请在 Yokonex-Gamehub 中启用");
      }
      connect();
    } catch {
      setStatus("网关不可用", "请先启动 Yokonex-Gamehub");
    }
  }

  function connect() {
    if (!config.enabled || socket?.readyState === WebSocket.OPEN || socket?.readyState === WebSocket.CONNECTING) return;
    socket = new WebSocket(config.endpoint);
    socket.onopen = () => {
      setStatus("联动运行中", "正在接收 Apex Legends 事件", true);
      const now = Date.now();
      while (pending.length) {
        const item = pending.shift();
        if (now - item.createdAt <= 3000) socket.send(JSON.stringify(item.payload));
      }
    };
    socket.onclose = () => {
      if (!config.enabled) return;
      setStatus("网关已断开", "正在重新连接");
      clearTimeout(reconnectTimer);
      reconnectTimer = setTimeout(connect, 1200);
    };
    socket.onerror = () => socket.close();
  }

  function currentSessionId() {
    if (!state.sessionId) state.sessionId = `apex-${Date.now()}`;
    return state.sessionId;
  }

  function emit(eventKey, matchValue = "", data = {}) {
    const commandId = String(config.mappings[eventKey] || "").trim();
    if (!config.enabled || !commandId) return;
    const now = Date.now();
    const duplicateKey = `${eventKey}|${JSON.stringify(data)}`;
    if (now - (recent.get(duplicateKey) || 0) < 120) return;
    recent.set(duplicateKey, now);
    for (const [key, createdAt] of recent) if (now - createdAt > 3000) recent.delete(key);
    const sessionId = currentSessionId();
    const payload = {
      source: "apex",
      eventKey,
      commandId,
      occurredAt: new Date().toISOString(),
      sessionId,
      eventId: `${sessionId}-${eventKey}-${now}`,
      matchValue: matchValue || undefined,
      data,
    };
    if (socket?.readyState === WebSocket.OPEN) socket.send(JSON.stringify(payload));
    else if (eventKey !== "apex.damage") {
      pending.push({ payload, createdAt: now });
      while (pending.length > 30) pending.shift();
      connect();
    } else return;
    document.querySelector("#sent").textContent = String(++sent);
  }

  function updateInfo(item) {
    const value = core.parseValue(item.value);
    if (item.key === "name" || item.key === "player") state.player = String(value?.player_name || value || "");
    if (item.key === "pseudo_match_id") state.sessionId = String(value || "");
    if (item.key === "map_name" || item.key === "map_id") state.map = String(value || "");
    if (item.key === "mode_name" || item.key === "game_mode") state.mode = String(value || "");
    if (item.key === "phase") state.phase = String(value || "");
    if (item.key === "match_summary") state.summary = value && typeof value === "object" ? value : {};
    if (item.key === "victory") {
      const victory = value === true || value === "true";
      if (victory && !state.victory) emit("apex.victory", "victory", { map: state.map, mode: state.mode, summary: state.summary });
      state.victory = victory;
    }
    if (item.key === "team_info") {
      const teamState = String(value?.team_state || "");
      if (teamState === "eliminated" && state.teamState !== "eliminated") emit("apex.team_eliminated", "eliminated", { map: state.map, mode: state.mode });
      state.teamState = teamState;
    }
    document.querySelector("#player").textContent = state.player || "尚未识别";
  }

  function handleEvent(item) {
    const descriptor = core.eventDescriptor(item);
    if (!descriptor) return;
    if (descriptor.eventKey === "apex.match_start") {
      state.sessionId = state.sessionId || `apex-${Date.now()}`;
      state.matchActive = true;
      state.victory = false;
      state.teamState = "active";
    }
    const data = { ...descriptor.data, player: state.player, map: state.map, mode: state.mode, phase: state.phase };
    emit(descriptor.eventKey, descriptor.matchValue, data);
    if (descriptor.eventKey === "apex.match_end") {
      state.matchActive = false;
      state.sessionId = "";
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
      else setStatus("等待游戏启动", "启动 Apex Legends 后自动连接");
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
