package cn.cinian.network;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import net.minecraft.network.FriendlyByteBuf;

public record GameHubEventPacket(String eventName, String matchValue, String dataJson) {
    private static final int MAX_EVENT_NAME = 100;
    private static final int MAX_MATCH_VALUE = 500;
    private static final int MAX_DATA_JSON = 16_384;

    public static void encode(GameHubEventPacket packet, FriendlyByteBuf buffer) {
        buffer.writeUtf(packet.eventName(), MAX_EVENT_NAME);
        buffer.writeUtf(packet.matchValue(), MAX_MATCH_VALUE);
        buffer.writeUtf(packet.dataJson(), MAX_DATA_JSON);
    }

    public static GameHubEventPacket decode(FriendlyByteBuf buffer) {
        return new GameHubEventPacket(
                buffer.readUtf(MAX_EVENT_NAME),
                buffer.readUtf(MAX_MATCH_VALUE),
                buffer.readUtf(MAX_DATA_JSON)
        );
    }

    public JsonObject data() {
        return JsonParser.parseString(dataJson()).getAsJsonObject();
    }
}
