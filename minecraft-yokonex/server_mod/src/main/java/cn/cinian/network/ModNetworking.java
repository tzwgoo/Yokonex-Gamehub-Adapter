package cn.cinian.network;

import cn.cinian.YOKONEXLink;
import cn.cinian.gamehub.GameHubClient;
import com.google.gson.JsonObject;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.server.level.ServerPlayer;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.fml.DistExecutor;
import net.minecraftforge.network.NetworkDirection;
import net.minecraftforge.network.NetworkRegistry;
import net.minecraftforge.network.simple.SimpleChannel;

public final class ModNetworking {
    private static final String PROTOCOL_VERSION = "1";
    private static final SimpleChannel CHANNEL = NetworkRegistry.newSimpleChannel(
            new ResourceLocation(YOKONEXLink.MOD_ID, "gamehub_events"),
            () -> PROTOCOL_VERSION,
            PROTOCOL_VERSION::equals,
            PROTOCOL_VERSION::equals
    );

    private ModNetworking() {
    }

    public static void register() {
        CHANNEL.messageBuilder(GameHubEventPacket.class, 0, NetworkDirection.PLAY_TO_CLIENT)
                .encoder(GameHubEventPacket::encode)
                .decoder(GameHubEventPacket::decode)
                .consumerMainThread((packet, contextSupplier) ->
                        DistExecutor.unsafeRunWhenOn(Dist.CLIENT, () -> () ->
                                GameHubClient.publish(packet.eventName(), packet.matchValue(), packet.data())))
                .add();
    }

    public static void sendToPlayer(
            ServerPlayer player,
            String eventName,
            String matchValue,
            JsonObject data
    ) {
        // Forge 通道只把事件发送给触发事件的玩家，避免串到其他人的设备。
        GameHubEventPacket packet = new GameHubEventPacket(
                eventName,
                matchValue == null ? "" : matchValue,
                data.toString()
        );
        CHANNEL.sendTo(packet, player.connection.connection, NetworkDirection.PLAY_TO_CLIENT);
    }
}
