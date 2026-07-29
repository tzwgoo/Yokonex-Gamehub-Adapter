extends Object

static var _dc_depth = 0
static var _pre_player_items = {}

func _eb():
	return Engine.get_meta("br_api_bus", null)

# 庄家射击事件 — 在扣动扳机时触发 (WA#4)
func Shoot(chain: ModLoaderHookChain, who: String):
	var ref = chain.reference_object
	# 在方法入口快照数据
	var chamber = "" if ref.shellSpawner.sequenceArray.is_empty() else ref.shellSpawner.sequenceArray[0]
	var src = "Dealer"
	var tgt = "Player" if who != "self" else "Dealer"
	var b = _eb()

	if b:
		# 庄家扣扳机时机: 打玩家 2.0s, 打自己 2.2s, 取 2.0s 折中
		var tree = Engine.get_main_loop()
		tree.create_timer(2.0, false).timeout.connect(func():
			if not is_instance_valid(ref):
				return
			var fired = chamber == "live"
			var dmg = ref.roundManager.currentShotgunDamage if fired else 0
			# WA#10: bulletQuantity = 开枪后的剩余弹药数 (size - 1)
			# NEW.md: shoot 事件不再包含 health 参数
			b.emit(b.EVT_SHOOT, {
				"source": src, "apply": tgt, "inFire": fired,
				"damage": dmg,
				"bulletQuantity": max(0, ref.shellSpawner.sequenceArray.size() - 1)
			})
		, 4)

	await chain.execute_next_async([who])

# 庄家使用肾上腺素偷取玩家物品事件
func DealerChoice(chain: ModLoaderHookChain):
	_dc_depth += 1
	var ref = chain.reference_object
	var b = _eb()

	if _dc_depth == 1 and b:
		_pre_player_items.clear()
		for c in ref.itemManager.itemSpawnParent.get_children():
			if not is_instance_valid(c) or c.get_child_count() < 2:
				continue
			if not (c.get_child(0) is PickupIndicator):
				continue
			var inter = c.get_child(1)
			if inter.isPlayerSide and inter.itemName != "":
				_pre_player_items[inter.itemGridIndex] = inter.itemName

	await chain.execute_next_async([])

	if _dc_depth == 1 and b:
		var current_player_items = {}
		for c in ref.itemManager.itemSpawnParent.get_children():
			if not is_instance_valid(c) or c.get_child_count() < 2:
				continue
			if not (c.get_child(0) is PickupIndicator):
				continue
			var inter = c.get_child(1)
			if inter.isPlayerSide and inter.itemName != "":
				current_player_items[inter.itemGridIndex] = inter.itemName
		for slot in _pre_player_items:
			if not current_player_items.has(slot):
				b.emit(b.EVT_ITEM_STOLEN, {"type": _pre_player_items[slot], "slot": slot + 1})
		_pre_player_items.clear()

	# 确保深度计数器始终递减，防止异常导致后续调用全部失效
	_dc_depth = max(0, _dc_depth - 1)
