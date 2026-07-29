extends Object

const LOG_NAME = "game_api-br_api:event_bus"

# event registry
static var _cb = {}

static func on(evt: String, fn: Callable) -> void:
	if not _cb.has(evt): _cb[evt] = []
	_cb[evt].append(fn)

static func off(evt: String, fn: Callable) -> void:
	if _cb.has(evt): _cb[evt].erase(fn)

static func emit(evt: String, data := {}) -> void:
	if not _cb.has(evt): return
	for fn in _cb[evt].duplicate():
		if fn.is_valid(): fn.call(data)
		else: _cb[evt].erase(fn)

# events
const EVT_ROUND_START       = "round_start"
const EVT_SHOOT             = "shoot"
const EVT_ITEM_PLACED       = "item_placed"
const EVT_ITEM_STOLEN       = "item_stolen"
const EVT_HANDCUFFS_APPLIED = "handcuffs_applied"
const EVT_HANDCUFFS_REMOVED = "handcuffs_removed"
const EVT_GAME_START        = "game_start"
const EVT_HEALTH_CHANGED    = "health_changed"
const EVT_AMMO_UPDATED      = "ammo_updated"
const EVT_ITEM_USED         = "item_used"
const EVT_ITEMS_CLEARED     = "items_cleared"
const EVT_ITEM_BOX_OPENED   = "item_box_opened"
const EVT_ITEM_BOX_CLOSED   = "item_box_closed"
const EVT_ROUND_WON         = "round_won"
const EVT_GAME_WON          = "game_won"
const EVT_GAME_LOST         = "game_lost"
const EVT_NO_REGEN          = "no_regeneration"
const EVT_MEDICINE_USED     = "medicine_used"
const EVT_MAGNIFYING_GLASS_USED  = "magnifying_glass_used"
const EVT_BEER_USED         = "beer_used"
const EVT_SCORE_UPDATED     = "score_updated"
const EVT_DOUBLE_OR_NOTHING = "double_or_nothing"
const EVT_BURNER_PHONE_USED = "burner_phone_used"
const EVT_GAME_LAUNCH       = "game_launch"
const EVT_GAME_CLOSE        = "game_close"

# node helpers
static func _find(node: Node, suffix: String) -> Node:
	for c in node.get_children():
		var s = c.get_script()
		if s and s.resource_path.ends_with(suffix): return c
		var f = _find(c, suffix)
		if f: return f
	return null

static func round_mgr() -> Node:
	var t = Engine.get_main_loop()
	return _find(t.root, "RoundManager.gd") if t else null

static func item_mgr() -> Node:
	var t = Engine.get_main_loop()
	return _find(t.root, "ItemManager.gd") if t else null

static func health_ctr() -> Node:
	var t = Engine.get_main_loop()
	return _find(t.root, "HealthCounter.gd") if t else null

# immediate reads
static func inventory() -> Array:
	var im = item_mgr()
	if not im or not is_instance_valid(im.itemSpawnParent): return []
	var out = []
	for c in im.itemSpawnParent.get_children():
		if not is_instance_valid(c) or c.get_child_count() < 2: continue
		if not (c.get_child(0) is PickupIndicator): continue
		var inter = c.get_child(1)
		if inter.isPlayerSide:
			out.append({"slot": inter.itemGridIndex + 1, "type": inter.itemName})
	return out

static func player_hp() -> Dictionary:
	var rm = round_mgr()
	if not rm: return {}
	var regen = true
	if rm.playerData.currentBatchIndex == 2 and rm.health_player <= 2 and not rm.endless:
		regen = false
	return {"value": rm.health_player, "regeneration": regen}

static func round_info() -> Dictionary:
	var rm = round_mgr()
	if not rm: return {}
	var r = 1; var h = 3
	if rm.currentRound >= 0 and rm.currentRound < rm.roundArray.size():
		r = rm.currentRound + 1
		h = rm.roundArray[rm.currentRound].startingHealth
	return {"round": r, "health": h, "infinite": rm.endless}
