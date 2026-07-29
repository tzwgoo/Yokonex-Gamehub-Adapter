local function SetupYokonexLink(inst)
    if inst ~= GLOBAL.ThePlayer then return end
    inst._yokonex_is_dead = inst:HasTag("playerghost")

    -- 只写游戏自己的客户端日志，由 GameHub 在本机读取，不访问外网。
    inst:ListenForEvent("healthdelta", function(player, data)
        if not data then return end
        if data.newpercent < data.oldpercent then
            local loss = (data.oldpercent - data.newpercent) * 100
            print(string.format("[GSI] EVENT:DAMAGED | LOSS:%.2f%%", loss))
        end
        if data.newpercent <= 0 and not player._yokonex_is_dead then
            player._yokonex_is_dead = true
            print("[GSI] EVENT:DEATH | REASON:HEALTH_ZERO")
        elseif data.newpercent > 0 and player._yokonex_is_dead then
            player._yokonex_is_dead = false
            print("[GSI] EVENT:RESURRECTED")
        end
    end)
end

AddPlayerPostInit(SetupYokonexLink)
