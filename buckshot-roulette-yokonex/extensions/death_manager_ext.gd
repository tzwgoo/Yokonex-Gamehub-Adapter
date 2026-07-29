extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

func MainDeathRoutine(chain: ModLoaderHookChain):
	var b = _eb()
	if b:
		b.emit(b.EVT_GAME_LOST, {})
	await chain.execute_next_async([])
