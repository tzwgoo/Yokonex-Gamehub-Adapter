local frame = CreateFrame("Frame")

frame:RegisterEvent("PLAYER_LOGIN")
frame:SetScript("OnEvent", function()
    -- 只开启游戏原生战斗日志，事件由本机 GameHub 读取。
    local enabled = LoggingCombat(true)
    if enabled then
        print("|cff57d38cYokonex GameHub：战斗日志已开启。|r")
    else
        print("|cffffcc00Yokonex GameHub：战斗日志开启失败，请稍后输入 /combatlog。|r")
    end
end)
