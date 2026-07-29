local UDP_PORT = 34198

local function player_data(player_index)
  local player = player_index and game.get_player(player_index) or nil
  if not player then return { playerIndex = player_index } end
  return {
    playerIndex = player.index,
    playerName = player.name,
    force = player.force and player.force.name or nil,
    surface = player.surface and player.surface.name or nil
  }
end

local function emit(event_key, data, player_index)
  data = data or {}
  data.tick = game.tick
  local player = player_data(player_index)
  for key, value in pairs(player) do data[key] = value end
  local packet = helpers.table_to_json({ eventKey = event_key, data = data })
  -- Factorio 仅允许向 localhost 发包；失败通常表示未加 --enable-lua-udp。
  local ok, error_message = pcall(helpers.send_udp, UDP_PORT, packet, player_index)
  if not ok then log("[YOKONEX_FACTORIO] " .. tostring(error_message)) end
end

script.on_event(defines.events.on_entity_damaged, function(event)
  local entity = event.entity
  if entity and entity.valid and entity.type == "character" and entity.player then
    emit("factorio.player_damaged", {
      amount = event.final_damage_amount,
      damageType = event.damage_type and event.damage_type.name or nil,
      health = entity.health
    }, entity.player.index)
  end
end)

script.on_event(defines.events.on_player_died, function(event)
  emit("factorio.player_died", { cause = event.cause and event.cause.name or nil }, event.player_index)
end)

script.on_event(defines.events.on_player_respawned, function(event)
  emit("factorio.player_respawned", {}, event.player_index)
end)

local function on_built(event)
  local entity = event.created_entity or event.entity
  if entity and entity.valid then
    emit("factorio.entity_built", { entity = entity.name, entityType = entity.type }, event.player_index)
  end
end

script.on_event({
  defines.events.on_built_entity,
  defines.events.on_robot_built_entity,
  defines.events.script_raised_built,
  defines.events.script_raised_revive
}, on_built)

local function on_mined(event)
  local entity = event.entity
  if entity and entity.valid then
    emit("factorio.entity_mined", { entity = entity.name, entityType = entity.type }, event.player_index)
  end
end

script.on_event({
  defines.events.on_player_mined_entity,
  defines.events.on_robot_mined_entity
}, on_mined)

script.on_event(defines.events.on_research_finished, function(event)
  emit("factorio.research_finished", {
    research = event.research and event.research.name or nil,
    force = event.research and event.research.force.name or nil
  })
end)

script.on_event(defines.events.on_achievement_gained, function(event)
  emit("factorio.achievement_unlocked", {
    achievement = event.achievement and event.achievement.name or nil
  }, event.player_index)
end)

script.on_event(defines.events.on_rocket_launched, function(event)
  emit("factorio.rocket_launched", {
    silo = event.rocket_silo and event.rocket_silo.name or nil
  })
end)

script.on_event(defines.events.on_train_changed_state, function(event)
  emit("factorio.train_state_changed", {
    trainId = event.train and event.train.id or nil,
    oldState = event.old_state,
    newState = event.train and event.train.state or nil
  })
end)
