extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# Hook: Signature.Input_Enter — 玩家按下Enter键，签名动画开始时触发
# WA#3 修复：移除不可靠的 hasSignedWaiver 检查（长名字时 1.0s 尚未完成签名）
# 改为 0.5s 定时器（初始 0.25s 等待 + 动画开始），仅校验名字有效性
func Input_Enter(chain: ModLoaderHookChain):
	var ref = chain.reference_object
	var b = _eb()
	
	if b:
		# 0.5s 后签名动画已开始（0.25s 初始等待 + 字母冲压开始）
		var tree = Engine.get_main_loop()
		tree.create_timer(0.5, false).timeout.connect(func():
			if not is_instance_valid(ref): return
			# 名字验证：无效签名不触发（空名、"dealer"、"god"）
			# 名字有效性在 Input_Enter 开头已同步校验，0.5s 后若函数未提前返回则名字有效
			if not ref.fullstring or ref.fullstring == "" or ref.fullstring == "dealer" or ref.fullstring == "god":
				return
			if not ref.roundManager:
				return
			var name_str = ref.fullstring.strip_edges()
			var infinite = ref.roundManager.endless
			b.emit(b.EVT_GAME_START, {"name": name_str, "infinite": infinite})
		, 4)
	
	await chain.execute_next_async([])
