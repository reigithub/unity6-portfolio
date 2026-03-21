using Fusion.Addons.KCC;

namespace Game.Shared.Network.Survivor
{
    public interface ISurvivorPlayerMovementHandler
    {
        /// <summary>
        /// Fusion tick ごとに呼ばれる。入力→移動変換とアイテム収集のみ。
        /// スタミナ/HP/ステートはすべて SurvivorFusionPlayer が [Networked] で管理。
        /// </summary>
        void ProcessTick(SurvivorPlayerNetworkInput input, float deltaTime);

        /// <summary>
        /// SurvivorFusionPlayer.Spawned から呼ばれ、入力収集と移動ハンドラをバインドする。
        /// </summary>
        void BindFusionPlayer(SurvivorFusionPlayer fusionPlayer);

        /// <summary>
        /// Render フレームごとに呼ばれる。現在の入力を KCC に設定してレンダー予測を可能にする。
        /// </summary>
        void ProcessRenderInput(KCC kcc);
    }
}
