extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

func UseMedicine(chain: ModLoaderHookChain):
	await chain.execute_next_async([])
	var b = _eb()
	if not b:
		return
	var ref = chain.reference_object
	var apply = ref.counter.overriding_medicine_adding if ref.counter.overriding_medicine else false
	b.emit(b.EVT_MEDICINE_USED, {"apply": apply})
