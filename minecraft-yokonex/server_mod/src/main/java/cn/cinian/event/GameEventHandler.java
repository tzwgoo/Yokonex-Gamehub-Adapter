package cn.cinian.event;

import cn.cinian.YOKONEXLink;
import cn.cinian.network.ModNetworking;
import com.google.gson.JsonObject;
import net.minecraft.core.BlockPos;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.world.entity.monster.Enemy;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.ItemStack;
import net.minecraftforge.common.MinecraftForge;
import net.minecraftforge.event.ServerChatEvent;
import net.minecraftforge.event.entity.living.LivingDeathEvent;
import net.minecraftforge.event.entity.living.LivingHurtEvent;
import net.minecraftforge.event.entity.player.PlayerEvent;
import net.minecraftforge.event.entity.player.PlayerInteractEvent;
import net.minecraftforge.event.level.BlockEvent;
import net.minecraftforge.eventbus.api.SubscribeEvent;

public final class GameEventHandler {
    public static void register() {
        MinecraftForge.EVENT_BUS.register(new GameEventHandler());
    }

    @SubscribeEvent
    public void onPlayerLogin(PlayerEvent.PlayerLoggedInEvent event) {
        if (event.getEntity() instanceof ServerPlayer player) {
            publishPlayer("player_join", player, null, new JsonObject());
        }
    }

    @SubscribeEvent
    public void onPlayerLogout(PlayerEvent.PlayerLoggedOutEvent event) {
        if (event.getEntity() instanceof ServerPlayer player) {
            publishPlayer("player_leave", player, null, new JsonObject());
        }
    }

    @SubscribeEvent
    public void onPlayerChat(ServerChatEvent event) {
        JsonObject data = new JsonObject();
        data.addProperty("message", event.getRawText());
        publishPlayer("player_chat", event.getPlayer(), event.getRawText(), data);
    }

    @SubscribeEvent
    public void onBlockBreak(BlockEvent.BreakEvent event) {
        if (event.getPlayer() instanceof ServerPlayer player) {
            JsonObject data = positionData(event.getPos());
            String block = event.getState().getBlock().getName().getString();
            data.addProperty("block", block);
            publishPlayer("block_break", player, block, data);
        }
    }

    @SubscribeEvent
    public void onBlockPlace(BlockEvent.EntityPlaceEvent event) {
        if (event.getEntity() instanceof ServerPlayer player) {
            JsonObject data = positionData(event.getPos());
            String block = event.getPlacedBlock().getBlock().getName().getString();
            data.addProperty("block", block);
            publishPlayer("block_place", player, block, data);
        }
    }

    @SubscribeEvent
    public void onBlockAttack(PlayerInteractEvent.LeftClickBlock event) {
        if (!event.getLevel().isClientSide()) {
            Player player = event.getEntity();
            JsonObject data = positionData(event.getPos());
            String block = event.getLevel().getBlockState(event.getPos()).getBlock().getName().getString();
            data.addProperty("block", block);
            publishPlayer("block_attack", player, block, data);
        }
    }

    @SubscribeEvent
    public void onPlayerDeath(LivingDeathEvent event) {
        if (event.getEntity() instanceof ServerPlayer player) {
            JsonObject data = new JsonObject();
            String reason = event.getSource().getLocalizedDeathMessage(player).getString();
            data.addProperty("reason", reason);
            publishPlayer("player_death", player, reason, data);
        }
    }

    @SubscribeEvent
    public void onPlayerHurt(LivingHurtEvent event) {
        if (event.getEntity() instanceof ServerPlayer player) {
            JsonObject data = new JsonObject();
            data.addProperty("damage", event.getAmount());
            data.addProperty("damageSource", event.getSource().getMsgId());
            publishPlayer("player_damage", player, event.getSource().getMsgId(), data);
        }
    }

    @SubscribeEvent
    public void onEntityKilled(LivingDeathEvent event) {
        if (event.getSource().getEntity() instanceof ServerPlayer player) {
            String entity = event.getEntity().getType().getDescription().getString();
            JsonObject data = new JsonObject();
            data.addProperty("entity", entity);
            data.addProperty("hostile", event.getEntity() instanceof Enemy);
            publishPlayer("entity_killed", player, entity, data);
        }
    }

    @SubscribeEvent
    public void onItemUse(PlayerInteractEvent.RightClickItem event) {
        if (!event.getLevel().isClientSide()) {
            ItemStack stack = event.getItemStack();
            if (!stack.isEmpty()) {
                JsonObject data = new JsonObject();
                String item = stack.getHoverName().getString();
                data.addProperty("item", item);
                data.addProperty("count", stack.getCount());
                publishPlayer("item_use", event.getEntity(), item, data);
            }
        }
    }

    private static void publishPlayer(String eventName, Player player, String matchValue, JsonObject data) {
        if (!(player instanceof ServerPlayer serverPlayer)) {
            return;
        }
        // 服务端按玩家定向发送，事件只会进入该玩家电脑上的 GameHub。
        data.addProperty("playerName", player.getName().getString());
        data.addProperty("playerId", player.getUUID().toString());
        ModNetworking.sendToPlayer(serverPlayer, eventName, matchValue, data);
        YOKONEXLink.LOGGER.debug("已转发 Minecraft 事件 {}，玩家 {}", eventName, player.getName().getString());
    }

    private static JsonObject positionData(BlockPos pos) {
        JsonObject data = new JsonObject();
        data.addProperty("x", pos.getX());
        data.addProperty("y", pos.getY());
        data.addProperty("z", pos.getZ());
        return data;
    }
}
