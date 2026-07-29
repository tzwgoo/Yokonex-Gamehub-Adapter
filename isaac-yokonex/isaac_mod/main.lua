local mod = RegisterMod("Yokonex GameHub Link", 1)
local json = require("json")
local game = Game()
local itemConfig = Isaac.GetItemConfig()
local LOG_PREFIX = "[YOKONEX_ISAAC] "
local INVENTORY_SCAN_INTERVAL = 30

local runActive = false
local playerStates = {}
local maxCollectibleId = itemConfig:GetCollectibles().Size - 1

local function emit(eventKey, data)
    local payload = {
        eventKey = eventKey,
        data = data or {}
    }
    -- Mod 只写本地游戏日志，网络发送由 GameHub 桥接器完成。
    Isaac.DebugString(LOG_PREFIX .. json.encode(payload))
end

local function playerSlot(target)
    local targetHash = GetPtrHash(target)

    -- 先匹配 Game 玩家列表，确保雅各与以扫等双角色各自有稳定序号。
    for index = 0, game:GetNumPlayers() - 1 do
        if GetPtrHash(Isaac.GetPlayer(index)) == targetHash then
            return index, "main"
        end
    end

    -- 骨哥灵魂、里遗骸等子角色可能不在主玩家列表中。
    for index = 0, game:GetNumPlayers() - 1 do
        local root = Isaac.GetPlayer(index)
        local subPlayer = root:GetSubPlayer()
        if subPlayer and GetPtrHash(subPlayer) == targetHash then
            return index, "sub_player"
        end
        local otherTwin = root:GetOtherTwin()
        if otherTwin and GetPtrHash(otherTwin) == targetHash then
            return index, "other_twin"
        end
    end
    return -1, "unindexed"
end

local function playerData(player, extra)
    local index, role = playerSlot(player)
    local data = {
        playerIndex = index,
        playerRole = role,
        playerHash = tostring(GetPtrHash(player)),
        controllerIndex = player.ControllerIndex,
        playerType = player:GetPlayerType(),
        characterName = player:GetName()
    }
    for key, value in pairs(extra or {}) do
        data[key] = value
    end
    return data
end

local function playerVitals(player)
    local redHearts = player:GetHearts()
    local soulHearts = player:GetSoulHearts()
    local boneHearts = player:GetBoneHearts()
    return {
        redHearts = redHearts,
        soulHearts = soulHearts,
        boneHearts = boneHearts,
        eternalHearts = player:GetEternalHearts(),
        goldenHearts = player:GetGoldenHearts(),
        rottenHearts = player:GetRottenHearts(),
        bloodCharge = player:GetEffectiveBloodCharge(),
        soulCharge = player:GetEffectiveSoulCharge(),
        -- 骨心容器为空时仍可承受一次伤害，因此按一个半心单位计入。
        totalHealth = redHearts + soulHearts + boneHearts
    }
end

local function captureInventory(player)
    local inventory = {}
    for itemId = 1, maxCollectibleId do
        local count = player:GetCollectibleNum(itemId, true)
        if count > 0 then
            inventory[itemId] = count
        end
    end
    return inventory
end

local function itemData(player, itemId, count, delta)
    local config = itemConfig:GetCollectible(itemId)
    return playerData(player, {
        itemId = itemId,
        itemName = config and config.Name or "",
        count = count,
        delta = delta
    })
end

local function checkInventory(player, state, frame)
    if frame - state.lastInventoryScan < INVENTORY_SCAN_INTERVAL then return end
    state.lastInventoryScan = frame

    -- 背包快照能覆盖底座拾取、D4/D100 重投、控制台和其他 Mod 给予的道具。
    local current = captureInventory(player)
    for itemId, count in pairs(current) do
        local previous = state.inventory[itemId] or 0
        if count > previous then
            emit("isaac.collectible_gained", itemData(player, itemId, count, count - previous))
        end
    end
    for itemId, previous in pairs(state.inventory) do
        local count = current[itemId] or 0
        if count < previous then
            emit("isaac.collectible_lost", itemData(player, itemId, count, previous - count))
        end
    end
    state.inventory = current
end

local function roomData()
    local level = game:GetLevel()
    local room = game:GetRoom()
    local descriptor = level:GetCurrentRoomDesc()
    return {
        stage = level:GetStage(),
        stageType = level:GetStageType(),
        roomIndex = descriptor and descriptor.GridIndex or -1,
        roomType = room:GetType(),
        firstVisit = room:IsFirstVisit()
    }
