using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace Elin_AutoExplore
{
    public class IgnoreList 
    {
        private readonly ConfigEntry<string> gatheringExclusionConfigList;
        private readonly ConfigEntry<string> miningExclusionConfigList;
        private HashSet<string> gatheringExclusionList;
        private HashSet<string> miningExclusionList;

        public IgnoreList(ConfigEntry<string> gatheringExclusionList, ConfigEntry<string> miningExclusionList)
        {
            this.gatheringExclusionConfigList = gatheringExclusionList;
            this.miningExclusionConfigList = miningExclusionList;
            this.gatheringExclusionList = new HashSet<string>(this.gatheringExclusionConfigList.Value.TrimStart(',').Split(','));
            this.miningExclusionList = new HashSet<string>(this.miningExclusionConfigList.Value.TrimStart(',').Split(','));
        }

        public bool IsIgnoredFromGathering(string name)
        {

            return this.gatheringExclusionList.Contains(name);
        }

        public bool IsIgnoredFromMining(string name)
        {
            return this.miningExclusionList.Contains(name);
        }

        public void AddToGatheringIgnoreList(string name)
        {
            this.gatheringExclusionList.Add(name);
            this.gatheringExclusionConfigList.Value = string.Join(",", this.gatheringExclusionList);
        }

        public void AddToMiningIgnoreList(string name)
        {
            this.miningExclusionList.Add(name);
            this.miningExclusionConfigList.Value = string.Join(",", this.miningExclusionList);
        }

        public void RemoveFromGatheringIgnoreList(string name)
        {
            this.gatheringExclusionList.Remove(name);
            this.gatheringExclusionConfigList.Value = string.Join(",", this.gatheringExclusionList);
        }

        public void RemoveFromMiningIgnoreList(string name)
        {
            this.miningExclusionList.Remove(name);
            this.miningExclusionConfigList.Value = string.Join(",", this.miningExclusionList);
        }


    }
}
