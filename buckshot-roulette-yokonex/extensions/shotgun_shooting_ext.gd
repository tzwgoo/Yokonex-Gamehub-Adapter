extends Object

func _eb():
	return Engine.get_meta("br_api_bus", null)

func Shoot(chain: ModLoaderHookChain, who: String):
	var ref = chain.reference_object
	# 在方法入口立即快照数据，确保拿到扣扳机前的状态
	var chamber = "" if ref.shellSpawner.sequenceArray.is_empty() else ref.shellSpawner.sequenceArray[0]
	var src = "Player"
	var tgt = "Dealer" if who != "self" else "Player"
	var b = _eb()
	
	if b:
		# 扣动扳机时机: 0.25(等待) + 0.5(举枪动画) + 2.0(瞄准) = 2.75s
		var tree = Engine.get_main_loop()
		tree.create_timer(2.75, false).timeout.connect(func():
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