end

local function onGameStarted(_, isContinued)
    runActive = true
    playerStates = {}

    local seeds = game:GetSeeds()
    emit("isaac.run_started", {
        continued = isContinued,
        runSeed = seeds:GetStartSeed(),
        playerCount = game:GetNumPlayers()
    })

    -- 游戏开始回调晚于首次房间/层回调，因此在这里补发初始位置。
    local current = roomData()
    emit("isaac.floor_entered", {
        stage = current.stage,
        stageType = current.stageType
    })
    emit("isaac.room_entered", current)
end

local function onGameEnded(_, isGameOver)
    if not runActive then return end
    emit("isaac.run_ended", { gameOver = isGameOver })
    runActive = false
    playerStates = {}
end

local function onNewLevel()
    if not runActive then return end
    local level = game:GetLevel()
    emit("isaac.floor_entered", {
        stage = level:GetStage(),
        stageType = level:GetStageType()
    })
end

local function onNewRoom()
    if not runActive then return end
    emit("isaac.room_entered", roomData())
end

local function onPlayerDamaged(_, entity, amount, damageFlags, source)
    if not runActive or entity.Type ~= EntityType.ENTITY_PLAYER then return end
    local player = entity:ToPlayer()
    if not player then return end

    emit("isaac.player_damaged", playerData(player, {
        amount = amount,
        damageFlags = damageFlags,
        sourceType = source and source.Type or 0,
        sourceVariant = source and source.Variant or 0,
        vitals = playerVitals(player)
    }))
end

local function onPlayerEffectUpdate(_, player)
    if not runActive then return end
    local key = GetPtrHash(player)
    local playerType = player:GetPlayerType()
    local characterName = player:GetName()
    local dead = player:IsDead()
    local vitals = playerVitals(player)
    local frame = game:GetFrameCount()
    local state = playerStates[key]

    if not state then
        playerStates[key] = {
            playerType = playerType,
            characterName = characterName,
            dead = dead,
            vitals = vitals,
            inventory = captureInventory(player),
            lastInventoryScan = frame
        }
        emit("isaac.player_joined", playerData(player, { vitals = vitals }))
        return
    end

    -- 拉撒路、里拉撒路、遗骸等切换形态时先重建基线，避免误报治疗和道具变化。
    if state.playerType ~= playerType then
        emit("isaac.character_changed", playerData(player, {
            previousPlayerType = state.playerType,
            previousCharacterName = state.characterName,
            vitals = vitals
        }))
        state.playerType = playerType
        state.characterName = characterName
        state.dead = dead
        state.vitals = vitals
        state.inventory = captureInventory(player)
        state.lastInventoryScan = frame
        return
    end

    if state.dead and not dead then
        emit("isaac.player_revived", playerData(player, { vitals = vitals }))
    elseif not dead and vitals.totalHealth > state.vitals.totalHealth then
        emit("isaac.player_healed", playerData(player, {
            amount = vitals.totalHealth - state.vitals.totalHealth,
            vitals = vitals
        }))
    end
    if dead and not state.dead then
        emit("isaac.player_died", playerData(player, { vitals = vitals }))
    end

    state.dead = dead
    state.vitals = vitals
    checkInventory(player, state, frame)
end

local function onEntityKilled(_, entity)
    if not runActive or not entity:IsBoss() then return end
    emit("isaac.boss_killed", {
        entityType = entity.Type,
        variant = entity.Variant,
        subType = entity.SubType,
        bossId = entity:GetBossID()
    })
end

mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, onGameStarted)
mod:AddCallback(ModCallbacks.MC_POST_GAME_END, onGameEnded)
mod:AddCallback(ModCallbacks.MC_POST_NEW_LEVEL, onNewLevel)
mod:AddCallback(ModCallbacks.MC_POST_NEW_ROOM, onNewRoom)
mod:AddCallback(ModCallbacks.MC_ENTITY_TAKE_DMG, onPlayerDamaged, EntityType.ENTITY_PLAYER)
mod:AddCallback(ModCallbacks.MC_POST_PEFFECT_UPDATE, onPlayerEffectUpdate)
mod:AddCallback(ModCallbacks.MC_POST_ENTITY_KILL, onEntityKilled)
