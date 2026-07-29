extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# WA#9: 弹药总数量更新事件 — NEW.md 格式 {value, live, blank}
func SpawnShells(chain: ModLoaderHookChain, numberOfShells: int, numberOfLives: int, numberOfBlanks: int, shufflingArray: bool):
	chain.execute_next([numberOfShells, numberOfLives, numberOfBlanks, shufflingArray])
	var b = _eb()
	if b:
		b.emit(b.EVT_AMMO_UPDATED, {"value": numberOfShells, "live": numberOfLives, "blank": numberOfBlanks})
