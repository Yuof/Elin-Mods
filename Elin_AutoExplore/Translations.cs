namespace Elin_AutoExplore;

public static class Translations
{
    public const string HarvestingMode = "Harvesting mode";
    public const string MiningMode = "Mining mode";
    public const string HarvestingAndMiningMode = "Harvesting and mining mode";
    public const string ExploringMode = "Exploring mode";

    public static string GetTranslation(string id)
    {
        var lang = EClass.core.config.lang;

        return lang switch
        {
            "JP" => id switch
            {
                AutoExploreConfigUi.Name => "オートエクスプローラー設定",
                nameof(AutoExplorerConfig.HandleFighting) => "オートエクスプローラーは戦闘を処理するべきですか？",
                nameof(AutoExplorerConfig.HandleHarvestables) => "オートエクスプローラーは収穫物を処理するべきですか？",
                nameof(AutoExplorerConfig.HandleMineables) => "オートエクスプローラーは鉱石を処理するべきですか？",
                nameof(AutoExplorerConfig.HandleTraps) => "オートエクスプローラーは罠を処理するべきですか？",
                nameof(AutoExplorerConfig.HandleShrines) => "オートエクスプローラーは神殿を処理するべきですか？",
                nameof(AutoExplorerConfig.UseMeditation) => "オートエクスプローラーは瞑想を使用するべきですか？",
                nameof(AutoExplorerConfig.HandleHunger) => "オートエクスプローラーは食事を摂るべきですか？",
                nameof (AutoExplorerConfig.MinMP) => "瞑想を開始する最小MP",
                nameof(AutoExplorerConfig.MinHP) => "瞑想を開始する最小HP",
                HarvestingMode => "収穫モード",
                MiningMode => "鉱業モード",
                HarvestingAndMiningMode => "収穫と鉱業モード",
                ExploringMode => "探索モード",
                _ => "error",
            },
            "CN" => id switch
            {
                AutoExploreConfigUi.Name => "自动探索设置",
                nameof(AutoExplorerConfig.HandleFighting) => "自动探索是否应处理战斗？",
                nameof(AutoExplorerConfig.HandleHarvestables) => "自动探索是否应处理可收获物？",
                nameof(AutoExplorerConfig.HandleMineables) => "自动探索是否应处理可挖掘物？",
                nameof(AutoExplorerConfig.HandleTraps) => "自动探索是否应处理陷阱？",
                nameof(AutoExplorerConfig.HandleShrines) => "自动探索是否应处理神殿？",
                nameof(AutoExplorerConfig.UseMeditation) => "自动探索是否应使用冥想？",
                nameof(AutoExplorerConfig.HandleHunger) => "自动探索是否应吃食物？",
                nameof(AutoExplorerConfig.MinMP) => "开始冥想的最低MP",
                nameof(AutoExplorerConfig.MinHP) => "开始冥想的最低HP",
                HarvestingMode => "收获模式",
                MiningMode => "采矿模式",
                HarvestingAndMiningMode => "收获和采矿模式",
                ExploringMode => "探索模式",
                _ => "error",
            },
            "ZHTW" => id switch
            {
                AutoExploreConfigUi.Name => "自動探索設置",
                nameof(AutoExplorerConfig.HandleFighting) => "自動探索是否應處理戰鬥？",
                nameof(AutoExplorerConfig.HandleHarvestables) => "自動探索是否應處理可收穫物？",
                nameof(AutoExplorerConfig.HandleMineables) => "自動探索是否應處理可挖掘物？",
                nameof(AutoExplorerConfig.HandleTraps) => "自動探索是否應處理陷阱？",
                nameof(AutoExplorerConfig.HandleShrines) => "自動探索是否應處理神殿？",
                nameof(AutoExplorerConfig.UseMeditation) => "自動探索是否應使用冥想？",
                nameof(AutoExplorerConfig.HandleHunger) => "自動探索是否應吃食物？",
                nameof(AutoExplorerConfig.MinMP) => "開始冥想的最低MP",
                nameof(AutoExplorerConfig.MinHP) => "開始冥想的最低HP",
                HarvestingMode => "收穫模式",
                MiningMode => "採礦模式",
                HarvestingAndMiningMode => "收穫和採礦模式",
                ExploringMode => "探索模式",
                _ => "error",
            },
            _ => id switch
            {
                AutoExploreConfigUi.Name => "AutoExplore Settings",
                nameof(AutoExplorerConfig.HandleFighting) => "Should AutoExplore handle fighting?",
                nameof(AutoExplorerConfig.HandleHarvestables) => "Should AutoExplore handle harvestables?",
                nameof(AutoExplorerConfig.HandleMineables) => "Should AutoExplore handle mineables?",
                nameof(AutoExplorerConfig.HandleTraps) => "Should AutoExplore handle traps?",
                nameof(AutoExplorerConfig.HandleShrines) => "Should AutoExplore handle shrines?",
                nameof(AutoExplorerConfig.UseMeditation) => "Should AutoExplore use meditation?",
                nameof(AutoExplorerConfig.HandleHunger) => "Should AutoExplore eat food?",
                nameof(AutoExplorerConfig.MinMP) => "Minimum MP to start meditation",
                nameof(AutoExplorerConfig.MinHP) => "Minimum HP to start meditation",
                HarvestingMode => "Harvesting mode",
                MiningMode => "Mining mode",
                HarvestingAndMiningMode => "Harvesting and mining mode",
                ExploringMode => "Exploring mode",
                _ => "error",
            },
        };
    }
}
