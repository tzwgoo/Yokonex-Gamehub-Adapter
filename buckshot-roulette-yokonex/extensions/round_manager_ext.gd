extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# WA#3: round_start — 在视角移动到小屏幕时触发（RoundIndicator 开始时）
func RoundIndicator(chain: ModLoaderHookChain):
	var ref = chain.reference_object
	var b = _eb()
	if b:
		var h = 3
		if ref.roundArray.size() > 0:
			h = ref.roundArray[0].startingHealth
		var round_num = ref.playerData.currentBatchIndex + 1
		b.emit(b.EVT_ROUND_START, {"round": round_num, "health": h, "infinite": ref.endless})
	await chain.execute_next_async([])

func StartRound(chain: ModLoaderHookChain, gettingNext: bool):
	# handcuffs_removed 已移至 handcuff_manager_ext.gd 的 RemoveAllCuffsRoutine 钩子
	await chain.execute_next_async([gettingNext])

# handcuffs_removed 已移至 handcuff_manager_ext.gd 的 BreakPlayerHandCuffs 钩子
func BeginPlayerTurn(chain: ModLoaderHookChain):
	await chain.execute_next_async([])

# 游戏结束时复位手铐标记
func OutOfHealth(chain: ModLoaderHookChain, who: String):
	var b = _eb()
	if b and who == "dealer":
		b.emit(b.EVT_ROUND_WON, {})
	await chain.execute_next_async([who])

func EndMainBatch(chain: ModLoaderHookChain):
	await chain.execute_next_async([])
	var b = _eb()
	if b and chain.reference_object.playerData.currentBatchIndex == 3 and not chain.reference_object.endless:
		b.emit(b.EVT_GAME_WON, {})

func Response(chain: ModLoaderHookChain, rep: bool):
	await chain.execute_next_async([rep])
	var b = _eb()
	if b:
		if not rep and chain.reference_object.endless:
			b.emit(b.EVT_GAME_WON, {})
		if chain.reference_object.endless:
			b.emit(b.EVT_DOUBLE_OR_NOTHING, {"select": rep})

# WA#15: 分数更新事件 — 在小屏幕显示分数动画完成后、"加倍/放弃"按钮显示前触发
func BeginScoreLerp(chain: ModLoaderHookChain):
	var ref = chain.reference_object
	var b = _eb()
	
	if b:
		# 原版时序: 1.1s(等待) + 0.5s(等待) + 3.08s(分数动画) + 0.46s(等待) = ~5.14s
		# 分数动画在 1.1+0.5=1.6s 处开始，持续 3.08s，在约 4.68s 处结束
		# 按钮在 4.68+0.46=5.14s 后显示，在 5.14+0.5+1.0=6.64s 处可交互
		# 在分数动画结束、按钮显示动画开始前触发：~4.7s
		var tree = Engine.get_main_loop()
		tree.create_timer(4.7, false).timeout.connect(func():
			if not is_instance_valid(ref): return
			b.emit(b.EVT_SCORE_UPDATED, {"score": ref.double_or_nothing_score})
		, 4)
	
	await chain.execute_next_async([])
