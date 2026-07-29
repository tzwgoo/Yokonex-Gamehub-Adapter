extends Object

static var _prev_player_hp = -1
static var _prev_dealer_hp = -1
static var _initialized = false

func _eb():
	return Engine.get_meta("br_api_bus", null)

# SetupHealth — 新轮次开始时初始化血量追踪
func SetupHealth(chain: ModLoaderHookChain):
	await chain.execute_next_async([])
	var ref = chain.reference_object
	if ref and ref.roundManager:
		var rm = ref.roundManager
		_prev_player_hp = rm.health_player
		_prev_dealer_hp = rm.health_opponent
		_initialized = true

# 彻底重写：统一 hook UpdateDisplay（sync）
# UpdateDisplay 是所有血量显示路径的唯一汇聚点：
#   - 射击伤害 → UpdateDisplayRoutine → UpdateDisplay
#   - 香烟/药品 → UpdateDisplayRoutineCigarette_Main → UpdateDisplay
#   - 新回合   → SetupHealth → (后续 UpdateDisplayRoutine) → UpdateDisplay
# 健康值在调用 UpdateDisplay 前已修改完毕，直接比较即可。
func UpdateDisplay(chain: ModLoaderHookChain):
	chain.execute_next([])
	
	var ref = chain.reference_object
	var b = _eb()
	if not b or not ref or not ref.roundManager:
		return
	
	var p = ref.roundManager.health_player
	var d = ref.roundManager.health_opponent
	var inf = ref.roundManager.endless
	
	# 首次调用：初始化基线，不发射事件
	if not _initialized:
		_prev_player_hp = p
		_prev_dealer_hp = d
		_initialized = true
		return
	
	var p_changed = (p != _prev_player_hp)
	var d_changed = (d != _prev_dealer_hp)
	
	# 双变启发式：双方同时变化 = 新回合血量显示，静默更新基线
	if p_changed and d_changed:
		_prev_player_hp = p
		_prev_dealer_hp = d
		return
	
	# 单方变化 = 真实血量变化事件（射击/香烟/药品）
	if p_changed:
		b.emit(b.EVT_HEALTH_CHANGED, {"value": p, "type": "Player", "infinite": inf})
	if d_changed:
		b.emit(b.EVT_HEALTH_CHANGED, {"value": d, "type": "Dealer", "infinite": inf})
	
	_prev_player_hp = p
	_prev_dealer_hp = d
