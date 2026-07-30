package cn.cinian.gamehub;

import cn.cinian.YOKONEXLink;
import com.google.gson.Gson;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.reflect.TypeToken;

import java.lang.reflect.Type;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.WebSocket;
import java.time.Duration;
import java.time.Instant;
import java.util.Collections;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;

public final class GameHubClient {
    private static final URI EVENT_URI = URI.create("ws://127.0.0.1:43002/v1/events");
    private static final Duration CONFIG_TTL = Duration.ofSeconds(5);
    private static final Gson GSON = new Gson();
    private static final Type MAPPING_TYPE = new TypeToken<Map<String, String>>() { }.getType();
    private static final HttpClient HTTP = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(2))
            .build();
    private static final ScheduledExecutorService RECONNECT_EXECUTOR =
            Executors.newSingleThreadScheduledExecutor(task -> {
                Thread thread = new Thread(task, "yokonex-gamehub-websocket");
                thread.setDaemon(true);
                return thread;
            });
    private static final AtomicReference<WebSocket> SOCKET = new AtomicReference<>();
    private static final AtomicBoolean CONNECTING = new AtomicBoolean();
    private static final AtomicBoolean RECONNECT_SCHEDULED = new AtomicBoolean();
    private static final Object SEND_LOCK = new Object();
    private static final String SESSION_ID = "minecraft-" + UUID.randomUUID();

    private static volatile AdapterConfig config = AdapterConfig.disabled();
    private static volatile Instant nextRefresh = Instant.EPOCH;
    private static CompletableFuture<Void> sendTail = CompletableFuture.completedFuture(null);

    private GameHubClient() {
    }

    public static void start() {
        connect();
    }

    public static void publish(String eventName, String matchValue, JsonObject data) {
        WebSocket socket = SOCKET.get();
        if (socket == null) {
            connect();
            return;
        }
        if (Instant.now().isAfter(nextRefresh)) {
            requestConfig(socket);
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

        // 所有消息串行写入同一条 WebSocket，避免游戏事件并发时发生帧冲突。
        send(socket, GSON.toJson(payload));
    }

    private static void connect() {
        if (SOCKET.get() != null || !CONNECTING.compareAndSet(false, true)) {
            return;
        }
        HTTP.newWebSocketBuilder()
                .connectTimeout(Duration.ofSeconds(2))
                .buildAsync(EVENT_URI, new GameHubListener())
                .whenComplete((ignored, error) -> {
                    CONNECTING.set(false);
                    if (error != null) {
                        config = AdapterConfig.disabled();
                        scheduleReconnect();
                    }
                });
    }

    private static void requestConfig(WebSocket socket) {
        nextRefresh = Instant.now().plus(CONFIG_TTL);
        JsonObject request = new JsonObject();
        request.addProperty("type", "getAdapterConfig");
        request.addProperty("source", "minecraft");
        send(socket, GSON.toJson(request));
    }

    private static void send(WebSocket socket, String message) {
        synchronized (SEND_LOCK) {
            sendTail = sendTail
                    .handle((ignored, error) -> null)
                    .thenCompose(ignored -> {
                        if (SOCKET.get() != socket) {
                            return CompletableFuture.<Void>completedFuture(null);
                        }
                        return socket.sendText(message, true).thenApply(sent -> (Void) null);
                    })
                    .whenComplete((ignored, error) -> {
                        if (error != null) {
                            disconnect(socket, error);
                        }
                    });
        }
    }

    private static void disconnect(WebSocket socket, Throwable error) {
        if (!SOCKET.compareAndSet(socket, null)) {
            return;
        }
        config = AdapterConfig.disabled();
        nextRefresh = Instant.EPOCH;
        socket.abort();
        if (error != null) {
            YOKONEXLink.LOGGER.debug("GameHub WebSocket 已断开：{}", error.getMessage());
        }
        scheduleReconnect();
    }

    private static void scheduleReconnect() {
        if (!RECONNECT_SCHEDULED.compareAndSet(false, true)) {
            return;
        }
        RECONNECT_EXECUTOR.schedule(() -> {
            RECONNECT_SCHEDULED.set(false);
            connect();
        }, 2, TimeUnit.SECONDS);
    }

    private static void handleMessage(String message) {
        try {
            JsonObject root = JsonParser.parseString(message).getAsJsonObject();
            String type = root.has("type") ? root.get("type").getAsString() : "";
            if ("adapterConfig".equals(type) && root.has("source")
                    && "minecraft".equals(root.get("source").getAsString())) {
                boolean enabled = root.has("enabled") && root.get("enabled").getAsBoolean();
                Map<String, String> mappings = root.has("mappings")
                        ? GSON.fromJson(root.get("mappings"), MAPPING_TYPE)
                        : Collections.emptyMap();
                config = new AdapterConfig(
                        enabled,
                        mappings == null ? Collections.emptyMap() : Map.copyOf(mappings)
                );
                nextRefresh = Instant.now().plus(CONFIG_TTL);
            } else if ("eventResult".equals(type)
                    && root.has("accepted")
                    && !root.get("accepted").getAsBoolean()) {
                String code = root.has("code") ? root.get("code").getAsString() : "unknown";
                String detail = root.has("message") ? root.get("message").getAsString() : "";
                YOKONEXLink.LOGGER.warn("GameHub 拒绝 Minecraft 事件：{} {}", code, detail);
            }
        } catch (RuntimeException error) {
            YOKONEXLink.LOGGER.debug("忽略无法解析的 GameHub WebSocket 消息：{}", error.getMessage());
        }
    }

    private static final class GameHubListener implements WebSocket.Listener {
        private final StringBuilder messageBuffer = new StringBuilder();

        @Override
        public void onOpen(WebSocket webSocket) {
            WebSocket previous = SOCKET.getAndSet(webSocket);
            if (previous != null && previous != webSocket) {
                previous.abort();
            }
            config = AdapterConfig.disabled();
            nextRefresh = Instant.EPOCH;
            webSocket.request(1);
            requestConfig(webSocket);
            YOKONEXLink.LOGGER.info("Minecraft 已连接 GameHub WebSocket");
        }

        @Override
        public CompletionStage<?> onText(WebSocket webSocket, CharSequence data, boolean last) {
            messageBuffer.append(data);
            if (last) {
                handleMessage(messageBuffer.toString());
                messageBuffer.setLength(0);
            }
            webSocket.request(1);
            return CompletableFuture.completedFuture(null);
        }

        @Override
        public CompletionStage<?> onClose(WebSocket webSocket, int statusCode, String reason) {
            disconnect(webSocket, null);
            return CompletableFuture.completedFuture(null);
        }

        @Override
        public void onError(WebSocket webSocket, Throwable error) {
            disconnect(webSocket, error);
        }
    }

    private record AdapterConfig(boolean enabled, Map<String, String> mappings) {
        private static AdapterConfig disabled() {
            return new AdapterConfig(false, Collections.emptyMap());
        }
    }
}
