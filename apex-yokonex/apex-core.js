(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  root.ApexYokonexCore = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  const EVENT_KEYS = {
    match_start: "apex.match_start",
    match_end: "apex.match_end",
    round_start: "apex.round_start",
    round_end: "apex.round_end",
    damage: "apex.damage",
    knockdown: "apex.knockdown",
    kill: "apex.kill",
    assist: "apex.assist",
    knocked_out: "apex.player_knocked",
    death: "apex.player_death",
    healed_from_ko: "apex.player_revived",
    respawn: "apex.respawn",
  };

  function parseValue(value) {
    if (typeof value !== "string") return value;
    try { return JSON.parse(value); } catch { return value; }
  }

  function eventDescriptor(item) {
    const name = String(item?.name || item?.key || "");
    const eventKey = EVENT_KEYS[name];
    if (!eventKey) return null;
    const value = parseValue(item?.data ?? item?.value ?? null);
    const data = value && typeof value === "object" ? value : value == null || value === "" ? {} : { value };
    let matchValue = "";
    if (name === "damage") matchValue = String(data.damageAmount || data.damage_amount || "");
    if (name === "kill" || name === "assist") matchValue = String(data.value ?? value ?? "");
    return { eventKey, matchValue, data };
  }

  return { EVENT_KEYS, parseValue, eventDescriptor };
});
