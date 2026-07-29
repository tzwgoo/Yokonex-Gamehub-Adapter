extends Node

const LOG_NAME = "buckshot-gamehub"
const SOURCE = "buckshot_roulette"
const CONFIG_REFRESH_SECONDS = 5.0
const MAX_QUEUED_EVENTS = 64

const EVENTS = {
	"game_launch": {"event_key": "buckshot.game_launch", "command_id": "buckshot-game-launch"},
	"game_start": {"event_key": "buckshot.game_start", "command_id": "buckshot-game-start"},
	"round_start": {"event_key": "buckshot.round_start", "command_id": "buckshot-round-start"},
	"round_won": {"event_key": "buckshot.round_won", "command_id": "buckshot-round-won"},
	"health_changed": {"event_key": "buckshot.health_changed", "command_id": "buckshot-health-changed"},
	"no_regeneration": {"event_key": "buckshot.no_regeneration", "command_id": "buckshot-no-regeneration"},
	"shoot": {"event_key": "buckshot.shoot", "command_id": "buckshot-shoot"},
	"ammo_updated": {"event_key": "buckshot.ammo_updated", "command_id": "buckshot-ammo-updated"},
	"item_placed": {"event_key": "buckshot.item_placed", "command_id": "buckshot-item-placed"},
	"item_used": {"event_key": "buckshot.item_used", "command_id": "buckshot-item-used"},
	"item_stolen": {"event_key": "buckshot.item_stolen", "command_id": "buckshot-item-stolen"},
	"items_cleared": {"event_key": "buckshot.items_cleared", "command_id": "buckshot-items-cleared"},
	"item_box_opened": {"event_key": "buckshot.item_box_opened", "command_id": "buckshot-item-box-opened"},
	"item_box_closed": {"event_key": "buckshot.item_box_closed", "command_id": "buckshot-item-box-closed"},
	"magnifying_glass_used": {"event_key": "buckshot.magnifying_glass_used", "command_id": "buckshot-magnifying-glass-used"},
	"beer_used": {"event_key": "buckshot.beer_used", "command_id": "buckshot-beer-used"},
	"burner_phone_used": {"event_key": "buckshot.burner_phone_used", "command_id": "buckshot-burner-phone-used"},
	"medicine_used": {"event_key": "buckshot.medicine_used", "command_id": "buckshot-medicine-used"},
	"handcuffs_applied": {"event_key": "buckshot.handcuffs_applied", "command_id": "buckshot-handcuffs-applied"},
	"handcuffs_removed": {"event_key": "buckshot.handcuffs_removed", "command_id": "buckshot-handcuffs-removed"},
	"game_won": {"event_key": "buckshot.game_won", "command_id": "buckshot-game-won"},
	"game_lost": {"event_key": "buckshot.game_lost", "command_id": "buckshot-game-lost"},
	"score_updated": {"event_key": "buckshot.score_updated", "command_id": "buckshot-score-updated"},
	"double_or_nothing": {"event_key": "buckshot.double_or_nothing", "command_id": "buckshot-double-or-nothing"},
	"game_close": {"event_key": "buckshot.game_close", "command_id": "buckshot-game-close"},
}

var _gateway_url = "http://127.0.0.1:43002"
var _event_request: HTTPRequest
var _config_request: HTTPRequest
var _queue: Array[Dictionary] = []
var _request_active = false
var _integration_enabled = true
var _config_loaded = false
var _command_ids: Dictionary = {}
var _enabled_events: Dictionary = {}
var _session_id = ""
var _sequence = 0

func _ready() -> void:
	_load_endpoint()
	_session_id = str(Time.get_unix_time_from_system()).replace(".", "-")
	_event_request = HTTPRequest.new()
	_event_request.timeout = 3.0
	_event_request.request_completed.connect(_on_event_request_completed)
	add_child(_event_request)
	_config_request = HTTPRequest.new()
	_config_request.timeout = 3.0
	_config_request.request_completed.connect(_on_config_request_completed)
	add_child(_config_request)

	var bus = Engine.get_meta("br_api_bus", null)
	if not bus:
		ModLoaderLog.error("BR-API event bus is unavailable", LOG_NAME)
		return
	for event_name in EVENTS:
		bus.on(event_name, Callable(self, "_on_game_event").bind(event_name))

	var timer = Timer.new()
	timer.wait_time = CONFIG_REFRESH_SECONDS
	timer.autostart = true
	timer.timeout.connect(_refresh_config)
	add_child(timer)
	_refresh_config()
	ModLoaderLog.info("GameHub bridge ready: " + _gateway_url, LOG_NAME)

func _load_endpoint() -> void:
	var config_path = get_script().resource_path.get_base_dir().get_base_dir() + "/gamehub.json"
	if not FileAccess.file_exists(config_path):
		return
	var file = FileAccess.open(config_path, FileAccess.READ)
	if not file:
		return
	var parsed = JSON.parse_string(file.get_as_text())
	if parsed is Dictionary:
		var configured = str(parsed.get("gateway_url", "")).strip_edges().trim_suffix("/")
		if configured.begins_with("http://127.0.0.1:") or configured.begins_with("http://localhost:"):
			_gateway_url = configured

func _refresh_config() -> void:
	if _config_request.get_http_client_status() != HTTPClient.STATUS_DISCONNECTED:
		return
	var url = _gateway_url + "/v1/game-integrations/" + SOURCE + "/adapter-config"
	_config_request.request(url, PackedStringArray(["Accept: application/json"]))

func _on_config_request_completed(_result: int, response_code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	if response_code != 200:
		return
	var parsed = JSON.parse_string(body.get_string_from_utf8())
	if not (parsed is Dictionary):
		return
	_config_loaded = true
	_integration_enabled = bool(parsed.get("enabled", true))
	var mappings = parsed.get("mappings", {})
	if mappings is Dictionary:
		_command_ids = mappings
		_enabled_events.clear()
		for event_key in mappings:
			_enabled_events[str(event_key)] = true

func _on_game_event(data, event_name: String) -> void:
	var definition: Dictionary = EVENTS.get(event_name, {})
	if definition.is_empty() or not _integration_enabled:
		return
	var event_key = str(definition.event_key)
	if _config_loaded and not _enabled_events.has(event_key):
		return
	_sequence += 1
	var payload = {
		"source": SOURCE,
		"eventKey": event_key,
		"commandId": str(_command_ids.get(event_key, definition.command_id)),
		"occurredAt": Time.get_datetime_string_from_system(true, false) + "Z",
		"eventId": _session_id + "-" + str(_sequence),
		"sessionId": _session_id,
		"data": data.duplicate(true) if data is Dictionary else {},
	}
	if _queue.size() >= MAX_QUEUED_EVENTS:
		_queue.pop_front()
	_queue.push_back(payload)
	_send_next()

func _send_next() -> void:
	if _request_active or _queue.is_empty():
		return
	_request_active = true
	var payload = _queue.pop_front()
	var error = _event_request.request(
		_gateway_url + "/v1/events",
		PackedStringArray(["Content-Type: application/json", "Accept: application/json"]),
		HTTPClient.METHOD_POST,
		JSON.stringify(payload)
	)
	if error != OK:
		_request_active = false
		call_deferred("_send_next")

func _on_event_request_completed(_result: int, _response_code: int, _headers: PackedStringArray, _body: PackedByteArray) -> void:
	_request_active = false
	_send_next()
