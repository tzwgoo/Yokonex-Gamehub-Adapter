package cn.cinian.gamehub;

import cn.cinian.YOKONEXLink;
import com.google.gson.Gson;
import com.google.gson.JsonObject;
import com.google.gson.reflect.TypeToken;

import java.lang.reflect.Type;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.time.Instant;
import java.util.Collections;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.atomic.AtomicBoolean;

public final class GameHubClient {
    private static final URI CONFIG_URI = URI.create("http://127.0.0.1:43002/v1/game-integrations/minecraft/adapter-config");
    private static final URI EVENT_URI = URI.create("http://127.0.0.1:43002/v1/events");
    private static final Duration CONFIG_TTL = Duration.ofSeconds(5);
    private static final Gson GSON = new Gson();
    private static final Type MAPPING_TYPE = new TypeToken<Map<String, String>>() { }.getType();
    private static final HttpClient HTTP = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(2))
            .build();
    private static final AtomicBoolean REFRESHING = new AtomicBoolean();
    private static final String SESSION_ID = "minecraft-" + UUID.randomUUID();

    private static volatile AdapterConfig config = AdapterConfig.disabled();
    private static volatile Instant nextRefresh = Instant.EPOCH;

    private GameHubClient() {
    }

    public static void start() {
        refreshConfig();
    }

    public static void publish(String eventName, String matchValue, JsonObject data) {
        if (Instant.now().isAfter(nextRefresh)) {
            refreshConfig();
        }

        AdapterConfig snapshot = config;
        String eventKey = "minecraft." + eventName;
        String commandId = snapshot.mappings().get(eventKey);
        if (!snapshot.enabled() || commandId == null || commandId.isBlank()) {
            return;
        }

        JsonObject payload = new JsonObject();
        payload.addProperty("source", "minecraft");
        payload.addProperty("eventKey", eventKey);
        payload.addProperty("commandId", commandId);
        payload.addProperty("occurredAt", Instant.now().toString());
        payload.addProperty("sessionId", SESSION_ID);
        payload.addProperty("eventId", UUID.randomUUID().toString());
        if (matchValue != null && !matchValue.isBlank()) {
            payload.addProperty("matchValue", matchValue);
        }
        payload.add("data", data == null ? new JsonObject() : data);

        // 异步上报，GameHub 未运行时也不能卡住游戏主线程。
        HttpRequest request = HttpRequest.newBuilder(EVENT_URI)
                .timeout(Duration.ofSeconds(2))
                .header("Content-Type", "application/json")
                .POST(HttpRequest.BodyPublishers.ofString(GSON.toJson(payload)))
                .build();
        HTTP.sendAsync(request, HttpResponse.BodyHandlers.discarding())
                .thenAccept(response -> {
                    if (response.statusCode() >= 400) {
                        YOKONEXLink.LOGGER.warn("GameHub 拒绝 Minecraft 事件 {}，状态码 {}", eventKey, response.statusCode());
                    }
                })
                .exceptionally(error -> {
                    YOKONEXLink.LOGGER.debug("GameHub 暂不可用，已跳过事件 {}：{}", eventKey, error.getMessage());
                    return null;
                });
    }

    private static void refreshConfig() {
        if (!REFRESHING.compareAndSet(false, true)) {
            return;
        }
        nextRefresh = Instant.now().plus(CONFIG_TTL);
        HttpRequest request = HttpRequest.newBuilder(CONFIG_URI)
                .timeout(Duration.ofSeconds(2))
                .GET()
                .build();
        HTTP.sendAsync(request, HttpResponse.BodyHandlers.ofString())
                .thenAccept(response -> {
                    if (response.statusCode() != 200) {
                        config = AdapterConfig.disabled();
                        return;
                    }
                    JsonObject root = GSON.fromJson(response.body(), JsonObject.class);
                    boolean enabled = root.has("enabled") && root.get("enabled").getAsBoolean();
                    Map<String, String> mappings = root.has("mappings")
                            ? GSON.fromJson(root.get("mappings"), MAPPING_TYPE)
                            : Collections.emptyMap();
                    config = new AdapterConfig(enabled, mappings == null ? Collections.emptyMap() : Map.copyOf(mappings));
                })
                .exceptionally(error -> {
                    config = AdapterConfig.disabled();
                    return null;
                })
                .whenComplete((ignored, error) -> REFRESHING.set(false));
    }

    private record AdapterConfig(boolean enabled, Map<String, String> mappings) {
        private static AdapterConfig disabled() {
            return new AdapterConfig(false, Collections.emptyMap());
        }
    }
}
