package cn.cinian;

import cn.cinian.event.GameEventHandler;
import cn.cinian.gamehub.GameHubClient;
import cn.cinian.network.ModNetworking;
import net.minecraftforge.api.distmarker.Dist;
import net.minecraftforge.fml.DistExecutor;
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
        // 服务端只采集和定向转发事件，玩家客户端再交给本机 GameHub。
        ModNetworking.register();
        DistExecutor.unsafeRunWhenOn(Dist.CLIENT, () -> GameHubClient::start);
        GameEventHandler.register();
        LOGGER.info("Yokonex Minecraft GameHub 联动已加载");
    }
}
