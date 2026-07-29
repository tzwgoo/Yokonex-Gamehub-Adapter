extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

# WA#12: 电话使用事件 — 精确获取游戏实际预言数据
# 原理：SendDialogue 中 ShowText_Forever 同步写入 dialogueUI.text，
# HideText 仅隐藏不清理文本，故 await 全函数完成后仍可读取。
# 通过反向匹配候选文本，精确定位游戏显示的 slot 和 type。
func SendDialogue(chain: ModLoaderHookChain):
	var ref = chain.reference_object
	var b = _eb()
	
	await chain.execute_next_async([])
	
	if not b or not is_instance_valid(ref) or not ref.sh or not ref.dia or not ref.dia.dialogueUI:
		return
	
	var seq = ref.sh.sequenceArray
	var text = ref.dia.dialogueUI.text
	
	# 仅一发子弹时显示 UNFORTUNATE（"真遗憾"），发射空预言
	if seq.size() <= 1 or text == "":
		b.emit(b.EVT_BURNER_PHONE_USED, {"slot": 0, "type": "none"})
		return
	
	# 遍历可能的槽位(1~6)，用 tr() 构造候选文本，与游戏实际显示文本精确匹配
	# 注意：原版 SendDialogue 对文本做了双重 tr() 调用: fulldia = tr(firstpart) + ... + tr(secondpart)
	# 其中 firstpart = tr("SEQUENCE{N}"), secondpart = tr("BLANKROUND") % ""
	# slot 使用 1-indexed 子弹编号（"第3发" → slot:3）
	for i in range(1, min(7, seq.size())):
		var firstpart = tr(tr("SEQUENCE" + str(i + 1)))
		var shell_type = seq[i]
		var secondpart_raw = tr("BLANKROUND") % "" if shell_type == "blank" else tr("LIVEROUND") % ""
		var secondpart = tr(secondpart_raw)
		var candidate = firstpart + "\n" + "... " + secondpart
		
		if text == candidate:
			b.emit(b.EVT_BURNER_PHONE_USED, {"slot": i + 1, "type": shell_type})
			return
