extends Object

# beer_used → shell_eject_manager_ext.gd (BeerEjection_dealer)
# handcuffs_applied → handcuff_manager_ext.gd (AttachHandCuffs, 方案 B)

func PickupItemFromTable(chain: ModLoaderHookChain, itemName: String):
	await chain.execute_next_async([itemName])
