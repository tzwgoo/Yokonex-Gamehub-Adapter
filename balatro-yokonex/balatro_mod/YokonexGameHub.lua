--- STEAMODDED HEADER
--- MOD_NAME: Yokonex GameHub
--- MOD_ID: YokonexGameHub
--- MOD_AUTHOR: [辞年]
--- MOD_DESCRIPTION: 将 Balatro 关键事件写入本机 GameHub 桥接日志
--- VERSION: 1.0.0
--- DEPENDENCIES: [Steamodded>=1.0.0]

local function emit(event_key, data)
    data = data or {}
    data.ante = G and G.GAME and G.GAME.round_resets and G.GAME.round_resets.ante or nil
    data.round = G and G.GAME and G.GAME.round or nil
    local ok, encoded = pcall(json.encode, {eventKey = event_key, data = data})
    if ok then
        -- love.filesystem 的保存目录固定在 %APPDATA%\Balatro。
        love.filesystem.append("yokonex_events.log", encoded .. "\n")
    end
end

local old_start_run = Game.start_run
function Game:start_run(args)
    local result = old_start_run(self, args)
    emit("balatro.run_started", {
        seed = G.GAME and G.GAME.pseudorandom and G.GAME.pseudorandom.seed or nil,
        stake = G.GAME and G.GAME.stake or nil
    })
    return result
end

local old_add_to_deck = Card.add_to_deck
function Card:add_to_deck(from_debuff)
    local result = old_add_to_deck(self, from_debuff)
    if self.ability and self.ability.set == "Joker" then
        emit("balatro.joker_gained", {name = self.ability.name, key = self.config and self.config.center and self.config.center.key})
    elseif self.ability and (self.ability.set == "Tarot" or self.ability.set == "Planet" or self.ability.set == "Spectral") then
        emit("balatro.consumable_gained", {name = self.ability.name, set = self.ability.set})
    end
    return result
end

local old_remove_from_deck = Card.remove_from_deck
function Card:remove_from_deck(from_debuff)
    if self.added_to_deck and self.ability and self.ability.set == "Joker" then
        emit("balatro.joker_lost", {name = self.ability.name, key = self.config and self.config.center and self.config.center.key})
    end
    return old_remove_from_deck(self, from_debuff)
end

local old_ease_dollars = ease_dollars
function ease_dollars(amount, instant)
    local result = old_ease_dollars(amount, instant)
    if amount and amount > 0 then
        emit("balatro.money_gained", {amount = amount, dollars = G.GAME and G.GAME.dollars})
    elseif amount and amount < 0 then
        emit("balatro.money_spent", {amount = -amount, dollars = G.GAME and G.GAME.dollars})
    end
    return result
end

local old_evaluate_play = G.FUNCS.evaluate_play
G.FUNCS.evaluate_play = function(e)
    local result = old_evaluate_play(e)
    emit("balatro.hand_played", {
        hand = G.GAME and G.GAME.last_hand_played,
        chips = G.GAME and G.GAME.chips,
        handsLeft = G.GAME and G.GAME.current_round and G.GAME.current_round.hands_left
    })
    return result
end

local old_end_round = end_round
function end_round()
    local boss = G.GAME and G.GAME.blind and G.GAME.blind.boss
    local blind_name = G.GAME and G.GAME.blind and G.GAME.blind.name
    local result = old_end_round()
    emit("balatro.round_ended", {blind = blind_name, boss = boss and true or false})
    if boss then emit("balatro.boss_blind_defeated", {blind = blind_name}) end
    return result
end

local old_game_over = G.FUNCS.game_over
G.FUNCS.game_over = function(e)
    emit("balatro.game_over", {
        ante = G.GAME and G.GAME.round_resets and G.GAME.round_resets.ante,
        dollars = G.GAME and G.GAME.dollars
    })
    return old_game_over(e)
end
