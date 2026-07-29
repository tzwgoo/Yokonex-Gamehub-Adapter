extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# Hook PickupItemFromTable — 在玩家拿起物品时触发 item_used (NEW.md: 物品使用事件)
# 此时 itemParent 尚未被释放，可以获取 slot 和 fromDealer
func PickupItemFromTable(chain: ModLoaderHookChain, itemParent: Node3D, passedItemName: String):
	var b = _eb()
	var slot = 0
	var from_dealer = false
	
	if b and itemParent and itemParent.get_child_count() >= 2:
		var inter = itemParent.get_child(1)
		slot = inter.itemGridIndex + 1
		from_dealer = not inter.isPlayerSide
		b.emit(b.EVT_ITEM_USED, {"type": passedItemName, "slot": slot, "fromDealer": from_dealer})
	
	await chain.execute_next_async([itemParent, passedItemName])

# Hook InteractWith — 处理需要时序延迟的特定物品事件
func InteractWith(chain: ModLoaderHookChain, itemName: String):
	var b = _eb()
	if b:
		match itemName:
			"magnifying glass":
				var ref = chain.reference_object
				var shell = "" if ref.roundManager.shellSpawner.sequenceArray.is_empty() else ref.roundManager.shellSpawner.sequenceArray[0]
				var tree = Engine.get_main_loop()
				tree.create_timer(2.0, false).timeout.connect(func():
					b.emit(b.EVT_MAGNIFYING_GLASS_USED, {"type": shell})
				, 4)
			"beer":
				pass  # beer_used 已移至 shell_eject_manager_ext.gd 的 BeerEjection_player()
	await chain.execute_next_async([itemName])
