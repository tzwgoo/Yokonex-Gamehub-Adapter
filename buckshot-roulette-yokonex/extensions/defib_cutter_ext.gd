extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

func CutWire(chain: ModLoaderHookChain, who: String):
	await chain.execute_next_async([who])
	var b = _eb()
	if b:
		b.emit(b.EVT_NO_REGEN, {})
