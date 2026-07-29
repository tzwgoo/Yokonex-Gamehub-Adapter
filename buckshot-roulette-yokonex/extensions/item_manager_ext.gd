extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

func PlaceDownItem(chain: ModLoaderHookChain, gridIndex: int):
	var ref = chain.reference_object
	var itype = ref.temp_interaction.itemName if ref.temp_interaction else ""
	await chain.execute_next_async([gridIndex])
	var b = _eb()
	if b:
		b.emit(b.EVT_ITEM_PLACED, {"type": itype, "slot": gridIndex + 1})

func BeginItemGrabbing(chain: ModLoaderHookChain):
	var ref = chain.reference_object
	var b = _eb()
	var tree = Engine.get_main_loop()
	
	if b:
		# WA#7: 物品刷新事件 — 在桌面物品刷新动画开始时触发 (~0.8s)
		# 注意：仅在 BeginItemGrabbing 中触发，SetupItemClear 不再重复触发
		if ref.newBatchHasBegun:
			tree.create_timer(0.8, false).timeout.connect(func():
				if is_instance_valid(ref) and b:
					b.emit(b.EVT_ITEMS_CLEARED, {})
			, 4)
		
		# WA#5: 物品箱打开事件 — 在动画结束后触发
		var open_delay = 5.0 if ref.newBatchHasBegun else 2.5
		tree.create_timer(open_delay, false).timeout.connect(func():
			if b:
				b.emit(b.EVT_ITEM_BOX_OPENED, {})
		, 4)
	
	await chain.execute_next_async([])

func EndItemGrabbing(chain: ModLoaderHookChain):
	var b = _eb()
	var tree = Engine.get_main_loop()
	
	# WA#6: 物品箱关闭事件 — 在动画开始后 0.25 秒触发
	# 关闭动画(hide briefcase)在 ~0.45s 处开始，动画开始后0.25s ≈ 0.7s
	if b:
		tree.create_timer(0.7, false).timeout.connect(func():
			if b:
				b.emit(b.EVT_ITEM_BOX_CLOSED, {})
		, 4)
	
	await chain.execute_next_async([])

# SetupItemClear — 不再发射 items_cleared（已由 BeginItemGrabbing 的 0.8s 定时器处理）
func SetupItemClear(chain: ModLoaderHookChain):
	chain.execute_next([])

# GrabItems_Enemy — 无操作，保留链完整性
func GrabItems_Enemy(chain: ModLoaderHookChain):
	await chain.execute_next_async([])
