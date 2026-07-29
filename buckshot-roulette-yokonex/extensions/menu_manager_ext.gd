extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# 主动退出游戏 — MenuManager.Exit() 被调用时触发
func Exit(chain: ModLoaderHookChain):
	var b = _eb()
	if b:
		b.emit(b.EVT_GAME_CLOSE, {})
	await chain.execute_next_async([])
