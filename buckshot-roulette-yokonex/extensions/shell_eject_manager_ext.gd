extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# 玩家啤酒抛壳 — 在 ShellEjectManager.BeerEjection_player() 触发时立即发射事件
# 此时 sequenceArray[0] 是将被弹出的子弹，size() 是弹出前的总数
func BeerEjection_player(chain: ModLoaderHookChain):
	var b = _eb()
	if b:
		var ref = chain.reference_object
		if ref.shellSpawner:
			var arr = ref.shellSpawner.sequenceArray
			var shell = "" if arr.is_empty() else arr[0]
			var qty = max(0, arr.size() - 1)
			b.emit(b.EVT_BEER_USED, {"type": shell, "bulletQuantity": qty, "source": "Player"})
	await chain.execute_next_async([])

# 庄家啤酒抛壳 — 在 ShellEjectManager.BeerEjection_dealer() 触发时立即发射事件
func BeerEjection_dealer(chain: ModLoaderHookChain):
	var b = _eb()
	if b:
		var ref = chain.reference_object
		if ref.shellSpawner:
			var arr = ref.shellSpawner.sequenceArray
			var shell = "" if arr.is_empty() else arr[0]
			var qty = max(0, arr.size() - 1)
			b.emit(b.EVT_BEER_USED, {"type": shell, "bulletQuantity": qty, "source": "Dealer"})
	await chain.execute_next_async([])
