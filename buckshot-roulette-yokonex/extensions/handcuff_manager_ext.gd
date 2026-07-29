extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# 中场挣脱解铐动画开始时触发（BreakPlayerHandCuffs → animator 播放 "player break handcuffs"）
func BreakPlayerHandCuffs(chain: ModLoaderHookChain, lerpingToPrevious: bool):
	var b = _eb()
	if b:
		b.emit(b.EVT_HANDCUFFS_REMOVED, {})
	await chain.execute_next_async([lerpingToPrevious])

# 回合切换解铐动画开始时触发（RemoveAllCuffsRoutine → animator 播放 "player break handcuffs"）
func RemoveAllCuffsRoutine(chain: ModLoaderHookChain):
	var ref = chain.reference_object
	var b = _eb()
	if b and ref.roundManager.playerCuffed:
		b.emit(b.EVT_HANDCUFFS_REMOVED, {})
	await chain.execute_next_async([])

# 方案 B：庄家给玩家戴手铐动画关键帧 → 发射 handcuffs_applied
# 若动画未调用此函数则不会触发
func AttachHandCuffs(chain: ModLoaderHookChain):
	var b = _eb()
	if b:
		b.emit(b.EVT_HANDCUFFS_APPLIED, {})
	await chain.execute_next_async([])
