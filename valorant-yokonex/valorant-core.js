(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  root.ValorantYokonexCore = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  function parseValue(value) {
    if (typeof value !== "string") return value;
    try { return JSON.parse(value); } catch { return value; }
  }

  function normalizeName(value) {
    return String(value || "").trim().toLocaleLowerCase().replace(/\s*#\s*/g, "#");
  }

  function isLocalName(candidate, state) {
    const name = normalizeName(candidate);
    if (!name) return false;
    if (name === "me") return true;
    return [state.playerName, state.rosterLocalName].some((item) => normalizeName(item) === name);
  }

  function killEvents(feed, state) {
    const attackerLocal = isLocalName(feed.attacker, state);
    const victimLocal = isLocalName(feed.victim, state);
    const data = {
      attacker: feed.attacker || "",
      victim: feed.victim || "",
      weapon: feed.weapon || "",
      ultimate: feed.ult || "",
      headshot: Boolean(feed.headshot),
      round: state.round,
      map: state.map,
    };
    const events = [];
    if (attackerLocal) {
      events.push({ eventKey: "valorant.player_kill", matchValue: feed.weapon || "", data });
      if (feed.headshot) events.push({ eventKey: "valorant.player_headshot", matchValue: "headshot", data });
    } else if (feed.is_attacker_teammate === true) {
      events.push({ eventKey: "valorant.teammate_kill", matchValue: feed.weapon || "", data });
    }
    if (victimLocal) events.push({ eventKey: "valorant.player_death", matchValue: feed.weapon || "", data });
    return events;
  }

  return { parseValue, normalizeName, isLocalName, killEvents };
});
