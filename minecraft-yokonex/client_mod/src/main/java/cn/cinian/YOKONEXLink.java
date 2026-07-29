package cn.cinian;

import cn.cinian.event.GameEventHandler;
import cn.cinian.gamehub.GameHubClient;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.event.lifecycle.FMLCommonSetupEvent;
import net.minecraftforge.fml.javafmlmod.FMLJavaModLoadingContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

@Mod(YOKONEXLink.MOD_ID)
public final class YOKONEXLink {
    public static final String MOD_ID = "yokonex_link";
    public static final Logger LOGGER = LoggerFactory.getLogger(MOD_ID);

    public YOKONEXLink() {
        FMLJavaModLoadingContext.get().getModEventBus().addListener(this::setup);
    }

    private void setup(FMLCommonSetupEvent event) {
        // Mod 只注册事件采集器，设备连接和 IM 登录全部交给 GameHub。
        GameHubClient.start();
        GameEventHandler.register();
        LOGGER.info("Yokonex Minecraft GameHub 联动已加载");
    }
}
