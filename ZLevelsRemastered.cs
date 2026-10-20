using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using Rust;
using Newtonsoft.Json.Converters;

namespace Oxide.Plugins
{
    [Info("ZLevels Remastered", "nivex", "3.3.1")]
    [Description("Lets players level up as they harvest resources, fish, and craft")]

    class ZLevelsRemastered : RustPlugin
    {
        [PluginReference]
        Plugin EventManager, CraftingController, ZoneManager, Economics, IQEconomic, ServerRewards, SkillTree;

        private StoredData data = new();
        private Dictionary<string, ItemDefinition> CraftItems;
        private CraftData _craftData = new();
        private StringBuilder _sb = new();
        private bool newSaveDetected;
        private bool bonusOn;
        private int MaxB = 10001;
        private int MinB = 10;
        private HashSet<ulong> uiMovePlayers = new();

        public enum Skills { ACQUIRE, CRAFTING, MINING, SKINNING, WOODCUTTING, FISHING, ALL }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum ClientPanels { None, Overall, Overlay, OverlayNonScaled, HudMenu, Hud, Contacts, Clans, Crafting, Inventory, Map, TechTree, Under, UnderNonScaled }

        private Skills[] AllSkills = new Skills[] { Skills.ACQUIRE, Skills.CRAFTING, Skills.MINING, Skills.SKINNING, Skills.WOODCUTTING, Skills.FISHING };

        private class CraftData
        {
            public Dictionary<string, CraftInfo> CraftList = new();
            public CraftData() { }
        }

        private class CraftInfo
        {
            public int MaxBulkCraft;
            public int MinBulkCraft;
            public string shortName;
            public bool Enabled;
            public CraftInfo() { }
        }

        private class StoredData
        {
            public Hash<ulong, PlayerInfo> PlayerInfo = new();
            [JsonProperty(ItemConverterType = typeof(Vector2Converter))]
            public Dictionary<ulong, Vector2> UiPositions = new();
            public StoredData() { }
        }

        private class PlayerInfo : IEquatable<PlayerInfo>
        {
            private double _acquireLevel = 1.0;
            private double _acquirePoints = 10.0;
            private double _craftingLevel = 1.0;
            private double _craftingPoints = 10.0;
            private double _miningLevel = 1.0;
            private double _miningPoints = 10.0;
            private double _skinningLevel = 1.0;
            private double _skinningPoints = 10.0;
            private double _woodcuttingLevel = 1.0;
            private double _woodcuttingPoints = 10.0;
            private double _fishingLevel = 1.0;
            private double _fishingPoints = 10.0;
            private double _lastDeath = 0.0;
            private double _xpMultiplier = 100.0;

            private double RoundValue(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

            [JsonProperty(PropertyName = "AL")]
            public double ACQUIRE_LEVEL
            {
                get => RoundValue(_acquireLevel);
                set => _acquireLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "AP")]
            public double ACQUIRE_POINTS
            {
                get => RoundValue(_acquirePoints);
                set => _acquirePoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "CL")]
            public double CRAFTING_LEVEL
            {
                get => RoundValue(_craftingLevel);
                set => _craftingLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "CP")]
            public double CRAFTING_POINTS
            {
                get => RoundValue(_craftingPoints);
                set => _craftingPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "ML")]
            public double MINING_LEVEL
            {
                get => RoundValue(_miningLevel);
                set => _miningLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "MP")]
            public double MINING_POINTS
            {
                get => RoundValue(_miningPoints);
                set => _miningPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "SL")]
            public double SKINNING_LEVEL
            {
                get => RoundValue(_skinningLevel);
                set => _skinningLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "SP")]
            public double SKINNING_POINTS
            {
                get => RoundValue(_skinningPoints);
                set => _skinningPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "WCL")]
            public double WOODCUTTING_LEVEL
            {
                get => RoundValue(_woodcuttingLevel);
                set => _woodcuttingLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "WCP")]
            public double WOODCUTTING_POINTS
            {
                get => RoundValue(_woodcuttingPoints);
                set => _woodcuttingPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "FL")]
            public double FISHING_LEVEL
            {
                get => RoundValue(_fishingLevel);
                set => _fishingLevel = RoundValue(value);
            }

            [JsonProperty(PropertyName = "FP")]
            public double FISHING_POINTS
            {
                get => RoundValue(_fishingPoints);
                set => _fishingPoints = RoundValue(value);
            }

            [JsonProperty(PropertyName = "LD")]
            public double LAST_DEATH
            {
                get => RoundValue(_lastDeath);
                set => _lastDeath = RoundValue(value);
            }

            [JsonProperty(PropertyName = "XPM")]
            public double XP_MULTIPLIER
            {
                get => RoundValue(_xpMultiplier);
                set => _xpMultiplier = RoundValue(value);
            }

            [JsonProperty(PropertyName = "CUI")]
            public bool CUI = true;

            [JsonProperty(PropertyName = "CUIMOVE")]
            public bool CUIMOVE;

            [JsonProperty(PropertyName = "ONOFF")]
            public bool ENABLED = true;

            public PlayerInfo() { }

            public bool Equals(PlayerInfo other)
            {
                if (other == null)
                    return false;

                return ACQUIRE_LEVEL == other.ACQUIRE_LEVEL &&
                       ACQUIRE_POINTS == other.ACQUIRE_POINTS &&
                       CRAFTING_LEVEL == other.CRAFTING_LEVEL &&
                       CRAFTING_POINTS == other.CRAFTING_POINTS &&
                       MINING_LEVEL == other.MINING_LEVEL &&
                       MINING_POINTS == other.MINING_POINTS &&
                       SKINNING_LEVEL == other.SKINNING_LEVEL &&
                       SKINNING_POINTS == other.SKINNING_POINTS &&
                       WOODCUTTING_LEVEL == other.WOODCUTTING_LEVEL &&
                       WOODCUTTING_POINTS == other.WOODCUTTING_POINTS &&
                       FISHING_LEVEL == other.FISHING_LEVEL &&
                       FISHING_POINTS == other.FISHING_POINTS &&
                       XP_MULTIPLIER == other.XP_MULTIPLIER &&
                       CUI == other.CUI &&
                       CUIMOVE == other.CUIMOVE &&
                       ENABLED == other.ENABLED;
            }

            public override bool Equals(object obj)
            {
                return obj is PlayerInfo other && Equals(other);
            }

            public override int GetHashCode()
            {
                HashCode hash = new();
                hash.Add(ACQUIRE_LEVEL);
                hash.Add(ACQUIRE_POINTS);
                hash.Add(CRAFTING_LEVEL);
                hash.Add(CRAFTING_POINTS);
                hash.Add(MINING_LEVEL);
                hash.Add(MINING_POINTS);
                hash.Add(SKINNING_LEVEL);
                hash.Add(SKINNING_POINTS);
                hash.Add(WOODCUTTING_LEVEL);
                hash.Add(WOODCUTTING_POINTS);
                hash.Add(FISHING_LEVEL);
                hash.Add(FISHING_POINTS);
                hash.Add(XP_MULTIPLIER);
                hash.Add(CUI);
                hash.Add(CUIMOVE);
                hash.Add(ENABLED);
                return hash.ToHashCode();
            }

            public static bool operator ==(PlayerInfo left, PlayerInfo right)
            {
                if (ReferenceEquals(left, right))
                    return true;

                if (left is null)
                    return false;

                return left.Equals(right);
            }

            public static bool operator !=(PlayerInfo left, PlayerInfo right)
            {
                return !(left == right);
            }

            internal bool IsDefault(PlayerInfo other) => Equals(other);
        }

        #region Main

        private void Init()
        {
            Unsubscribe();
            RegisterPermissions();

            int index = 0;

            foreach (Skills skill in AllSkills)
            {
                if (IsSkillEnabled(skill))
                {
                    skillIndex.Add(skill, ++index);
                }
            }
        }

        private void Unload()
        {
            Clean();
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyGUI(player);
            }
            if (TOD_Sky.Instance?.Components?.Time != null)
            {
                TOD_Sky.Instance.Components.Time.OnSunrise -= OnTimeSunrise;
                TOD_Sky.Instance.Components.Time.OnSunset -= OnTimeSunset;
            }
        }

        protected new void Puts(string format, params object[] args)
        {
            if (string.IsNullOrWhiteSpace(format)) return;
            string message = Localize(format, null, args);
            if (string.IsNullOrWhiteSpace(message)) return;
            Interface.Oxide.LogInfo("[{0}] {1}", nameof(ZLevelsRemastered), message);
        }

        private void OnNewSave(string strFilename)
        {
            if (config.generic.wipeDataOnNewSave)
            {
                Puts("New save detected...");
                newSaveDetected = true;
            }
        }

        private void OnServerInitialized()
        {
            LoadData();
            if (config.nightbonus.enableNightBonus && TOD_Sky.Instance.IsNight)
            {
                pointsPerHitCurrent = config.nightbonus.pointsPerHitAtNight;
                resourceMultipliersCurrent = config.nightbonus.resourceMultipliersAtNight;
                pointsPerHitPowerToolCurrent = config.nightbonus.pointsPerPowerToolAtNight;
                bonusOn = true;
            }
            else
            {
                pointsPerHitCurrent = config.settings.pointsPerHit;
                resourceMultipliersCurrent = config.settings.resourceMultipliers;
                pointsPerHitPowerToolCurrent = config.settings.pointsPerPowerTool;
            }

            foreach (var player in BasePlayer.allPlayerList)
            {
                var pi = GetPlayerInfo(player);
                if (pi != null && player.IsConnected) CreateGUI(player, pi);
            }

            Puts("Stats can be reset by > zl.reset <");
            Subscribe();
            SaveData();
            RegisterCommands();
        }

        private void OnPlayerConnected(BasePlayer player) => OnPlayerSleepEnded(player);

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (!ReferenceEquals(player, null)) uiMovePlayers.Remove(player.userID);
            if (player != null) GetPlayerInfo(player);
        }

        private void OnEntityKill(BasePlayer player) => OnPlayerSleep(player);

        private void OnPlayerRespawned(BasePlayer player) => OnPlayerSleepEnded(player);

        private void OnPlayerSleepEnded(BasePlayer player) => CreateGUI(player, GetPlayerInfo(player));

        private void OnPlayerSleep(BasePlayer player) => DestroyGUI(player);

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || !IsValid(player) || isZoneExcluded(player))
                return;

            DestroyGUI(player);

            if (!hasRights(player.UserIDString))
                return;

            var pi = GetPlayerInfo(player);
            if (!pi.ENABLED || permission.UserHasPermission(player.UserIDString, config.generic.permissionNameXP))
                return;

            string L(string key, params object[] args) => Localize(key, player.UserIDString, args);

            bool isSuicide = info.damageTypes.Has(DamageType.Suicide) && (player.IsAdmin || player.IsDeveloper || !player.CanSuicide());

            if (!config.generic.penaltyOnSuicide && isSuicide) return;
            if (!config.generic.penaltyOnDeath && !isSuicide) return;
            if (EventManager?.Call("IsEventPlayer", player) != null) return;
            if (Interface.CallHook("CanBePenalized", player) != null) return;

            _sb.Clear();

            foreach (Skills skill in AllSkills)
            {
                if (IsSkillEnabled(skill))
                {
                    var penalty = GetPenalty(player, pi, skill, isSuicide);

                    if (penalty > 0)
                    {
                        _sb.AppendLine(L("* -{0} {1} XP.", penalty, L(skill + "Skill")));
                        removePoints(pi, skill, penalty);
                    }
                }
            }

            if (_sb.Length > 0)
            {
                Message(player, "PenaltyText", _sb.ToString());
            }

            pi.LAST_DEATH = ToEpochTime(DateTime.UtcNow);
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item == null || dispenser == null || !hasRights(player))
                return;

            Skills? skill = dispenser.gatherType switch
            {
                ResourceDispenser.GatherType.Tree => Skills.WOODCUTTING,
                ResourceDispenser.GatherType.Flesh => Skills.SKINNING,
                ResourceDispenser.GatherType.Ore => Skills.MINING,
                _ => null
            };

            if (skill == null || !IsSkillEnabled(skill.Value))
                return;

            var pi = GetPlayerInfo(player);
            if (!pi.ENABLED)
                return;

            Item activeItem = player.GetActiveItem();

            if (permission.UserHasPermission(player.UserIDString, config.generic.BlockWeaponsGather) && activeItem?.info.category == ItemCategory.Weapon)
                return;

            if (!config.functions.ShouldAllowGather(item.info.shortname, out bool grantXPOnly))
                return;

            int prevAmount = item.amount;
            bool isPowerTool = false;

            if (skill != Skills.SKINNING && activeItem != null)
            {
                HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;

                if (heldEntity is Jackhammer || heldEntity is Chainsaw)
                {
                    if (skill == Skills.WOODCUTTING && !CanUseChainsaw(player))
                    {
                        return;
                    }

                    if (skill == Skills.MINING && !CanUseJackhammer(player))
                    {
                        return;
                    }

                    if (!config.functions.gibs && dispenser.GetComponent<ServerGib>() != null)
                    {
                        return;
                    }

                    isPowerTool = true;
                }
            }

            if (!config.functions.gibs && skill == Skills.MINING && dispenser.GetComponent<ServerGib>() != null)
            {
                return;
            }

            int amount = levelHandler(pi, player, item.amount, skill.Value, dispenser.baseEntity, isPowerTool);

            if (!grantXPOnly)
            {
                item.amount = amount;
            }

            if (item.amount != prevAmount)
            {
                Interface.CallHook("OnZLevelDispenserGather", dispenser, player, item, prevAmount, item.amount, isPowerTool);
            }
        }

        private bool CanUseJackhammer(BasePlayer player) => permission.UserHasPermission(player.UserIDString, config.generic.AllowJackhammerGather);

        private bool CanUseChainsaw(BasePlayer player) => permission.UserHasPermission(player.UserIDString, config.generic.AllowChainsawGather);

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item) => OnDispenserGather(dispenser, player, item);

        private object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || collectible.itemList.IsNullOrEmpty() || !hasRights(player))
                return null;

            var pi = GetPlayerInfo(player);
            if (!pi.ENABLED)
                return null;

            if (config.functions.enabledCollectibleEntity.TryGetValue(collectible.ShortPrefabName, out var enabled) && !enabled)
                return null;

            for (int i = 0; i < collectible.itemList.Length; i++)
            {
                ItemAmount itemAmount = collectible.itemList[i];
                if (itemAmount?.itemDef?.shortname == null || !TryGetCollectibleSkill(itemAmount.itemDef.shortname, player, out Skills skill))
                    continue;

                int prevAmount = (int)itemAmount.amount;
                itemAmount.amount = levelHandler(pi, player, prevAmount, skill, collectible);
                if (Interface.CallHook("OnZLevelCollectiblePickup", itemAmount, player, collectible, prevAmount, itemAmount.amount) != null)
                {
                    return true;
                }
            }

            return null;
        }

        private void OnCollectiblePickedup(CollectibleEntity collectible, BasePlayer player, Item item)
        {
            if (collectible == null || item?.info?.shortname == null || !hasRights(player))
                return;

            var pi = GetPlayerInfo(player);
            if (!pi.ENABLED)
                return;

            if (config.functions.enabledCollectibleEntity.TryGetValue(collectible.ShortPrefabName, out var enabled) && !enabled)
                return;

            if (!TryGetCollectibleSkill(item.info.shortname, player, out Skills skill))
                return;

            int prevAmount = item.amount;
            item.amount = levelHandler(pi, player, item.amount, skill, collectible);
            Interface.CallHook("OnZLevelCollectiblePickedup", item, player, collectible, prevAmount, item.amount);
        }

        private bool TryGetCollectibleSkill(string shortname, BasePlayer player, out Skills skill)
        {
            switch (shortname)
            {
                case "black.berry" or "blue.berry" or "bone.fragments" or "clone.seed" or "clone.orchid" or "clone.rose" or "clone.sunflower" or "cloth" or "corn" or "diesel_barrel" or "green.berry" or "mushroom" or "orchid" or "potato" or "pumpkin" or "red.berry" or "rose" or "seed.black.berry" or "seed.blue.berry" or "seed.corn" or "seed.green.berry" or "seed.hemp" or "seed.orchid" or "seed.potato" or "seed.pumpkin" or "seed.red.berry" or "seed.rose" or "seed.sunflower" or "seed.wheat" or "seed.white.berry" or "seed.yellow.berry" or "sunflower" or "wheat" or "white.berry" or "yellow.berry":
                    skill = IsSkillEnabled(Skills.ACQUIRE) ? Skills.ACQUIRE : Skills.SKINNING;
                    break;
                case "hq.metal.ore" or "metal.ore" or "sulfur.ore" or "stones":
                    skill = config.settings.MiningStonesOre ? Skills.MINING : Skills.ACQUIRE;
                    break;
                case "wood":
                    skill = config.settings.AcquireWood ? Skills.ACQUIRE : Skills.WOODCUTTING;
                    break;
                default:
                    if (IsSkillEnabled(Skills.ACQUIRE)) skill = Skills.ACQUIRE;
                    else if (IsSkillEnabled(Skills.SKINNING)) skill = Skills.SKINNING;
                    else
                    {
                        skill = default;
                        return false;
                    }

                    if (_warnings.Add(shortname))
                    {
                        Puts("WARNING: {0} picked up undefined item: {1}. Defaulting to {2} skill.", player, shortname, Localize(skill + "Skill"));
                    }
                    break;
            }

            return IsSkillEnabled(skill);
        }

        private HashSet<string> _warnings = new();

        private void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            if (item?.info == null || rod == null || !hasRights(player) || !IsSkillEnabled(Skills.FISHING))
                return;

            var pi = GetPlayerInfo(player);
            if (!pi.ENABLED)
                return;

            double level = getLevel(pi, Skills.FISHING);
            if (!TryGainXP(pi, player, Skills.FISHING, rod, config.settings.fishingDetails.xpPerCatch))
                return;

            int prevAmount = item.amount;
            bool actualFish = TryGetFishable(item.info, out ItemModFishable fishable) && !fishable.IsJunk;
            double chance = GetFishingBonusChance(level);
            bool extraFish = actualFish && (chance >= 100.0 || chance > 0.0 && UnityEngine.Random.Range(0f, 100f) < chance);
            if (extraFish)
            {
                item.amount++;
            }

            Interface.CallHook("OnZLevelFishCatch", item, rod, player, prevAmount, item.amount, extraFish);
        }

        private void OnTimeSunset()
        {
            if (!config.nightbonus.enableNightBonus || bonusOn)
            {
                return;
            }

            bonusOn = true;
            pointsPerHitCurrent = config.nightbonus.pointsPerHitAtNight;
            resourceMultipliersCurrent = config.nightbonus.resourceMultipliersAtNight;
            pointsPerHitPowerToolCurrent = config.nightbonus.pointsPerPowerToolAtNight;
            if (config.nightbonus.broadcastEnabledBonus)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Message(player, "NightBonusOn");
            }
            if (config.nightbonus.logEnabledBonusConsole)
                Puts("Nightbonus points enabled");
        }

        private void OnTimeSunrise()
        {
            if (!config.nightbonus.enableNightBonus || !bonusOn)
            {
                return;
            }
            bonusOn = false;
            pointsPerHitCurrent = config.settings.pointsPerHit;
            resourceMultipliersCurrent = config.settings.resourceMultipliers;
            pointsPerHitPowerToolCurrent = config.settings.pointsPerPowerTool;
            if (config.nightbonus.broadcastEnabledBonus)
            {
                foreach (var player in BasePlayer.activePlayerList)
                    Message(player, "NightBonusOff");
            }
            if (config.nightbonus.logEnabledBonusConsole)
                Puts("Nightbonus points disabled");
        }

        private object OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (item == null || !hasRights(player))
                return true;

            var pi = GetPlayerInfo(player);
            if (!pi.ENABLED)
                return true;

            Skills skill;
            if (IsSkillEnabled(Skills.ACQUIRE))
                skill = Skills.ACQUIRE;
            else skill = Skills.SKINNING;

            var prevAmount = item.amount;

            item.amount = levelHandler(pi, player, item.amount, skill, plant);

            Interface.CallHook("OnZLevelGrowableGathered", plant, item, player, prevAmount, item.amount);

            return null;
        }


        private object OnItemCraft(ItemCraftTask task, BasePlayer crafter, Item fromTempBlueprint)
        {
            if (!hasRights(crafter)) return null;

            var pi = GetPlayerInfo(crafter);
            if (!pi.ENABLED) return null;

            var bypassCraftLevelRequirement = CanInstantCraftNoLevelRequirement(crafter);
            var craftingTime = task.blueprint.time;

            if (bypassCraftLevelRequirement)
            {
                craftingTime = 0;
            }
            else if (IsSkillEnabled(Skills.CRAFTING))
            {
                if (pi.CRAFTING_LEVEL > 1)
                    craftingTime -= task.blueprint.time * (float)((pi.CRAFTING_LEVEL * config.settings.craftingDetails.percent) / 100);
            }
            else return null;

            if (craftingTime <= 0)
            {
                craftingTime = 0;

                foreach (var craftInfo in _craftData.CraftList.Values)
                {
                    if (!craftInfo.Enabled || craftInfo.shortName != task.blueprint.targetItem.shortname)
                    {
                        continue;
                    }
                    if (task.amount >= craftInfo.MinBulkCraft && task.amount <= craftInfo.MaxBulkCraft && task.amount > 0)
                    {
                        var stacks = GetStacks(task.amount * task.blueprint.amountToCreate, task.blueprint.targetItem.stackable);
                        if (!HasPlace(crafter, stacks))
                        {
                            ReturnCraft(task, crafter);
                            return true;
                        }
                        if (bypassCraftLevelRequirement || CanInstantCraft(crafter))
                        {
                            if (!task.blueprint.name.Contains("(Clone)"))
                                task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);
                            task.blueprint.amountToCreate *= task.amount;
                            crafter.inventory.crafting.FinishCrafting(task);
                            task.cancelled = true;
                            return true;
                        }
                    }
                }
            }

            if (!task.blueprint.name.Contains("(Clone)"))
                task.blueprint = UnityEngine.Object.Instantiate(task.blueprint);

            task.blueprint.time = craftingTime;

            return null;
        }
        private List<int> GetStacks(int amount, int maxStack)
        {
            List<int> stacks = new();
            while (amount > maxStack && amount > 0)
            {
                amount -= maxStack;
                stacks.Add(maxStack);
            }
            if (amount > 0)
            {
                stacks.Add(amount);
            }
            return stacks;
        }
        private bool HasPlace(BasePlayer crafter, List<int> stacks)
        {
            if (!config.settings.craftingDetails.slots)
            {
                return true;
            }
            var capacity = crafter.inventory.containerMain.capacity + crafter.inventory.containerBelt.capacity;
            var taken = crafter.inventory.containerMain.itemList.Count + crafter.inventory.containerBelt.itemList.Count;
            var slots = capacity - taken;
            if (slots - stacks.Count < 0)
            {
                return false;
            }
            return slots > 0;
        }
        private bool CanInstantCraftNoLevelRequirement(BasePlayer crafter)
        {
            return config.settings.craftingDetails.noLevelRequirement && permission.UserHasPermission(crafter.UserIDString, config.settings.craftingDetails.Permission);
        }
        private bool CanInstantCraft(BasePlayer crafter)
        {
            if (config.settings.craftingDetails.usePermission)
            {
                return permission.UserHasPermission(crafter.UserIDString, config.settings.craftingDetails.Permission);
            }
            return true;
        }
        private void ReturnCraft(ItemCraftTask task, BasePlayer crafter)
        {
            task.cancelled = true;
            Message(crafter, "NoSlots");
            foreach (var item in task.takenItems)
            {
                if (item.amount > 0)
                    crafter.GiveItem(item);
            }
        }
        private object OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter craft)
        {
            if (task == null) return null;
            var crafter = craft?.owner;
            if (crafter == null || !hasRights(crafter.UserIDString)) return null;
            var bypassLevelRequirement = CanInstantCraftNoLevelRequirement(crafter);
            if (!bypassLevelRequirement && !IsSkillEnabled(Skills.CRAFTING)) return null;
            var pi = GetPlayerInfo(crafter);
            var xpPercentBefore = getExperiencePercent(pi, Skills.CRAFTING);
            if (task.blueprint == null)
            {
                Puts("There is problem obtaining task.blueprint on 'OnItemCraftFinished' hook! This is usually caused by some incompatable plugins.");
                return null;
            }
            var experienceGain = (task.blueprint.time + 0.99f) / config.settings.craftingDetails.time;
            if (experienceGain == 0)
                return null;

            double Level = pi.CRAFTING_LEVEL;
            double Points = pi.CRAFTING_POINTS;
            Points += experienceGain * config.settings.craftingDetails.xp;
            if (Points >= getLevelPoints(Level + 1))
            {
                var levelCap = config.settings.levelCaps.CRAFTING;
                var maxLevel = levelCap > 0 && Level + 1 > levelCap;
                if (!maxLevel)
                {
                    Level = getPointsLevel(Points, Skills.CRAFTING);
                    string format = $"<color={config.settings.colors.CRAFTING}>{Localize("LevelUpText", crafter.UserIDString)}</color>";
                    string message = string.Format(format, Localize("CRAFTINGSkill", crafter.UserIDString), Level, Points, getLevelPoints(Level + 1), pi.CRAFTING_LEVEL * Convert.ToDouble(config.settings.craftingDetails.percent));
                    Player.Message(crafter, message, string.IsNullOrEmpty(config.generic.pluginPrefix) ? string.Empty : config.generic.pluginPrefix, config.generic.steamIDIcon);
                    GiveReward(crafter, Skills.CRAFTING, Level);
                    if (config.generic.enableLevelupBroadcast)
                    {
                        foreach (var target in BasePlayer.activePlayerList)
                        {
                            if (target != null && target.userID != crafter.userID && hasRights(target.UserIDString) && GetPlayerInfo(target).ENABLED)
                            {
                                Message(target, "LevelUpTextBroadcast", crafter.displayName, Level, config.settings.colors.CRAFTING, Localize("CRAFTINGSkill", target.UserIDString));
                            }
                        }
                    }
                }
            }
            try
            {
                if (item.info.shortname != "lantern_a" && item.info.shortname != "lantern_b")
                    setPointsAndLevel(pi, Skills.CRAFTING, Points, Level);
            }
            catch { }

            try
            {
                var xpPercentAfter = getExperiencePercent(pi, Skills.CRAFTING);
                if (xpPercentAfter != xpPercentBefore)
                    GUIUpdateSkill(crafter, Skills.CRAFTING);
            }
            catch { }

            if (task.amount > 0) return null;
            if (task.blueprint != null && task.blueprint.name.Contains("(Clone)"))
            {
                var behaviours = task.blueprint.GetComponents<MonoBehaviour>();
                foreach (var behaviour in behaviours)
                {
                    if (behaviour.name.Contains("(Clone)")) UnityEngine.Object.Destroy(behaviour);
                }
            }
            return null;
        }

        #endregion Serverhooks

        #region Commands

        [HookMethod("SendHelpText"), ChatCommand("stathelp")]
        private void SendHelpText(BasePlayer player)
        {
            string L(string key) => Localize(key, player.UserIDString);
            _sb.Clear();
            _sb.AppendLine(L("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>"));
            _sb.AppendLine(L("/stats - Displays your stats."));
            _sb.AppendLine(L("/statsui - Enable/Disable stats UI. Use /statsui reset to restore its position."));
            _sb.AppendLine(L("/statsui move - Start/finish moving the UI, or click DONE to finish moving the UI. /statsui button - Show/hide the move button."));
            _sb.AppendLine(L("/statsonoff - Enable/Disable whole leveling."));
            _sb.AppendLine(L("/statinfo - Displays information about skills."));
            _sb.AppendLine(L("/stathelp - Displays the help."));
            //sb.AppendLine("/topskills - Display max levels reached so far.");
            if (_sb.Length > 0) SendReply(player, _sb.ToString());
        }

        private void PointsPerHitCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            string L(string key, params object[] args) => Localize(key, user.Id, args);

            if (args.Length < 2)
            {
                Reply(user, "Syntax: zl.pointsperhit skill number");
                Reply(user, "Possible skills are: WC, M, S, A, C, *(All skills)");
                _sb.Clear();
                _sb.Append(L("Current points per hit:"));
                foreach (var currSkill in AllSkills)
                {
                    if (currSkill == Skills.FISHING || !IsSkillEnabled(currSkill)) continue;
                    _sb.Append(L(" {0} > {1} |", L(currSkill + "Skill"), pointsPerHitCurrent.Get(currSkill)));
                }
                user.Reply(_sb.ToString().TrimEnd('|'));
                return;
            }
            if (!double.TryParse(args[1], out var points) || points < 1)
            {
                Reply(user, "Incorrect number. Must be greater than or equal to 1");
                return;
            }

            var skillName = args[0].ToUpper();
            Skills skill;
            if (skillName == "*") skill = Skills.ALL;
            else if (skillName.Equals("C")) skill = Skills.CRAFTING;
            else if (skillName.Equals("WC")) skill = Skills.WOODCUTTING;
            else if (skillName.Equals("M")) skill = Skills.MINING;
            else if (skillName.Equals("A")) skill = Skills.ACQUIRE;
            else if (skillName.Equals("S")) skill = Skills.SKINNING;
            else { Reply(user, "Incorrect skill. Possible skills are: WC, M, S, A, C, *(All skills)."); return; }

            if (skill == Skills.ALL)
            {
                foreach (var currSkill in AllSkills)
                {
                    if (currSkill == Skills.FISHING || !IsSkillEnabled(currSkill)) continue;
                    pointsPerHitCurrent.Set(currSkill, points);
                }
                _sb.Clear();
                _sb.Append(L("New points per hit:"));
                foreach (var currSkill in AllSkills)
                {
                    if (currSkill == Skills.FISHING || !IsSkillEnabled(currSkill)) continue;
                    _sb.Append(L(" {0} > {1} |", L(currSkill + "Skill"), pointsPerHitCurrent.Get(currSkill)));
                }
                user.Reply(_sb.ToString().TrimEnd('|'));
            }
            else
            {
                pointsPerHitCurrent.Set(skill, points);
                _sb.Clear();
                _sb.Append(L("New points per hit:"));
                foreach (var currSkill in AllSkills)
                {
                    if (currSkill == Skills.FISHING || !IsSkillEnabled(currSkill)) continue;
                    _sb.Append(L(" {0} > {1} |", L(currSkill + "Skill"), pointsPerHitCurrent.Get(currSkill)));
                }
                user.Reply(_sb.ToString().TrimEnd('|'));
            }
        }

        private void PlayerXpmCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;
            if (args.Length < 1)
            {
                Reply(user, "XPM USE 1");
                Reply(user, "XPM USE 2");
                return;
            }
            IPlayer target = covalence.Players.FindPlayer(args[0]);
            if (target == null)
            {
                Reply(user, "PLAYER NOT FOUND");
                return;
            }
            if (!data.PlayerInfo.TryGetValue(Convert.ToUInt64(target.Id), out var playerData))
            {
                Reply(user, "PlayerData is NULL!");
                return;
            }
            if (args.Length < 2)
            {
                var bonus1 = getXpMulti(playerData, target.Id);
                var baseMultiplier = playerData.XP_MULTIPLIER;
                if (baseMultiplier != bonus1)
                {
                    Reply(user, "Current XP multiplier for player '{0}' is {1}% with vip raising total to {2}%", target.Name, baseMultiplier, bonus1);
                }
                else Reply(user, "Current XP multiplier for player '{0}' is {1}%", target.Name, baseMultiplier);
                return;
            }
            double multiplier = -1;
            if (double.TryParse(args[1], out multiplier))
            {
                if (multiplier <= 0)
                {
                    Reply(user, "Incorrect number. Must be greater than 0");
                    return;
                }
            }
            playerData.XP_MULTIPLIER = multiplier;
            var bonus2 = getXpMulti(playerData, target.Id);
            if (multiplier != bonus2)
            {
                Reply(user, "New XP multiplier for player '{0}' is {1}% with vip raising total to {2}%", target.Name, multiplier, bonus2);
            }
            else Reply(user, "New XP multiplier for player '{0}' is {1}%", target.Name, bonus2);
        }

        private void InfoCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin)
            {
                return;
            }
            if (args.Length < 1)
            {
                if (!GetLookingAtPlayer(user))
                {
                    Reply(user, "INFO USE");
                }
                return;
            }
            IPlayer target = covalence.Players.FindPlayer(args[0]);
            if (target == null)
            {
                Reply(user, "PLAYER NOT FOUND");
                return;
            }
            if (!data.PlayerInfo.TryGetValue(Convert.ToUInt64(target.Id), out var playerData))
            {
                Reply(user, "PlayerData is NULL!");
                return;
            }
            PrintInfo(user, target.Id, target.Name, playerData);
        }

        private bool GetLookingAtPlayer(IPlayer user)
        {
            BasePlayer player = user.Object as BasePlayer;
            if (player == null) return false;
            BasePlayer target = RelationshipManager.GetLookingAtPlayer(player);
            if (target == null) return false;
            if (!data.PlayerInfo.TryGetValue(target.userID, out var playerData)) return false;
            PrintInfo(user, target.UserIDString, target.displayName, playerData);
            return true;
        }

        private void PrintInfo(IPlayer user, string targetId, string targetName, PlayerInfo playerData)
        {
            string L(string key) => Localize(key, user.Id);
            TextTable textTable = new();
            textTable.AddColumn(L("FieldInfo"));
            textTable.AddColumn(L("Level"));
            textTable.AddColumn(L("Points"));
            textTable.AddRow(L("ACQUIRESkill"), $"{playerData.ACQUIRE_LEVEL:#0.##}", $"{playerData.ACQUIRE_POINTS:#0.##}");
            textTable.AddRow(L("CRAFTINGSkill"), $"{playerData.CRAFTING_LEVEL:#0.##}", $"{playerData.CRAFTING_POINTS:#0.##}");
            textTable.AddRow(L("MININGSkill"), $"{playerData.MINING_LEVEL:#0.##}", $"{playerData.MINING_POINTS:#0.##}");
            textTable.AddRow(L("SKINNINGSkill"), $"{playerData.SKINNING_LEVEL:#0.##}", $"{playerData.SKINNING_POINTS:#0.##}");
            textTable.AddRow(L("WOODCUTTINGSkill"), $"{playerData.WOODCUTTING_LEVEL:#0.##}", $"{playerData.WOODCUTTING_POINTS:#0.##}");
            textTable.AddRow(L("FISHINGSkill"), $"{playerData.FISHING_LEVEL:#0.##}", $"{playerData.FISHING_POINTS:#0.##}");
            textTable.AddRow(L("XPM"), $"{getXpMulti(playerData, targetId):#0.##}%", string.Empty);
            user.Reply($"\n{L("STATS")}{targetName}\n{textTable}");
        }

        private void ResetCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;
            if (args.Length != 1 || args[0] != "true")
            {
                Reply(user, "RESET USE");
                return;
            }
            WipeData();
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CreateGUI(player, GetPlayerInfo(player));
            }
            Reply(user, "Userdata was successfully reset to zero");
            Puts("{0} ({1}) reset userdata to zero", user.Name, user.Id);
        }

        private void ZlvlCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            string L(string key) => Localize(key, user.Id);

            if (args.Length < 3)
            {
                _sb.Clear();
                _sb.AppendLine(L("Syntax: zl.lvl name|steamid skill [OPERATOR]NUMBER"));
                _sb.AppendLine(L("Example: zl.lvl Player WC /2 -- Player gets his WC level divided by 2."));
                _sb.AppendLine(L("Example: zl.lvl * * +3 -- Everyone currently playing in the server gets +3 for all skills."));
                _sb.AppendLine(L("Example: zl.lvl ** * /2 -- Everyone (including offline players) gets their level divided by 2."));
                _sb.AppendLine(L("Instead of names you can use wildcard(*): * - affects online players, ** - affects all players"));
                _sb.AppendLine(L("Possible operators: *(XP Modified %), +(Adds level), -(Removes level), /(Divides level)")).AppendLine();
                user.Reply(_sb.ToString());
                return;
            }
            var playerName = args[0];
            IPlayer target = covalence.Players.FindPlayer(playerName);

            if (playerName != "*" && playerName != "**" && target == null)
            {
                Reply(user, "PLAYER NOT FOUND");
                return;
            }
            if (playerName != "*" && playerName != "**" && !data.PlayerInfo.ContainsKey(Convert.ToUInt64(target.Id)))
            {
                Reply(user, "PlayerData is NULL!");
                return;
            }

            if (target != null || playerName == "*" || playerName == "**")
            {
                var playerMode = 0; // Exact player
                if (playerName == "*")
                    playerMode = 1; // Online players
                else if (playerName == "**")
                    playerMode = 2; // All players
                var skillName = args[1].ToUpper();
                Skills skill;
                if (skillName == "*") skill = Skills.ALL;
                else if (skillName.Equals("C")) skill = Skills.CRAFTING;
                else if (skillName.Equals("WC")) skill = Skills.WOODCUTTING;
                else if (skillName.Equals("M")) skill = Skills.MINING;
                else if (skillName.Equals("A")) skill = Skills.ACQUIRE;
                else if (skillName.Equals("S")) skill = Skills.SKINNING;
                else if (skillName.Equals("F")) skill = Skills.FISHING;
                else { Reply(user, "Incorrect skill. Possible skills are: WC, M, S, A, C, F, *(All skills)."); return; }
                var mode = 0; // 0 = SET, 1 = ADD, 2 = SUBTRACT, 3 = multiplier, 4 = divide
                int value;
                bool correct;
                if (args[2][0] == '+')
                {
                    mode = 1;
                    correct = int.TryParse(args[2].Replace("+", string.Empty), out value);
                }
                else if (args[2][0] == '-')
                {
                    mode = 2;
                    correct = int.TryParse(args[2].Replace("-", string.Empty), out value);
                }
                else if (args[2][0] == '*')
                {
                    mode = 3;
                    correct = int.TryParse(args[2].Replace("*", string.Empty), out value);
                }
                else if (args[2][0] == '/')
                {
                    mode = 4;
                    correct = int.TryParse(args[2].Replace("/", string.Empty), out value);
                }
                else
                {
                    correct = int.TryParse(args[2], out value);
                }
                if (correct)
                {
                    if (mode == 3) // Change XP Multiplier.
                    {
                        if (skill != Skills.ALL)
                        {
                            Reply(user, "XPMultiplier is changeable for all skills! Use * instead of {0}.", skill);
                            return;
                        }
                        if (playerMode == 1)
                        {
                            foreach (var currPlayer in BasePlayer.activePlayerList)
                                editMultiplierForPlayer(value, currPlayer.userID);
                        }
                        else if (playerMode == 2)
                            editMultiplierForPlayer(value);
                        else if (target != null)
                            editMultiplierForPlayer(value, Convert.ToUInt64(target.Id));

                        var whom = playerMode == 1 ? L("ALL ONLINE PLAYERS") : playerMode == 2 ? L("ALL PLAYERS") : target.Name;
                        Reply(user, "XP rates has changed to {0}% of normal XP for {1}", value, whom);
                        return;
                    }
                    if (playerMode == 1)
                    {
                        foreach (var connPlayer in covalence.Players.Connected)
                        {
                            adminModifyPlayerStats(user, skill, value, mode, connPlayer);
                        }
                    }
                    else if (playerMode == 2)
                    {
                        foreach (var other in covalence.Players.All)
                        {
                            if (data.PlayerInfo.ContainsKey(Convert.ToUInt64(other.Id)))
                            {
                                adminModifyPlayerStats(user, skill, value, mode, other);
                            }
                        }
                    }
                    else
                    {
                        adminModifyPlayerStats(user, skill, value, mode, target);
                    }
                }
            }
        }

        private void CheckCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            int count = 0;
            var pi = CreatePlayerInfo();

            foreach (var info in data.PlayerInfo)
            {
                if (info.Value.IsDefault(pi))
                {
                    count++;
                }
            }

            Reply(user, "{0} / {1} entries in datafile can be removed", count, data.PlayerInfo.Count);
        }

        private void CleanCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            int count = data.PlayerInfo.Count;
            int cleaned = Clean();

            Reply(user, "{0} / {1} entries in datafile have been removed", cleaned, count);
            SaveData();
        }

        private int Clean()
        {
            int count = 0;

            if (data == null)
            {
                return count;
            }

            var pi = CreatePlayerInfo();

            foreach (var info in data.PlayerInfo.ToList())
            {
                if (info.Value.IsDefault(pi))
                {
                    count++;
                    data.PlayerInfo.Remove(info.Key);
                }
            }

            return count;
        }

        private void PenaltyCommand(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            if (args.Length == 1 && args[0] == "status")
            {
                Reply(user, config.generic.penaltyOnDeath ? "Penalty is currently enabled." : "Penalty is currently disabled.");
                return;
            }

            if (args.Length == 0) config.generic.penaltyOnDeath = !config.generic.penaltyOnDeath;
            else config.generic.penaltyOnSuicide = !config.generic.penaltyOnSuicide;

            if (config.generic.penaltyOnDeath || config.generic.penaltyOnSuicide)
            {
                Subscribe(nameof(OnEntityDeath));
                Reply(user, "Penalty is now enabled.");
            }
            else
            {
                Unsubscribe(nameof(OnEntityDeath));
                Reply(user, "Penalty is now disabled.");
            }

            SaveConfig();
        }

        [ChatCommand("stats")]
        private void StatsCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player))
            {
                Message(player, "NoPermission");
                return;
            }
            string id = player.UserIDString;
            string L(string key, params object[] args) => Localize(key, id, args);
            _sb.Clear();
            _sb.AppendLine(L("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>"));
            foreach (Skills skill in AllSkills) _sb.Append(getStatPrint(player, skill));
            var pi = GetPlayerInfo(player.userID);
            var alive = ReadableTimeSpan(DateTime.UtcNow - ToDateTimeFromEpoch(pi.LAST_DEATH), id);
            _sb.AppendLine().AppendLine(L("Time alive: {0}", alive));
            _sb.AppendLine(L("XP rates for you are {0}%", getXpMulti(pi, id)));
            SendReply(player, _sb.ToString());
        }

        [ChatCommand("statinfo")]
        private void StatInfoCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player))
            {
                Message(player, "NoPermission");
                return;
            }

            _sb.Clear();
            var colors = config.settings.colors;
            var craftingDetails = config.settings.craftingDetails;
            var fishingDetails = config.settings.fishingDetails;
            var xpm = getXpMulti(GetPlayerInfo(player.userID), player.UserIDString) / 100f;
            var m = player.GetHeldEntity() is Jackhammer ? pointsPerHitPowerToolCurrent.Get(Skills.MINING) : pointsPerHitCurrent.Get(Skills.MINING);
            var wc = player.GetHeldEntity() is Chainsaw ? pointsPerHitPowerToolCurrent.Get(Skills.WOODCUTTING) : pointsPerHitCurrent.Get(Skills.WOODCUTTING);

            string L(string key, params object[] args) => Localize(key, player.UserIDString, args);

            _sb.AppendLine(L("<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>"));

            AppendLine(colors.ACQUIRE, pointsPerHitCurrent.Get(Skills.ACQUIRE) * xpm, Skills.ACQUIRE, player);
            AppendLine(colors.MINING, m * xpm, Skills.MINING, player);
            AppendLine(colors.SKINNING, pointsPerHitCurrent.Get(Skills.SKINNING) * xpm, Skills.SKINNING, player);
            AppendLine(colors.WOODCUTTING, wc * xpm, Skills.WOODCUTTING, player);

            _sb.AppendLine(L("<color={0}>{1}</color> {2}", colors.FISHING, L("FISHINGSkill"), !IsSkillEnabled(Skills.FISHING) ? L("(DISABLED)") : string.Empty));
            _sb.AppendLine(L("XP per catch: <color={0}>{1:#0.##}</color>", colors.FISHING, fishingDetails.xpPerCatch * xpm));
            _sb.AppendLine(L("Bonus: <color={0}>Each level after 1 adds a {1:#0.##}% chance to catch one extra fish, up to {2:#0.##}%.</color>", colors.FISHING, fishingDetails.extraFishChancePerLevel, fishingDetails.maxExtraFishChance));

            _sb.AppendLine(L("<color={0}>{1}</color> {2}", colors.CRAFTING, L("CRAFTINGSkill"), !IsSkillEnabled(Skills.CRAFTING) ? L("(DISABLED)") : string.Empty));
            _sb.AppendLine(L("XP gain: <color={0}>You get {1} XP per {2}s spent crafting.</color>", colors.SKINNING, craftingDetails.xp, craftingDetails.time));
            _sb.AppendLine(L("Bonus: <color={0}>Crafting time is decreased by {1}% per every level.</color>", colors.SKINNING, craftingDetails.percent));

            SendReply(player, _sb.ToString());
            _sb.Clear();
        }

        private void AppendLine(string color, double xp, Skills skill, BasePlayer player)
        {
            string L(string key, params object[] args) => Localize(key, player.UserIDString, args);

            var state = !IsSkillEnabled(skill) ? L("(DISABLED)") : string.Empty;
            var bonus = (getGathMult(2, skill) - 1) * 100;

            _sb.AppendLine(L("<color={0}>{1}</color> {2}", color, L(skill + "Skill"), state));
            _sb.AppendLine(L("XP per hit: <color={0}>{1}</color>", color, xp));
            _sb.AppendLine(L("Bonus materials per level: <color={0}>{1:#0.##} %</color>", color, bonus));
        }

        [ChatCommand("statsui")]
        private void StatsUICommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player)) return;
            var pi = GetPlayerInfo(player);

            if (args.Length > 0)
            {
                if (args[0].Equals("move", StringComparison.OrdinalIgnoreCase))
                {
                    ToggleMoveUi(player);
                    return;
                }
                if (args[0].Equals("button", StringComparison.OrdinalIgnoreCase))
                {
                    if (config.cui.MoveButtonPanel == ClientPanels.None)
                    {
                        Message(player, "The stats UI move button is disabled. Use /statsui move to move the UI.");
                        return;
                    }
                    pi.CUIMOVE = !pi.CUIMOVE;
                    CreateGUI(player, pi);
                    Message(player, pi.CUIMOVE ? "Stats UI move button hidden. Use /statsui move to move the UI." : "Stats UI move button shown.");
                    return;
                }
                if (args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
                {
                    data.UiPositions.Remove(player.userID);
                    if (pi.CUI && pi.ENABLED)
                    {
                        UpdateGUIPosition(player);
                    }
                    Message(player, "Stats UI position reset.");
                    return;
                }
                if (player.IsAdmin && args[0].Equals("setui", StringComparison.OrdinalIgnoreCase))
                {
                    var offsets = config.cui.cuiPositioning;
                    offsets.NormalizedAnchor = data.UiPositions.TryGetValue(player.userID, out Vector2 position) ? position : offsets.NormalizedAnchor;
                    data.UiPositions.Clear();
                    SaveConfig();
                    SaveData();
                    Message(player, "Default stats UI position saved and all player positions reset.");
                    foreach (var target in BasePlayer.activePlayerList)
                    {
                        if (!hasRights(target)) continue;
                        PlayerInfo targetInfo = GetPlayerInfo(target);
                        if (targetInfo.CUI && targetInfo.ENABLED && target.IsAlive() && !target.IsSleeping())
                        {
                            UpdateGUIPosition(target);
                        }
                    }
                    return;
                }
            }

            if (pi.CUI)
            {
                DestroyGUI(player);
                pi.CUI = false;
            }
            else
            {
                pi.CUI = true;
                CreateGUI(player, pi);
            }
        }

        private Dictionary<ItemDefinition, ItemModFishable> _fishable = new();
        [ConsoleCommand("zl.fishing.items")]
        private void FishingItemsCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player != null && !player.IsAdmin)
                return;

            string id = player?.UserIDString;
            using var catchables = Facepunch.Pool.Get<PooledList<(ItemDefinition def, ItemModFishable fishable)>>();
            foreach (ItemDefinition def in ItemManager.GetItemDefinitions())
            {
                if (TryGetFishable(def, out ItemModFishable fishable))
                {
                    catchables.Add((def, fishable));
                }
            }

            catchables.Sort((left, right) =>
            {
                int result = left.fishable.IsJunk.CompareTo(right.fishable.IsJunk);
                return result != 0 ? result : left.def.shortname.CompareTo(right.def.shortname);
            });

            string L(string key, params object[] args) => Localize(key, id, args);

            using var table = Facepunch.Pool.Get<TextTable>();
            table.AddColumns(L("shortname (itemid)"), L("bait"), L("depth"), L("water"), L("weight"), L("type"));

            foreach (var (def, fishable) in catchables)
            {
                table.AddRow(
                    $"{def.shortname} ({def.itemid})",
                    FormatRange(fishable.MinimumBaitLevel, fishable.MaximumBaitLevel, id),
                    FormatRange(fishable.MinimumWaterDepth, fishable.MaximumWaterDepth, id),
                    (int)fishable.RequiredTag == -1 ? L("any") : fishable.RequiredTag.ToString(),
                    fishable.Chance.ToString("0.###"),
                    L(fishable.IsJunk ? "junk" : "fish")
                );
            }

            arg.ReplyWith(L("catchables:\n{0}", table));
        }

        private bool TryGetFishable(ItemDefinition def, out ItemModFishable fishable)
        {
            if (_fishable.TryGetValue(def, out fishable)) return true;
            if (!def.TryGetComponent(out fishable) || !fishable.CanBeFished) return false;
            _fishable[def] = fishable;
            return true;
        }

        private string FormatRange(float min, float max, string id)
        {
            if (min <= 0f && max <= 0f) return Localize("any", id);
            if (max <= 0f) return $"{min:0.##}+";
            return $"{min:0.##}-{max:0.##}";
        }

        [ConsoleCommand("zl.ui.move")]
        private void CommandMoveUi(ConsoleSystem.Arg arg) => ToggleMoveUi(arg.Player());

        private void ToggleMoveUi(BasePlayer player)
        {
            if (!config.cui.IsEnabled || !hasRights(player)) return;
            PlayerInfo pi = GetPlayerInfo(player);
            if (!pi.ENABLED || !pi.CUI || !player.IsAlive() || player.IsSleeping() || !player.IsConnected) return;
            if (!uiMovePlayers.Add(player.userID)) uiMovePlayers.Remove(player.userID);
            CreateGUI(player, pi);
        }

        [ChatCommand("statsonoff")]
        private void StatsOnOffCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasRights(player)) return;
            var pi = GetPlayerInfo(player);
            if (pi.ENABLED)
            {
                DestroyGUI(player);
                pi.ENABLED = false;
                Message(player, "PluginPlayerOff");
            }
            else
            {
                pi.ENABLED = true;
                Message(player, "PluginPlayerOn");
                if (pi.CUI)
                    CreateGUI(player, pi);
            }
        }

        #endregion Commands

        #region Helpers

        private void Unsubscribe()
        {
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnCollectiblePickedup));
            Unsubscribe(nameof(OnCuiDraggableDrag));
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnFishCatch));
            Unsubscribe(nameof(OnGrowableGathered));
            Unsubscribe(nameof(OnItemCraft));
            Unsubscribe(nameof(OnItemCraftFinished));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnPlayerRespawned));
            Unsubscribe(nameof(OnPlayerSleep));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnTimeSunrise));
            Unsubscribe(nameof(OnTimeSunset));
            Unsubscribe(nameof(Unload));
        }

        private void RegisterPermissions()
        {
            foreach (var name in config.generic.vip.Keys)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                if (name.Contains("."))
                {
                    if (!permission.PermissionExists(name))
                    {
                        permission.RegisterPermission(name, this);
                    }
                }
                else
                {
                    if (!permission.GroupExists(name))
                    {
                        permission.CreateGroup(name, name, 0);
                    }
                }
            }

            permission.RegisterPermission(config.generic.nowipe, this);
            permission.RegisterPermission(config.generic.permissionName, this);
            permission.RegisterPermission(config.generic.permissionNameXP, this);
            permission.RegisterPermission(config.generic.AllowChainsawGather, this);
            permission.RegisterPermission(config.generic.AllowJackhammerGather, this);
            permission.RegisterPermission(config.generic.BlockWeaponsGather, this);
            permission.RegisterPermission(config.settings.craftingDetails.Permission, this);
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand("zl.pointsperhit", nameof(PointsPerHitCommand));
            AddCovalenceCommand("zl.playerxpm", nameof(PlayerXpmCommand));
            AddCovalenceCommand("zl.info", nameof(InfoCommand));
            AddCovalenceCommand("zl.reset", nameof(ResetCommand));
            AddCovalenceCommand("zl.lvl", nameof(ZlvlCommand));
            AddCovalenceCommand("check_zlevel_datafile", nameof(CheckCommand));
            AddCovalenceCommand("clean_zlevel_datafile", nameof(CleanCommand));
            AddCovalenceCommand("zl.toggledeathpenalty", nameof(PenaltyCommand));
        }

        private void Subscribe()
        {
            if (config.generic.penaltyOnDeath || config.generic.penaltyOnSuicide)
            {
                Subscribe(nameof(OnEntityDeath));
            }

            if (config.functions.enableCollectiblePickup)
            {
                Subscribe(config.functions.GetCollectiblePickupHook);
            }

            if (config.functions.enableCropGather)
            {
                Subscribe(nameof(OnGrowableGathered));
            }

            if (IsSkillEnabled(Skills.FISHING))
            {
                Subscribe(nameof(OnFishCatch));
            }

            if (config.functions.enableDispenserGather)
            {
                Subscribe(nameof(OnDispenserBonus));
                Subscribe(nameof(OnDispenserGather));
            }

            if (IsCraftingEnabled())
            {
                Subscribe(nameof(OnItemCraft));
                Subscribe(nameof(OnItemCraftFinished));
            }

            if (config.nightbonus.enableNightBonus)
            {
                Plugin plugin = plugins.Find("TimeOfDay");
                if (plugin != null && plugin.IsLoaded)
                {
                    Subscribe(nameof(OnTimeSunrise));
                    Subscribe(nameof(OnTimeSunset));
                }
                else if (TOD_Sky.Instance?.Components?.Time != null)
                {
                    TOD_Sky.Instance.Components.Time.OnSunrise += OnTimeSunrise;
                    TOD_Sky.Instance.Components.Time.OnSunset += OnTimeSunset;
                }
            }

            if (config.cui.IsEnabled)
            {
                Subscribe(nameof(OnCuiDraggableDrag));
            }

            Subscribe(nameof(OnPlayerConnected));
            Subscribe(nameof(OnPlayerDisconnected));
            Subscribe(nameof(OnPlayerRespawned));
            Subscribe(nameof(OnPlayerSleep));
            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(Unload));

            timer.Repeat(300f, 0, SaveData);
        }

        private void CheckCollectible()
        {
            var collectList = Resources.FindObjectsOfTypeAll<CollectibleEntity>().Select(c => c.ShortPrefabName).Distinct().ToList();
            if (collectList == null || collectList.Count == 0)
            {
                return;
            }

            if (config.functions.enabledCollectibleEntity == null)
            {
                config.functions.enabledCollectibleEntity = new();
            }

            bool updated = false;
            foreach (var collect in collectList)
            {
                if (!config.functions.enabledCollectibleEntity.ContainsKey(collect))
                {
                    config.functions.enabledCollectibleEntity.Add(collect, true);
                    updated = true;
                }
            }

            if (updated)
            {
                SaveConfig();
            }
        }

        private void SaveData()
        {
            if (data != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, data);
            }
        }

        private void SaveDetails()
        {
            if (_craftData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject("ZLevelsCraftDetails", _craftData);
            }
        }

        private void LoadData()
        {
            try
            {
                _craftData = Interface.Oxide.DataFileSystem.ReadObject<CraftData>("ZLevelsCraftDetails");
            }
            catch (Exception ex)
            {
                Puts("CraftData threw an exception!");
                Puts(ex.ToString());
            }

            if (_craftData == null)
            {
                Puts("Crafting data is null and has been reset!");
                _craftData = new();
            }

            if (_craftData.CraftList == null)
            {
                Puts("Crafting list is null and has been reset!");
                _craftData.CraftList = new();
            }

            if (_craftData.CraftList.Count == 0)
            {
                GenerateItems(true);
            }

            CheckCollectible();

            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch (Exception ex)
            {
                Puts("StoredData threw an exception!");
                Puts(ex.ToString());
            }

            if (data == null || data.PlayerInfo == null)
            {
                Puts("Data is null, resetting to defaults!");
                data = new();
            }
            else
            {
                data.UiPositions ??= new();
            }

            if (newSaveDetected)
            {
                Puts("Wiping data... new save detected");
                WipeData();
            }
        }

        private void WipeData()
        {
            if (string.IsNullOrEmpty(config.generic.nowipe))
            {
                Dictionary<ulong, Vector2> uiPositions = data?.UiPositions ?? new();
                data = new();
                data.UiPositions = uiPositions;
                return;
            }
            foreach (var pair in data.PlayerInfo.ToList())
            {
                if (permission.UserHasPermission(pair.Key.ToString(), config.generic.nowipe))
                {
                    continue;
                }
                data.PlayerInfo.Remove(pair.Key);
            }
        }

        private bool hasRights(BasePlayer player) => IsValid(player) && hasRights(player.UserIDString);

        private bool hasRights(string UserIDString) => !config.generic.enablePermission || permission.UserHasPermission(UserIDString, config.generic.permissionName);

        private void editMultiplierForPlayer(double multiplier, ulong userID = ulong.MinValue)
        {
            if (userID == ulong.MinValue)
            {
                foreach (var p in data.PlayerInfo.ToList())
                    data.PlayerInfo[p.Key].XP_MULTIPLIER = multiplier;
                return;
            }
            if (data.PlayerInfo.TryGetValue(userID, out var playerData))
                playerData.XP_MULTIPLIER = multiplier;
        }

        private void adminModifyPlayerStats(IPlayer user, Skills skill, double level, int mode, IPlayer target)
        {
            var id = Convert.ToUInt64(target.Id);
            var pi = GetPlayerInfo(id);

            string L(string key, params object[] args) => Localize(key, user.Id, args);

            if (skill == Skills.ALL)
            {
                _sb.Clear();
                foreach (var currSkill in AllSkills)
                {
                    if (!IsSkillEnabled(currSkill)) continue;
                    var modifiedLevel = getLevel(pi, currSkill);
                    if (mode == 0) // SET
                        modifiedLevel = level;
                    else if (mode == 1) // ADD
                        modifiedLevel += level;
                    else if (mode == 2) // SUBTRACT
                        modifiedLevel -= level;
                    else if (mode == 4) // DIVIDE
                        modifiedLevel /= level;
                    if (modifiedLevel < 1)
                        modifiedLevel = 1;
                    var levelCap = config.settings.levelCaps.Get(currSkill);
                    if (modifiedLevel > levelCap && levelCap != 0)
                        modifiedLevel = levelCap;
                    setPointsAndLevel(pi, currSkill, getLevelPoints(modifiedLevel), modifiedLevel);
                    var basePlayer = BasePlayer.FindByID(id);
                    if (basePlayer != null) GUIUpdateSkill(basePlayer, currSkill);
                    _sb.Append(L("({0} > {1}) ", L(currSkill + "Skill"), modifiedLevel));
                }
                Reply(user, "\nChanges for '{0}': {1}", target.Name, _sb.ToString().TrimEnd());
            }
            else
            {
                var modifiedLevel = getLevel(pi, skill);
                if (mode == 0) // SET
                    modifiedLevel = level;
                else if (mode == 1) // ADD
                    modifiedLevel += level;
                else if (mode == 2) // SUBTRACT
                    modifiedLevel -= level;
                else if (mode == 4) // DIVIDE
                    modifiedLevel /= level;
                if (modifiedLevel < 1)
                    modifiedLevel = 1;
                var levelCap = config.settings.levelCaps.Get(skill);
                if (modifiedLevel > levelCap && levelCap != 0)
                {
                    modifiedLevel = levelCap;
                }
                setPointsAndLevel(pi, skill, getLevelPoints(modifiedLevel), modifiedLevel);
                var basePlayer = BasePlayer.FindByID(id);
                if (basePlayer != null) GUIUpdateSkill(basePlayer, skill);
                Reply(user, "{0} Lvl for [{1}] set to: [{2}]", L(skill + "Skill"), target.Name, modifiedLevel);
            }
        }

        private string getStatPrint(BasePlayer player, Skills skill)
        {
            if (!IsSkillEnabled(skill))
                return string.Empty;

            string L(string key) => Localize(key, player.UserIDString);

            var pi = GetPlayerInfo(player);
            var levelCap = config.settings.levelCaps.Get(skill);
            var skillMaxed = levelCap != 0 && getLevel(pi, skill) == levelCap;

            string bonusText = $"{GetSkillBonus(getLevel(pi, skill), skill):#0.##}";

            string format = $"<color={config.settings.colors.Get(skill)}>{L("StatsText")}</color>\n";
            var skillName = L(skill + "Skill");
            var xp = $"{getLevel(pi, skill):0}" + (levelCap > 0 ? ("/" + $"{levelCap:0}") : string.Empty);
            var points = $"{getPoints(pi, skill):0}";
            var level = skillMaxed ? "∞" : $"{getLevelPoints(getLevel(pi, skill) + 1):0}";
            var percent = $"{getExperiencePercent(pi, skill):0}%";
            var penalty = $"{getPenaltyPercent(player, pi, skill, false):0}%";
            return string.Format(format, skillName, xp, points, level, bonusText, percent, penalty);
        }

        private void removePoints(PlayerInfo pi, Skills skill, double points)
        {
            switch (skill)
            {
                case Skills.ACQUIRE:
                    pi.ACQUIRE_POINTS = pi.ACQUIRE_POINTS - 10.0 > points ? pi.ACQUIRE_POINTS - points : 10.0;
                    pi.ACQUIRE_LEVEL = getPointsLevel(pi.ACQUIRE_POINTS, skill);
                    break;
                case Skills.CRAFTING:
                    pi.CRAFTING_POINTS = pi.CRAFTING_POINTS - 10.0 > points ? pi.CRAFTING_POINTS - points : 10.0;
                    pi.CRAFTING_LEVEL = getPointsLevel(pi.CRAFTING_POINTS, skill);
                    break;
                case Skills.MINING:
                    pi.MINING_POINTS = pi.MINING_POINTS - 10.0 > points ? pi.MINING_POINTS - points : 10.0;
                    pi.MINING_LEVEL = getPointsLevel(pi.MINING_POINTS, skill);
                    break;
                case Skills.SKINNING:
                    pi.SKINNING_POINTS = pi.SKINNING_POINTS - 10.0 > points ? pi.SKINNING_POINTS - points : 10.0;
                    pi.SKINNING_LEVEL = getPointsLevel(pi.SKINNING_POINTS, skill);
                    break;
                case Skills.WOODCUTTING:
                    pi.WOODCUTTING_POINTS = pi.WOODCUTTING_POINTS - 10.0 > points ? pi.WOODCUTTING_POINTS - points : 10.0;
                    pi.WOODCUTTING_LEVEL = getPointsLevel(pi.WOODCUTTING_POINTS, skill);
                    break;
                case Skills.FISHING:
                    pi.FISHING_POINTS = pi.FISHING_POINTS - 10.0 > points ? pi.FISHING_POINTS - points : 10.0;
                    pi.FISHING_LEVEL = getPointsLevel(pi.FISHING_POINTS, skill);
                    break;
            }
        }

        private double GetPenalty(BasePlayer player, PlayerInfo pi, Skills skill, bool isSuicide)
        {
            double penaltyPercent = getPenaltyPercent(player, pi, skill, isSuicide);

            return skill switch
            {
                Skills.ACQUIRE => config.generic.penaltyReset ? pi.ACQUIRE_POINTS : getPercentAmount(pi.ACQUIRE_LEVEL, penaltyPercent),
                Skills.CRAFTING => config.generic.penaltyReset ? pi.CRAFTING_POINTS : getPercentAmount(pi.CRAFTING_LEVEL, penaltyPercent),
                Skills.MINING => config.generic.penaltyReset ? pi.MINING_POINTS : getPercentAmount(pi.MINING_LEVEL, penaltyPercent),
                Skills.SKINNING => config.generic.penaltyReset ? pi.SKINNING_POINTS : getPercentAmount(pi.SKINNING_LEVEL, penaltyPercent),
                Skills.WOODCUTTING => config.generic.penaltyReset ? pi.WOODCUTTING_POINTS : getPercentAmount(pi.WOODCUTTING_LEVEL, penaltyPercent),
                Skills.FISHING => config.generic.penaltyReset ? pi.FISHING_POINTS : getPercentAmount(pi.FISHING_LEVEL, penaltyPercent),
                _ => 0,
            };
        }

        private double getPenaltyPercent(BasePlayer player, PlayerInfo pi, Skills skill, bool isSuicide)
        {
            var penaltyPercent = 0.0;
            var details = pi.LAST_DEATH;
            var currentTime = DateTime.UtcNow;
            var lastDeath = ToDateTimeFromEpoch(details);
            var timeAlive = currentTime - lastDeath;
            if (timeAlive.TotalMinutes >= (isSuicide ? config.generic.penaltySuicideMinutes : config.generic.penaltyMinutes))
            {
                var percent = isSuicide ? config.settings.percentLostOnSuicide.Get(skill) : config.settings.percentLostOnDeath.Get(skill);
                if (percent == 0)
                    return 0;
                penaltyPercent = percent - (timeAlive.TotalHours * percent / 10.0);
                if (penaltyPercent < 0)
                    penaltyPercent = 0;
            }
            return Math.Round(penaltyPercent, 2);
        }

        private int levelHandler(PlayerInfo pi, BasePlayer player, int prevAmount, Skills skill, BaseEntity source, bool isPowerTool = false)
        {
            double level = getLevel(pi, skill);
            double points = isPowerTool ? pointsPerHitPowerToolCurrent.Get(skill) : pointsPerHitCurrent.Get(skill);
            if (!TryGainXP(pi, player, skill, source, points))
                return prevAmount;

            return Mathf.CeilToInt(prevAmount * (float)getGathMult(level, skill));
        }

        private bool TryGainXP(PlayerInfo pi, BasePlayer player, Skills skill, BaseEntity source, double points)
        {
            object extCanGainZLevelXP = Interface.CallHook("CanGainXP", new object[] { player, source });
            if (extCanGainZLevelXP is false)
                return false;

            string L(string key) => Localize(key, player.UserIDString);

            double xpPercentBefore = getExperiencePercent(pi, skill);
            double level = getLevel(pi, skill);
            double newPoints = getPoints(pi, skill) + points * (getXpMulti(pi, player.UserIDString) / 100.0);

            if (newPoints >= getLevelPoints(level + 1))
            {
                double levelCap = config.settings.levelCaps.Get(skill);
                bool maxLevel = levelCap > 0 && level + 1 > levelCap;

                if (!maxLevel)
                {
                    level = getPointsLevel(newPoints, skill);

                    double levelPoints = getLevelPoints(level + 1);
                    string color = config.settings.colors.Get(skill);
                    string skillName = L(skill + "Skill");
                    string bonus = GetSkillBonus(level, skill).ToString("0.##");
                    string format = $"<color={color}>{L("LevelUpText")}</color>";

                    PrintToChat(player, string.Format(format, skillName, level, newPoints, levelPoints, bonus));
                    BroadcastLevel(player, skill, color, level);
                    GiveReward(player, skill, level);
                }
            }

            setPointsAndLevel(pi, skill, newPoints, level);
            double xpPercentAfter = getExperiencePercent(pi, skill);
            if (xpPercentAfter != xpPercentBefore)
                GUIUpdateSkill(player, skill);
            return true;
        }

        private double GetSkillBonus(double level, Skills skill)
        {
            return skill switch
            {
                Skills.CRAFTING => level * config.settings.craftingDetails.percent,
                Skills.FISHING => GetFishingBonusChance(level),
                _ => (getGathMult(level, skill) - 1.0) * 100.0
            };
        }

        private double GetFishingBonusChance(double level)
        {
            var details = config.settings.fishingDetails;
            return Math.Min(Math.Max(0.0, level - 1.0) * details.extraFishChancePerLevel, details.maxExtraFishChance);
        }

        private void BroadcastLevel(BasePlayer player, Skills skill, string color, double Level)
        {
            if (config.generic.enableLevelupBroadcast && (int)Level % 10 == 0)
            {
                foreach (var target in BasePlayer.activePlayerList)
                {
                    if (target.userID == player.userID || !hasRights(target.UserIDString) || !GetPlayerInfo(target.userID).ENABLED)
                        continue;

                    Message(target, "LevelUpTextBroadcast", player.displayName, Level, color, Localize(skill + "Skill", target.UserIDString));
                }
            }
        }

        private double getExperiencePercentProc(PlayerInfo pi, Skills skill)
        {
            var Level = getLevel(pi, skill);
            var startingPoints = getLevelPoints(Level);
            var nextLevelPoints = getLevelPoints(Level + 1) - startingPoints;
            var Points = getPoints(pi, skill) - startingPoints;
            var experienceProc = Math.Round((Points / nextLevelPoints) * 100.0, 2);
            return experienceProc;
        }

        private double getExperiencePercent(PlayerInfo pi, Skills skill)
        {
            var experienceProc = getExperiencePercentProc(pi, skill);
            if (experienceProc >= 100)
                experienceProc = 99.99;
            return experienceProc;
        }

        private void setPointsAndLevel(PlayerInfo pi, Skills skill, double points, double level)
        {
            double levelPoints = getLevelPoints(level);

            switch (skill)
            {
                case Skills.ACQUIRE:
                    pi.ACQUIRE_LEVEL = level;
                    pi.ACQUIRE_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.CRAFTING:
                    pi.CRAFTING_LEVEL = level;
                    pi.CRAFTING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.MINING:
                    pi.MINING_LEVEL = level;
                    pi.MINING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.SKINNING:
                    pi.SKINNING_LEVEL = level;
                    pi.SKINNING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.WOODCUTTING:
                    pi.WOODCUTTING_LEVEL = level;
                    pi.WOODCUTTING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
                case Skills.FISHING:
                    pi.FISHING_LEVEL = level;
                    pi.FISHING_POINTS = points <= 0.0 ? levelPoints : points;
                    break;
            }
        }

        private double getLevel(PlayerInfo pi, Skills skill)
        {
            switch (skill)
            {
                case Skills.ACQUIRE:
                    return pi.ACQUIRE_LEVEL;
                case Skills.CRAFTING:
                    return pi.CRAFTING_LEVEL;
                case Skills.MINING:
                    return pi.MINING_LEVEL;
                case Skills.SKINNING:
                    return pi.SKINNING_LEVEL;
                case Skills.WOODCUTTING:
                    return pi.WOODCUTTING_LEVEL;
                case Skills.FISHING:
                    return pi.FISHING_LEVEL;
            }

            return 0.0;
        }

        private double getPoints(PlayerInfo pi, Skills skill)
        {
            switch (skill)
            {
                case Skills.ACQUIRE:
                    return pi.ACQUIRE_POINTS;
                case Skills.CRAFTING:
                    return pi.CRAFTING_POINTS;
                case Skills.MINING:
                    return pi.MINING_POINTS;
                case Skills.SKINNING:
                    return pi.SKINNING_POINTS;
                case Skills.WOODCUTTING:
                    return pi.WOODCUTTING_POINTS;
                case Skills.FISHING:
                    return pi.FISHING_POINTS;
            }

            return 0.0;
        }

        private double getLevelPoints(double level) => Math.Round(110 * level * level - 100 * level, 2); // TODO

        private double getPointsLevel(double points, Skills skill)
        {
            var a = 110;
            var b = 100;
            var c = -points;
            var x1 = (-b - Math.Sqrt(b * b - 4 * a * c)) / (2 * a);
            var levelCap = config.settings.levelCaps.Get(skill);
            if ((int)levelCap == 0 || (int)-x1 <= (int)levelCap)
                return (int)-x1;
            return Math.Round(levelCap, 2);
        }

        private double getGathMult(double skillLevel, Skills skill)
        {
            return Math.Round(config.settings.defaultMultipliers.Get(skill) + resourceMultipliersCurrent.Get(skill) * 0.1d * (skillLevel - 1.0), 2);
        }

        private double getXpMulti(PlayerInfo pi, string userid)
        {
            double baseMultiplier = pi.XP_MULTIPLIER;
            double highestMultiplier = double.MinValue;
            foreach (var (name, multiplier) in config.generic.vip)
            {
                if (string.IsNullOrEmpty(name) || multiplier < highestMultiplier)
                {
                    continue;
                }
                if (name.Contains('.') ? permission.UserHasPermission(userid, name) : permission.UserHasGroup(userid, name))
                {
                    highestMultiplier = multiplier;
                }
            }
            if (highestMultiplier == double.MinValue)
            {
                highestMultiplier = 1.0;
            }
            return baseMultiplier * highestMultiplier;
        }

        private bool IsCraftingEnabled()
        {
            if (CraftingController != null) return false;
            return config.settings.levelCaps.Get(Skills.CRAFTING) != -1 || config.settings.craftingDetails.noLevelRequirement;
        }

        private bool IsSkillEnabled(Skills skill)
        {
            if (skill == Skills.CRAFTING && ConVar.Craft.instant)
            {
                return false;
            }
            return config.settings.levelCaps.Get(skill) != -1;
        }

        private double getPointsNeededForNextLevel(double level)
        {
            return getLevelPoints(level + 1) - getLevelPoints(level);
        }

        private double getPercentAmount(double level, double percent)
        {
            return (getPointsNeededForNextLevel(level) * percent) / 100.0;
        }

        private bool IsValid(BasePlayer player)
        {
            return player != null && player.userID.IsSteamId();
        }

        private bool isZoneExcluded(BasePlayer player)
        {
            if (config.settings.zones.Count == 0 || ZoneManager == null)
            {
                return false;
            }

            var values = (string[])ZoneManager?.Call("GetPlayerZoneIDs", player);

            return values?.Length > 0 && config.settings.zones.Any(values.Contains);
        }

        private string ReadableTimeSpan(TimeSpan span, string id)
        {
            string L(string key, params object[] args) => Localize(key, id, args);

            var formatted = string.Format("{0}{1}{2}{3}{4}",
                (span.Days / 7) > 0 ? L("{0:0} weeks", span.Days / 7) + ", " : string.Empty,
                span.Days % 7 > 0 ? L("{0:0} days", span.Days % 7) + ", " : string.Empty,
                span.Hours > 0 ? L("{0:0} hours", span.Hours) + ", " : string.Empty,
                span.Minutes > 0 ? L("{0:0} minutes", span.Minutes) + ", " : string.Empty,
                span.Seconds > 0 ? L("{0:0} seconds", span.Seconds) + ", " : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);
            return formatted;
        }

        private long ToEpochTime(DateTime dateTime)
        {
            var date = dateTime.ToUniversalTime();
            var ticks = date.Ticks - new DateTime(1970, 1, 1, 0, 0, 0, 0).Ticks;
            var ts = ticks / TimeSpan.TicksPerSecond;
            return ts;
        }

        private DateTime ToDateTimeFromEpoch(double intDate)
        {
            var timeInTicks = (long)intDate * TimeSpan.TicksPerSecond;
            return new DateTime(1970, 1, 1, 0, 0, 0, 0).AddTicks(timeInTicks);
        }

        private string Localize(string key, string id = null) => lang.GetMessage(key, this, id);

        private string Localize(string key, string id, params object[] args) => args.Length != 0 ? string.Format(lang.GetMessage(key, this, id), args) : lang.GetMessage(key, this, id);

        private void Reply(IPlayer user, string key, params object[] args)
        {
            if (user == null) return;
            if (user.LastCommand == CommandType.Chat && user.Object is BasePlayer player)
            {
                Message(player, key, args);
                return;
            }
            string message = Localize(key, user.Id, args);
            if (!string.IsNullOrWhiteSpace(message)) user.Reply(message);
        }

        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (player == null) return;
            string message = Localize(key, player.UserIDString, args);
            if (string.IsNullOrWhiteSpace(message)) return;
            Player.Message(player, message, string.IsNullOrEmpty(config.generic.pluginPrefix) ? string.Empty : config.generic.pluginPrefix, config.generic.steamIDIcon);
        }

        private void GenerateItems(bool reset = false)
        {
            if (!reset)
            {
                var config_protocol = config.generic.gameProtocol;

                if (config_protocol != Protocol.network)
                {
                    config.generic.gameProtocol = Protocol.network;
                    Config["Generic", "gameProtocol"] = config.generic.gameProtocol;
                    Puts("Updating item list from protocol {0} to protocol {1}.", config_protocol, config.generic.gameProtocol);
                    GenerateItems(true);
                    SaveConfig();
                    return;
                }
            }

            if (reset)
            {
                Interface.Oxide.DataFileSystem.WriteObject("ZLevelsCraftDetails.old", _craftData);
                _craftData.CraftList.Clear();
                Puts("Generating new item list...");
            }

            CraftItems = ItemManager.GetItemDefinitions().ToDictionary(i => i.shortname);
            int loaded = 0, enabled = 0;
            foreach (var definition in CraftItems)
            {
                if (definition.Value.shortname.Length >= 1)
                {
                    if (_craftData.CraftList.TryGetValue(definition.Value.shortname, out var p))
                    {
                        if (p.Enabled) { enabled++; }
                        loaded++;
                    }
                    else
                    {
                        CraftInfo z = new()
                        {
                            shortName = definition.Value.shortname,
                            MaxBulkCraft = MaxB,
                            MinBulkCraft = MinB,
                            Enabled = true
                        };
                        _craftData.CraftList.Add(definition.Value.shortname, z);
                        loaded++;
                    }
                }
            }
            var inactive = loaded - enabled;
            Puts("Loaded {0} items (Enabled: {1} | Inactive: {2})", loaded, enabled, inactive);
            SaveDetails();
        }
        #endregion Helpers

        #region CUI

        private void GUIUpdateSkill(BasePlayer player, Skills skill)
        {
            if (!config.cui.IsEnabled || !skillIndex.ContainsKey(skill) || !hasRights(player) || !player.IsConnected || !player.IsAlive() || player.IsSleeping()) return;
            double maxRows = skillIndex.Count;
            double rowNumber = skillIndex[skill];
            var pi = GetPlayerInfo(player);
            if (!pi.CUI || !pi.ENABLED) return;
            double level = getLevel(pi, skill);
            //If the player has the max level we don't care about the percentage
            bool isMaxLevel = level >= config.settings.levelCaps.Get(skill);
            double percent = isMaxLevel ? 100.0 : getExperiencePercent(pi, skill);
            string L(string key) => Localize(key, player.UserIDString);
            var skillName = L(skill.ToString() + "Skill");
            var mainPanel = "ZL" + skill;

            var value = 1 / maxRows;
            var positionMin = 1 - (value * rowNumber);
            var positionMax = 2 - (1 - (value * (1 - rowNumber)));

            var container = new CuiElementContainer();
            AddCuiPanel(container, config.cui.cuiBoundsBackground, "0 " + positionMin.ToString("0.####"), $"1 {positionMax:0.####}", "0 0", "0 0", "ZLevelsUI", mainPanel);

            string xpBar = AddCuiImage(container, config.cui.cuiXpBarBackground, "0.190 0.05", "0.750 0.8", "0 0", "0 0", mainPanel);
            AddCuiImage(container, config.cui.cuiColors.Get(skill), "0 0", (percent / 100.0) + " 0.95", "0 0", "0 0", xpBar);

            if (config.cui.cuiTextShadow)
            {
                AddCuiText(container, skillName, config.cui.cuiFontSizeBar, TextAnchor.MiddleCenter, "0.1 0.1 0.1 0.5", "0.035 -0.1", "1 1", "0 0", "0 0", xpBar);
            }

            AddCuiText(container, skillName, config.cui.cuiFontSizeBar, TextAnchor.MiddleCenter, config.cui.cuiFontColor, "0.05 0", "1 1", "0 0", "0 0", xpBar);

            string levelText = $"{L("Lv.")}{level:0}";
            if (config.cui.cuiTextShadow)
            {
                AddCuiText(container, levelText, config.cui.cuiFontSizeLvl, TextAnchor.MiddleLeft, "0.1 0.1 0.1 0.5", "0.028 -0.1", "0.5 1", "0 0", "0 0", mainPanel);
            }

            AddCuiText(container, levelText, config.cui.cuiFontSizeLvl, TextAnchor.MiddleLeft, config.cui.cuiFontColor, "0.025 0", "0.5 1", "0 0", "0 0", mainPanel);

            string percentText = isMaxLevel ? L("MAX") : $"{percent:N2}%";
            if (config.cui.cuiTextShadow)
            {
                AddCuiText(container, percentText, config.cui.cuiFontSizePercent, TextAnchor.MiddleRight, "0.1 0.1 0.1 0.75", "0.5 -0.1", "1 1", "0 0", "0 0", mainPanel);
            }

            AddCuiText(container, percentText, config.cui.cuiFontSizePercent, TextAnchor.MiddleRight, config.cui.cuiFontColor, "0.5 0", "1 1", "0 0", "0 0", mainPanel);
            CuiHelper.AddUi(player, container);
        }

        private void DestroyGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ZLevelsUI");
            CuiHelper.DestroyUi(player, "ZLevelsUIMove");
            uiMovePlayers.Remove(player.userID);
        }

        private void OnCuiDraggableDrag(BasePlayer player, string name, Vector3 position, CommunityEntity.DraggablePositionSendType dragType)
        {
            if (!config.cui.IsEnabled || name != "ZLevelsUI" || !hasRights(player))
            {
                return;
            }

            var pi = GetPlayerInfo(player);
            if (!pi.CUI || !pi.ENABLED) return;

            Vector2 anchor;
            switch (dragType)
            {
                case CommunityEntity.DraggablePositionSendType.NormalizedParent:
                    anchor = new(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));
                    break;
                case CommunityEntity.DraggablePositionSendType.NormalizedScreen:
                    anchor = new(Mathf.Clamp01(position.x), 1f - Mathf.Clamp01(position.y));
                    break;
                default:
                    return;
            }

            data.UiPositions[player.userID] = anchor;
            UpdateMoveButton(player, anchor);
        }

        private string GetUiParent(BasePlayer player)
        {
            if (!uiMovePlayers.Contains(player.userID)) return config.cui.UiPanelString;

            return config.cui.UiPanel switch
            {
                ClientPanels.Overall or ClientPanels.Overlay or ClientPanels.OverlayNonScaled => config.cui.UiPanelString,
                ClientPanels.UnderNonScaled => nameof(ClientPanels.OverlayNonScaled),
                _ => nameof(ClientPanels.Overlay)
            };
        }

        private void UpdateGUIPosition(BasePlayer player)
        {
            if (!config.cui.IsEnabled || !hasRights(player) || !player.IsConnected || !player.IsAlive() || player.IsSleeping()) return;
            var pi = GetPlayerInfo(player);
            if (!pi.CUI || !pi.ENABLED) return;
            var offsets = config.cui.cuiPositioning;
            Vector2 anchor = data.UiPositions.TryGetValue(player.userID, out var position) ? position : offsets.NormalizedAnchor;
            string anchorString = Vec2ToString(anchor);
            GetUiOffsets(out Vector2 min, out Vector2 max);
            var container = new CuiElementContainer();
            AddCuiRectTransformUpdate(container, anchorString, anchorString, Vec2ToString(min), Vec2ToString(max), GetUiParent(player), "ZLevelsUI");
            CuiHelper.AddUi(player, container);
            UpdateMoveButton(player, anchor);
        }

        private void UpdateMoveButton(BasePlayer player, Vector2 anchor)
        {
            bool moving = uiMovePlayers.Contains(player.userID);
            if (!moving && (config.cui.MoveButtonPanel == ClientPanels.None || GetPlayerInfo(player).CUIMOVE)) return;

            GetMoveButtonOffsets(anchor, out Vector2 min, out Vector2 max);
            string anchorString = moving ? "0.5 0.5" : Vec2ToString(anchor);
            string parent = moving ? "ZLevelsUI" : config.cui.MoveButtonPanelString;
            var container = new CuiElementContainer();
            AddCuiRectTransformUpdate(container, anchorString, anchorString, Vec2ToString(min), Vec2ToString(max), parent, "ZLevelsUIMove");
            CuiHelper.AddUi(player, container);
        }

        private void GetUiOffsets(out Vector2 min, out Vector2 max)
        {
            var offsets = config.cui.cuiPositioning;
            float scale = Mathf.Max(0.1f, config.cui.cuiScale);
            Vector2 size = (offsets.Max - offsets.Min) * scale;
            size.y *= Mathf.Max(5, skillIndex.Count) / 5f;
            min = size * -0.5f;
            max = size * 0.5f;
        }

        private void GetMoveButtonOffsets(Vector2 anchor, out Vector2 min, out Vector2 max)
        {
            GetUiOffsets(out Vector2 panelMin, out Vector2 panelMax);
            float right = panelMax.x;
            const float width = 42f;
            const float height = 16f;
            const float gap = 2f;

            if (anchor.y > 0.5f)
            {
                float top = panelMin.y - gap;
                min = new(right - width, top - height);
                max = new(right, top);
            }
            else
            {
                float bottom = panelMax.y + gap;
                min = new(right - width, bottom);
                max = new(right, bottom + height);
            }
        }

        private void CreateGUI(BasePlayer player, PlayerInfo pi)
        {
            if (!config.cui.IsEnabled || pi == null || !pi.ENABLED || !pi.CUI || !hasRights(player) || !player.IsConnected || !player.IsAlive() || player.IsSleeping())
            {
                return;
            }

            const string panelName = "ZLevelsUI";
            bool moving = uiMovePlayers.Contains(player.userID);
            CuiHelper.DestroyUi(player, "ZLevelsUIMove");
            var offsets = config.cui.cuiPositioning;
            Vector2 anchor = data.UiPositions.TryGetValue(player.userID, out var position) ? position : offsets.NormalizedAnchor;
            string anchorString = Vec2ToString(anchor);
            GetUiOffsets(out Vector2 offsetMin, out Vector2 offsetMax);

            var mainContainer = new CuiElementContainer();
            AddCuiPanel(mainContainer, "0 0 0 0", anchorString, anchorString, Vec2ToString(offsetMin), Vec2ToString(offsetMax), GetUiParent(player), panelName, cursor: moving, draggable: true);

            if (moving || config.cui.MoveButtonPanel != ClientPanels.None && !pi.CUIMOVE)
            {
                string parent = moving ? panelName : config.cui.MoveButtonPanelString;
                string buttonAnchor = moving ? "0.5 0.5" : anchorString;
                GetMoveButtonOffsets(anchor, out Vector2 btnOffsetMin, out Vector2 btnOffsetMax);
                AddCuiButton(mainContainer, Localize(moving ? "DONE" : "MOVE", player.UserIDString), 9, moving ? "0.18 0.24 0.31 1" : "0.18 0.24 0.31 0.3", "0.85 0.9 0.95 0.75", buttonAnchor, buttonAnchor, Vec2ToString(btnOffsetMin), Vec2ToString(btnOffsetMax), parent, "ZLevelsUIMove", "zl.ui.move");
            }

            CuiHelper.AddUi(player, mainContainer);

            foreach (Skills skill in AllSkills)
                if (IsSkillEnabled(skill))
                    GUIUpdateSkill(player, skill);
        }

        private static void AddCuiPanel(CuiElementContainer container, string color, string amin, string amax, string omin, string omax, string parent, string name, bool cursor = false, bool draggable = false)
        {
            CuiPanel panel = new()
            {
                CursorEnabled = cursor,
                Image = { Color = color },
                RectTransform = { AnchorMin = amin, AnchorMax = amax, OffsetMin = omin, OffsetMax = omax }
            };

            if (!draggable)
            {
                container.Add(panel, parent, name, name);
                return;
            }

            CuiElement host = new()
            {
                Name = name,
                Parent = parent,
                DestroyUi = name
            };

            if (panel.Image != null) host.Components.Add(panel.Image);
            if (panel.RawImage != null) host.Components.Add(panel.RawImage);
            if (panel.RectTransform != null) host.Components.Add(panel.RectTransform);
            if (panel.CursorEnabled) host.Components.Add(new CuiNeedsCursorComponent());
            host.Components.Add(new CuiDraggableComponent
            {
                LimitToParent = true,
                MaxDistance = -1f,
                AllowSwapping = false,
                DropAnywhere = true,
                DragAlpha = 0.98f,
                Filter = null,
                ParentLimitIndex = 1,
                ParentPadding = "0 0",
                AnchorOffset = "0 0",
                KeepOnTop = false,
                PositionRPC = CommunityEntity.DraggablePositionSendType.NormalizedParent,
                Enabled = true
            });
            container.Add(host);
        }

        private static string AddCuiImage(CuiElementContainer container, string color, string amin, string amax, string omin, string omax, string parent, string name = null)
        {
            name ??= CuiHelper.GetGuid();
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiImageComponent { Color = color },
                    new CuiRectTransformComponent { AnchorMin = amin, AnchorMax = amax, OffsetMin = omin, OffsetMax = omax }
                }
            });
            return name;
        }

        private static string AddCuiText(CuiElementContainer container, string text, int fontSize, TextAnchor align, string color, string amin, string amax, string omin, string omax, string parent, string name = null)
        {
            name ??= CuiHelper.GetGuid();
            container.Add(new CuiElement
            {
                Name = name,
                Parent = parent,
                Components =
                {
                    new CuiTextComponent { Text = text, FontSize = fontSize, Align = align, Color = color },
                    new CuiRectTransformComponent { AnchorMin = amin, AnchorMax = amax, OffsetMin = omin, OffsetMax = omax }
                }
            });
            return name;
        }

        private static string AddCuiButton(CuiElementContainer container, string text, int fontSize, string color, string textColor, string amin, string amax, string omin, string omax, string parent, string name, string command)
        {
            container.Add(new CuiButton
            {
                Button = { Color = color, Command = command },
                Text = { Text = text, FontSize = fontSize, Align = TextAnchor.MiddleCenter, Color = textColor },
                RectTransform = { AnchorMin = amin, AnchorMax = amax, OffsetMin = omin, OffsetMax = omax }
            }, parent, name, name);
            return name;
        }

        private static void AddCuiRectTransformUpdate(CuiElementContainer container, string anchorMin, string anchorMax, string offsetMin, string offsetMax, string parent, string name)
        {
            container.Add(new CuiElement { Name = name, Parent = parent, Update = true, Components = { new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin, OffsetMax = offsetMax } } });
        }

        private static string Vec2ToString(Vector2 value) => $"{value.x.ToString("0.######", CultureInfo.InvariantCulture)} {value.y.ToString("0.######", CultureInfo.InvariantCulture)}";

        private static bool TryParse(string value, out float result) => float.TryParse(value?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        private static bool TryParseOffset(string value, out Vector2 result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] split = value.Trim('(', ')', ' ').Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            return split.Length == 2 && TryParse(split[0], out result.x) && TryParse(split[1], out result.y);
        }

        private PlayerInfo GetPlayerInfo(BasePlayer player)
        {
            if (!player.userID.IsSteamId())
            {
                return null;
            }

            return GetPlayerInfo(player.userID);
        }

        private PlayerInfo CreatePlayerInfo()
        {
            return new()
            {
                ACQUIRE_LEVEL = config.settings.stats.acquire_level,
                ACQUIRE_POINTS = config.settings.stats.acquire_points,
                CRAFTING_LEVEL = config.settings.stats.crafting_level,
                CRAFTING_POINTS = config.settings.stats.crafting_points,
                MINING_LEVEL = config.settings.stats.mining_level,
                MINING_POINTS = config.settings.stats.mining_points,
                SKINNING_LEVEL = config.settings.stats.skinning_level,
                SKINNING_POINTS = config.settings.stats.skinning_points,
                WOODCUTTING_LEVEL = config.settings.stats.woodcutting_level,
                WOODCUTTING_POINTS = config.settings.stats.woodcutting_points,
                FISHING_LEVEL = config.settings.stats.fishing_level,
                FISHING_POINTS = config.settings.stats.fishing_points,
                XP_MULTIPLIER = config.settings.stats.xpm
            };
        }

        private PlayerInfo GetPlayerInfo(ulong userID)
        {
            if (!data.PlayerInfo.TryGetValue(userID, out var pi))
            {
                data.PlayerInfo[userID] = pi = CreatePlayerInfo();

                pi.LAST_DEATH = ToEpochTime(DateTime.UtcNow);
                pi.CUI = config.generic.playerCuiDefaultEnabled;
                pi.ENABLED = config.generic.playerPluginDefaultEnabled;
            }

            return pi;
        }

        #endregion CUI

        #region Config

        private class Configuration
        {
            [JsonProperty(PropertyName = "CUI")]
            public ConfigurationCui cui = new();

            [JsonProperty(PropertyName = "Functions")]
            public ConfigurationFunctions functions = new();

            [JsonProperty(PropertyName = "Generic")]
            public ConfigurationGeneric generic = new();

            [JsonProperty(PropertyName = "Night Bonus")]
            public ConfigurationNightBonus nightbonus = new();

            [JsonProperty(PropertyName = "Settings")]
            public ConfigurationSettings settings = new();

            [JsonProperty(PropertyName = "Level Up Rewards (Reward * Level = Amount)")]
            public Rewards Rewards = new();
        }

        private class ConfigurationCui
        {
            private static bool TryGetClientPanel(string value, out ClientPanels panel) => Enum.TryParse(value?.Replace(".", "") ?? "None", true, out panel) && Enum.IsDefined(typeof(ClientPanels), panel);
            internal string MoveButtonPanelString => MoveButtonPanel == ClientPanels.HudMenu ? "Hud.Menu" : MoveButtonPanel.ToString();
            internal string UiPanelString => UiPanel == ClientPanels.HudMenu ? "Hud.Menu" : UiPanel.ToString();
            internal bool IsEnabled => cuiEnabled && UiPanel != ClientPanels.None;
            internal ClientPanels UiPanel = ClientPanels.Under, MoveButtonPanel = ClientPanels.Overlay;

            public void Normalize()
            {
                cuiColors ??= new("0.4 0 0.8 0.5", "0 1 0 0.5", "0 0 1 0.5", "1 0 0 0.5", "1 0.6 0 0.5", "0 0.75 1 0.5");
                cuiPositioning ??= new();
                cuiPositioning.ConvertOldBounds();
                if (string.IsNullOrWhiteSpace(cuiColors.FISHING)) cuiColors.FISHING = "0 0.75 1 0.5";
                if (displayOnOverlay.HasValue) { UiPanelValue = displayOnOverlay.Value ? nameof(ClientPanels.Overlay) : nameof(ClientPanels.Under); displayOnOverlay = null; }
                UiPanel = TryGetClientPanel(UiPanelValue, out var panel1) ? panel1 : ClientPanels.Under;
                MoveButtonPanel = TryGetClientPanel(MoveButtonPanelValue, out var panel2) ? panel2 : ClientPanels.Overlay;
                MoveButtonPanelValue = MoveButtonPanelString;
                UiPanelValue = UiPanelString;
            }

            [JsonProperty(PropertyName = "Bounds")]
            public ConfigurationCuiPositions cuiPositioning = new();

            [JsonProperty(PropertyName = "UI Scale")]
            public float cuiScale = 1.5f;

            [JsonProperty(PropertyName = "Display on Overlay", NullValueHandling = NullValueHandling.Ignore)]
            public bool? displayOnOverlay;

            [JsonProperty(PropertyName = "UI Client Panel")]
            public string UiPanelValue = nameof(ClientPanels.Under);

            [JsonProperty(PropertyName = "Move Button Client Panel")]
            public string MoveButtonPanelValue = nameof(ClientPanels.Overlay);

            [JsonProperty(PropertyName = "Xp Bar Colors")]
            public ConfigurationColors cuiColors = new("0.4 0 0.8 0.5", "0 1 0 0.5", "0 0 1 0.5", "1 0 0 0.5", "1 0.6 0 0.5", "0 0.75 1 0.5");

            [JsonProperty(PropertyName = "Bounds Background")]
            public string cuiBoundsBackground = "0.1 0.1 0.1 0.1";

            [JsonProperty(PropertyName = "CUI Enabled")]
            public bool cuiEnabled = true;

            [JsonProperty(PropertyName = "Font Color")]
            public string cuiFontColor = "0.74 0.76 0.78 1";

            [JsonProperty(PropertyName = "FontSize Bar")]
            public int cuiFontSizeBar = 11;

            [JsonProperty(PropertyName = "FontSize Level")]
            public int cuiFontSizeLvl = 11;

            [JsonProperty(PropertyName = "FontSize Percent")]
            public int cuiFontSizePercent = 11;

            [JsonProperty(PropertyName = "Text Shadow Enabled")]
            public bool cuiTextShadow = true;

            [JsonProperty(PropertyName = "Xp Bar Background")]
            public string cuiXpBarBackground = "0.2 0.2 0.2 0.2";
        }

        private class ConfigurationFunctions
        {
            [JsonProperty(PropertyName = "Collectible Entities", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, bool> enabledCollectibleEntity = new();

            [JsonProperty(PropertyName = "Enable Collectible Pickup")]
            public bool enableCollectiblePickup = true;

            [JsonProperty(PropertyName = "Collectible Pickup Hook")]
            public string collectiblePickupHook = "OnCollectiblePickup";

            internal string GetCollectiblePickupHook => collectiblePickupHook.Equals(nameof(OnCollectiblePickedup), StringComparison.OrdinalIgnoreCase) ? nameof(OnCollectiblePickedup) : nameof(OnCollectiblePickup);

            [JsonProperty(PropertyName = "Enable Crop Gather")]
            public bool enableCropGather = true;

            [JsonProperty(PropertyName = "Enable Wood Gather")]
            public bool wood = true;

            [JsonProperty(PropertyName = "Grant Wood XP Only")]
            public bool woodXpOnly;

            [JsonProperty(PropertyName = "Enable Stone Ore Gather")]
            public bool stone = true;

            [JsonProperty(PropertyName = "Grant Stone XP Only")]
            public bool stoneXpOnly;

            [JsonProperty(PropertyName = "Enable Sulfur Ore Gather")]
            public bool sulfur = true;

            [JsonProperty(PropertyName = "Grant Sulfur XP Only")]
            public bool sulfurXpOnly;

            [JsonProperty(PropertyName = "Enable Metal Gather")]
            public bool metal = true;

            [JsonProperty(PropertyName = "Grant Metal XP Only")]
            public bool metalXpOnly;

            [JsonProperty(PropertyName = "Enable HQM Gather")]
            public bool hqm = true;

            [JsonProperty(PropertyName = "Grant HQM XP Only")]
            public bool hqmXpOnly;

            [JsonProperty(PropertyName = "Allow Mining Multiplier On Gibs")]
            public bool gibs = true;

            internal bool enableDispenserGather => wood || stone || sulfur || metal || hqm || woodXpOnly || stoneXpOnly || sulfurXpOnly || metalXpOnly || hqmXpOnly;

            public bool ShouldAllowGather(string shortName, out bool grantXPOnly)
            {
                (bool main, bool xpOnly) = shortName switch
                {
                    "wood" => (wood, woodXpOnly),
                    "sulfur.ore" => (sulfur, sulfurXpOnly),
                    "stones" => (stone, stoneXpOnly),
                    "metal.ore" => (metal, metalXpOnly),
                    "hq.metal.ore" => (hqm, hqmXpOnly),
                    _ => (true, false)
                };

                grantXPOnly = !main && xpOnly;
                return main || xpOnly;
            }
        }

        private class ConfigurationGeneric
        {
            [JsonProperty(PropertyName = "Enable Level Up Broadcast")]
            public bool enableLevelupBroadcast;

            [JsonProperty(PropertyName = "Enable Permission")]
            public bool enablePermission;

            [JsonProperty(PropertyName = "Chainsaw On Gather Permission")]
            public string AllowChainsawGather = "zlevelsremastered.chainsaw.allowed";

            [JsonProperty(PropertyName = "Jackhammer On Gather Permission")]
            public string AllowJackhammerGather = "zlevelsremastered.jackhammer.allowed";

            [JsonProperty(PropertyName = "Weapons On Gather Permission")]
            public string BlockWeaponsGather = "zlevelsremastered.weapons.blocked";

            [JsonProperty(PropertyName = "gameProtocol")]
            public int gameProtocol = Protocol.network;

            [JsonProperty(PropertyName = "Penalty Minutes")]
            public int penaltyMinutes = 10;

            [JsonProperty(PropertyName = "Penalty Minutes (Suicide)")]
            public int penaltySuicideMinutes = 10;

            [JsonProperty(PropertyName = "Penalty Resets All Levels To Default")]
            public bool penaltyReset;

            [JsonProperty(PropertyName = "Penalty On Death")]
            public bool penaltyOnDeath = true;

            [JsonProperty(PropertyName = "Penalty On Suicide")]
            public bool penaltyOnSuicide;

            [JsonProperty(PropertyName = "Permission Name")]
            public string permissionName = "zlevelsremastered.use";

            [JsonProperty(PropertyName = "Permission Name XP")]
            public string permissionNameXP = "zlevelsremastered.noxploss";

            [JsonProperty(PropertyName = "Additional Boost Multipliers")]
            public Dictionary<string, double> vip = new()
            {
                ["zlevelsremastered.vip1"] = 2.0,
                ["zlevelsremastered.vip2"] = 3.0,
                ["zlevelsremastered.vip3"] = 4.0,
                ["zlevelsvip1"] = 5.0,
                ["zlevelsvip2"] = 6.0,
                ["zlevelsvip3"] = 7.0,
            };

            [JsonProperty(PropertyName = "Permission Name No Wipes")]
            public string nowipe = "zlevelsremastered.nowipes";

            [JsonProperty(PropertyName = "Player CUI Default Enabled")]
            public bool playerCuiDefaultEnabled = true;

            [JsonProperty(PropertyName = "Player Plugin Default Enabled")]
            public bool playerPluginDefaultEnabled = true;

            [JsonProperty(PropertyName = "Plugin Prefix")]
            public string pluginPrefix = "<color=orange>ZLevels</color>: ";

            [JsonProperty(PropertyName = "SteamID Icon")]
            public ulong steamIDIcon;

            [JsonProperty(PropertyName = "Wipe Data OnNewSave")]
            public bool wipeDataOnNewSave;
        }

        private class ConfigurationNightBonus
        {
            [JsonProperty(PropertyName = "Points Per Hit At Night")]
            public ConfigurationResources pointsPerHitAtNight = new(60, null, 60, 60, 60);

            [JsonProperty(PropertyName = "Points Per PowerTool At Night")]
            public ConfigurationResources pointsPerPowerToolAtNight = new(null, null, 60, null, 60);

            [JsonProperty(PropertyName = "Resource Per Level Multiplier At Night")]
            public ConfigurationResources resourceMultipliersAtNight = new(2, null, 2, 2, 2);

            [JsonProperty(PropertyName = "Enable Night Bonus")]
            public bool enableNightBonus;

            [JsonProperty(PropertyName = "Broadcast Enabled Bonus")]
            public bool broadcastEnabledBonus = true;

            [JsonProperty(PropertyName = "Log Enabled Bonus Console")]
            public bool logEnabledBonusConsole;
        }

        private class ConfigurationSettings
        {
            [JsonProperty(PropertyName = "Crafting Details", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public ConfigurationCraftingDetails craftingDetails = new(1, 3, 5);

            [JsonProperty(PropertyName = "Fishing Details")]
            public ConfigurationFishingDetails fishingDetails = new();

            [JsonProperty(PropertyName = "Default Resource Multiplier")]
            public ConfigurationResources defaultMultipliers = new(1, null, 1, 1, 1);

            [JsonProperty(PropertyName = "Level Caps")]
            public ConfigurationResources levelCaps = new(200, 20, 200, 200, 200, 200);

            [JsonProperty(PropertyName = "Percent Lost On Death")]
            public ConfigurationResources percentLostOnDeath = new(50, 50, 50, 50, 50, 50);

            [JsonProperty(PropertyName = "Percent Lost On Suicide")]
            public ConfigurationResources percentLostOnSuicide = new(0, 0, 0, 0, 0, 0);

            [JsonProperty(PropertyName = "No Penalty Zones", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> zones = new() { "adminzone1", "999999" };

            [JsonProperty(PropertyName = "Points Per Hit")]
            public ConfigurationResources pointsPerHit = new(30, null, 30, 30, 30);

            [JsonProperty(PropertyName = "Points Per Power Tool")]
            public ConfigurationResources pointsPerPowerTool = new(null, null, 30, null, 30);

            [JsonProperty(PropertyName = "Resource Per Level Multiplier")]
            public ConfigurationResources resourceMultipliers = new(2, null, 2, 2, 2);

            [JsonProperty(PropertyName = "Skill Colors")]
            public ConfigurationSkillColors colors = new();

            [JsonProperty(PropertyName = "Starting Stats")]
            public ConfigurationStartingStats stats = new();

            [JsonProperty(PropertyName = "Use Mining Skill When Picking Up Stones And Ore")]
            public bool MiningStonesOre = true;

            [JsonProperty(PropertyName = "Use Acquire Skill When Picking Up Wood")]
            public bool AcquireWood;
        }

        public class ConfigurationStartingStats
        {
            [JsonProperty(PropertyName = "Acquire Level")]
            public double acquire_level = 1.0;

            [JsonProperty(PropertyName = "Acquire Points")]
            public double acquire_points = 10.0;

            [JsonProperty(PropertyName = "Crafting Level")]
            public double crafting_level = 1.0;

            [JsonProperty(PropertyName = "Crafting Points")]
            public double crafting_points = 10.0;

            [JsonProperty(PropertyName = "Mining Level")]
            public double mining_level = 1.0;

            [JsonProperty(PropertyName = "Mining Points")]
            public double mining_points = 10.0;

            [JsonProperty(PropertyName = "Skinning Level")]
            public double skinning_level = 1.0;

            [JsonProperty(PropertyName = "Skinning Points")]
            public double skinning_points = 10.0;

            [JsonProperty(PropertyName = "Woodcutting Level")]
            public double woodcutting_level = 1.0;

            [JsonProperty(PropertyName = "Woodcutting Points")]
            public double woodcutting_points = 10.0;

            [JsonProperty(PropertyName = "Fishing Level")]
            public double fishing_level = 1.0;

            [JsonProperty(PropertyName = "Fishing Points")]
            public double fishing_points = 10.0;

            [JsonProperty(PropertyName = "XP Multiplier")]
            public double xpm = 100.0;

        }

        public class ConfigurationResources
        {
            [JsonProperty(PropertyName = nameof(Skills.ACQUIRE), NullValueHandling = NullValueHandling.Ignore)]
            public double? ACQUIRE;

            [JsonProperty(PropertyName = nameof(Skills.CRAFTING), NullValueHandling = NullValueHandling.Ignore)]
            public double? CRAFTING;

            [JsonProperty(PropertyName = nameof(Skills.MINING), NullValueHandling = NullValueHandling.Ignore)]
            public double? MINING;

            [JsonProperty(PropertyName = nameof(Skills.SKINNING), NullValueHandling = NullValueHandling.Ignore)]
            public double? SKINNING;

            [JsonProperty(PropertyName = nameof(Skills.WOODCUTTING), NullValueHandling = NullValueHandling.Ignore)]
            public double? WOODCUTTING;

            [JsonProperty(PropertyName = nameof(Skills.FISHING), NullValueHandling = NullValueHandling.Ignore)]
            public double? FISHING;

            public ConfigurationResources(double? ACQUIRE, double? CRAFTING, double? MINING, double? SKINNING, double? WOODCUTTING, double? FISHING = null)
            {
                this.ACQUIRE = ACQUIRE;
                this.CRAFTING = CRAFTING;
                this.MINING = MINING;
                this.SKINNING = SKINNING;
                this.WOODCUTTING = WOODCUTTING;
                this.FISHING = FISHING;
            }

            public double Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        return ACQUIRE.HasValue ? ACQUIRE.Value : 0.0;
                    case Skills.CRAFTING:
                        return CRAFTING.HasValue ? CRAFTING.Value : 0.0;
                    case Skills.MINING:
                        return MINING.HasValue ? MINING.Value : 0.0;
                    case Skills.SKINNING:
                        return SKINNING.HasValue ? SKINNING.Value : 0.0;
                    case Skills.WOODCUTTING:
                        return WOODCUTTING.HasValue ? WOODCUTTING.Value : 0.0;
                    case Skills.FISHING:
                        return FISHING.HasValue ? FISHING.Value : 0.0;
                }

                return 0;
            }

            public void Set(Skills skill, double value)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        ACQUIRE = value;
                        break;
                    case Skills.CRAFTING:
                        CRAFTING = value;
                        break;
                    case Skills.MINING:
                        MINING = value;
                        break;
                    case Skills.SKINNING:
                        SKINNING = value;
                        break;
                    case Skills.WOODCUTTING:
                        WOODCUTTING = value;
                        break;
                    case Skills.FISHING:
                        FISHING = value;
                        break;
                }
            }
        }

        public class ConfigurationColors
        {
            [JsonProperty(PropertyName = nameof(Skills.ACQUIRE))]
            public string ACQUIRE;

            [JsonProperty(PropertyName = nameof(Skills.CRAFTING))]
            public string CRAFTING;

            [JsonProperty(PropertyName = nameof(Skills.MINING))]
            public string MINING;

            [JsonProperty(PropertyName = nameof(Skills.SKINNING))]
            public string SKINNING;

            [JsonProperty(PropertyName = nameof(Skills.WOODCUTTING))]
            public string WOODCUTTING;

            [JsonProperty(PropertyName = nameof(Skills.FISHING))]
            public string FISHING;

            public ConfigurationColors(string ACQUIRE, string CRAFTING, string MINING, string SKINNING, string WOODCUTTING, string FISHING = "0 0.75 1 0.5")
            {
                this.ACQUIRE = ACQUIRE;
                this.CRAFTING = CRAFTING;
                this.MINING = MINING;
                this.SKINNING = SKINNING;
                this.WOODCUTTING = WOODCUTTING;
                this.FISHING = FISHING;
            }

            public string Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        return ACQUIRE;
                    case Skills.CRAFTING:
                        return CRAFTING;
                    case Skills.MINING:
                        return MINING;
                    case Skills.SKINNING:
                        return SKINNING;
                    case Skills.WOODCUTTING:
                        return WOODCUTTING;
                    case Skills.FISHING:
                        return FISHING;
                }

                return "1 1 1 1";
            }
        }

        public class ConfigurationCraftingDetails
        {
            [JsonProperty(PropertyName = "Time Spent")]
            public double time;

            [JsonProperty(PropertyName = "XP Per Time Spent")]
            public double xp;

            [JsonProperty(PropertyName = "Percent Faster Per Level")]
            public double percent;

            [JsonProperty(PropertyName = "Require Permission For Instant Bulk Craft")]
            public bool usePermission;

            [JsonProperty(PropertyName = "Instant Bulk Craft Permission Does Not Require Max Level")]
            public bool noLevelRequirement;

            [JsonProperty(PropertyName = "Permission For Instant Bulk Crafting At Max Level")]
            public string Permission = "zlevelsremastered.crafting.instantbulk";

            [JsonProperty(PropertyName = "Require Inventory Slots")]
            public bool slots;

            public ConfigurationCraftingDetails(double time, double xp, double percent)
            {
                this.time = time;
                this.xp = xp;
                this.percent = percent;
            }
        }

        public class ConfigurationFishingDetails
        {
            [JsonProperty(PropertyName = "XP Per Catch")]
            public double xpPerCatch = 30.0;

            [JsonProperty(PropertyName = "Extra Fish Chance Per Level (%)")]
            public double extraFishChancePerLevel = 0.25;

            [JsonProperty(PropertyName = "Maximum Extra Fish Chance (%)")]
            public double maxExtraFishChance = 50.0;
        }

        public class Vector2Converter : JsonConverter
        {
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    return Vector2.zero;
                }

                if (reader.TokenType == JsonToken.String && TryParseOffset(reader.Value as string, out Vector2 vector))
                {
                    return vector;
                }

                if (reader.TokenType == JsonToken.StartObject)
                {
                    var values = serializer.Deserialize(reader, typeof(Dictionary<string, float>)) as Dictionary<string, float>;
                    if (values != null && values.TryGetValue("x", out float x) && values.TryGetValue("y", out float y))
                    {
                        return new Vector2(x, y);
                    }
                }

                throw new JsonSerializationException($"Invalid Vector2 value for {objectType.Name}.");
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteValue(Vec2ToString((Vector2)value));
            }

            public override bool CanConvert(Type objectType) => objectType == typeof(Vector2);
        }

        public class UiOffsets
        {
            [JsonProperty(PropertyName = "Offset Min")]
            [JsonConverter(typeof(Vector2Converter))]
            public Vector2 Min;

            [JsonProperty(PropertyName = "Offset Max")]
            [JsonConverter(typeof(Vector2Converter))]
            public Vector2 Max;

            [JsonProperty(PropertyName = "Normalized Anchor")]
            [JsonConverter(typeof(Vector2Converter))]
            public Vector2 NormalizedAnchor;

            public UiOffsets()
            {
                Min = Vector2.zero;
                Max = Vector2.zero;
                NormalizedAnchor = Vector2.zero;
            }

            public UiOffsets(Vector2 min, Vector2 max, Vector2 normalizedAnchor)
            {
                Min = min;
                Max = max;
                NormalizedAnchor = normalizedAnchor;
            }

            internal string MinString => Vec2ToString(Min);
            internal string MaxString => Vec2ToString(Max);
        }

        public class ConfigurationCuiPositions : UiOffsets
        {
            [JsonProperty(PropertyName = "Width Left", NullValueHandling = NullValueHandling.Ignore)]
            public string widthLeft;

            [JsonProperty(PropertyName = "Width Right", NullValueHandling = NullValueHandling.Ignore)]
            public string widthRight;

            [JsonProperty(PropertyName = "Height Lower", NullValueHandling = NullValueHandling.Ignore)]
            public string heightLower;

            [JsonProperty(PropertyName = "Height Upper", NullValueHandling = NullValueHandling.Ignore)]
            public string heightUpper;

            public ConfigurationCuiPositions() : base(new Vector2(-67.2f, -36.9f), new Vector2(67.2f, 36.9f), new Vector2(0.7775f, 0.07125f)) { }

            public void ConvertOldBounds()
            {
                if (!TryParse(widthLeft, out float left) || !TryParse(widthRight, out float right) || !TryParse(heightLower, out float lower) || !TryParse(heightUpper, out float upper))
                {
                    return;
                }

                float width = (right - left) * 1280f;
                float height = (upper - lower) * 720f;
                Min = new Vector2(width * -0.5f, height * -0.5f);
                Max = new Vector2(width * 0.5f, height * 0.5f);
                NormalizedAnchor = new Vector2((left + right) * 0.5f, (lower + upper) * 0.5f);
                widthLeft = widthRight = heightLower = heightUpper = null;
            }
        }

        public class ConfigurationSkillColors
        {
            [JsonProperty(PropertyName = nameof(Skills.ACQUIRE))]
            public string ACQUIRE = "#7700AA";

            [JsonProperty(PropertyName = nameof(Skills.CRAFTING))]
            public string CRAFTING = "#00FF00";

            [JsonProperty(PropertyName = nameof(Skills.MINING))]
            public string MINING = "#0000FF";

            [JsonProperty(PropertyName = nameof(Skills.SKINNING))]
            public string SKINNING = "#FF0000";

            [JsonProperty(PropertyName = nameof(Skills.WOODCUTTING))]
            public string WOODCUTTING = "#FF9900";

            [JsonProperty(PropertyName = nameof(Skills.FISHING))]
            public string FISHING = "#00BFFF";

            public string Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE:
                        return ACQUIRE;
                    case Skills.CRAFTING:
                        return CRAFTING;
                    case Skills.MINING:
                        return MINING;
                    case Skills.SKINNING:
                        return SKINNING;
                    case Skills.WOODCUTTING:
                        return WOODCUTTING;
                    case Skills.FISHING:
                        return FISHING;
                }

                return "#FF0000";
            }
        }

        public class RewardType
        {
            [JsonProperty(PropertyName = "Acquire")]
            public double Acquire;

            [JsonProperty(PropertyName = "Crafting")]
            public double Crafting;

            [JsonProperty(PropertyName = "Mining")]
            public double Mining;

            [JsonProperty(PropertyName = "Skinning")]
            public double Skinning;

            [JsonProperty(PropertyName = "Woodcutting")]
            public double Woodcutting;

            [JsonProperty(PropertyName = "Fishing")]
            public double Fishing;

            public double Get(Skills skill)
            {
                switch (skill)
                {
                    case Skills.ACQUIRE: return Acquire;
                    case Skills.CRAFTING: return Crafting;
                    case Skills.MINING: return Mining;
                    case Skills.SKINNING: return Skinning;
                    case Skills.WOODCUTTING: return Woodcutting;
                    case Skills.FISHING: return Fishing;
                    default: return 0.0;
                }
            }
        }

        public class Rewards
        {
            [JsonProperty(PropertyName = "Economics Money")]
            public RewardType Money = new();

            [JsonProperty(PropertyName = "ServerRewards Points")]
            public RewardType Points = new();

            [JsonProperty(PropertyName = "SkillTree XP")]
            public RewardType XP = new();
        }

        public void GiveReward(BasePlayer player, Skills skill, double level)
        {
            if (Economics != null && Economics.IsLoaded)
            {
                var money = config.Rewards.Money.Get(skill) * level;
                if (money > 0)
                {
                    Economics?.Call("Deposit", player.UserIDString, money);
                    Message(player, "EconomicsDeposit", money);
                }
            }

            if (IQEconomic != null && IQEconomic.IsLoaded)
            {
                var money = config.Rewards.Money.Get(skill) * level;
                if (money > 0)
                {
                    IQEconomic?.Call("API_SET_BALANCE", player.UserIDString, (int)money);
                    Message(player, "EconomicsDeposit", (int)money);
                }
            }

            if (ServerRewards != null && ServerRewards.IsLoaded)
            {
                var points = config.Rewards.Points.Get(skill) * level;
                if (points > 0)
                {
                    ServerRewards?.Call("AddPoints", player.UserIDString, (int)points);
                    Message(player, "ServerRewardPoints", (int)points);
                }
            }

            if (SkillTree != null && SkillTree.IsLoaded)
            {
                var xp = config.Rewards.XP.Get(skill) * level;
                if (xp > 0)
                {
                    SkillTree?.Call("AwardXP", player, xp);
                    Message(player, "SkillTreeXP", xp);
                }
            }
        }

        private Configuration config = new();
        //private ConfigurationResources pointsPerPowerTool;
        private ConfigurationResources pointsPerHitCurrent;
        //private ConfigurationResources pointsPerPowerToolAtNight;
        private ConfigurationResources pointsPerHitPowerToolCurrent;
        private ConfigurationResources resourceMultipliersCurrent;
        private Dictionary<Skills, int> skillIndex = new();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                canSaveConfig = true;
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
                return;
            }
            LoadDefaultMessages();
            NormalizeConfig();
            SaveConfig();
        }

        private void NormalizeConfig()
        {
            config.cui ??= new();
            config.cui.Normalize();
            if (string.IsNullOrWhiteSpace(config.functions.collectiblePickupHook)) config.functions.collectiblePickupHook = nameof(OnCollectiblePickup);
            config.functions.collectiblePickupHook = config.functions.GetCollectiblePickupHook;
            config.settings.fishingDetails ??= new();
            if (string.IsNullOrWhiteSpace(config.settings.colors.FISHING)) config.settings.colors.FISHING = "#00BFFF";
            config.settings.levelCaps.FISHING ??= 200.0;
            config.settings.percentLostOnDeath.FISHING ??= 50.0;
            config.settings.percentLostOnSuicide.FISHING ??= 0.0;
            config.settings.fishingDetails.xpPerCatch = Math.Max(0.0, config.settings.fishingDetails.xpPerCatch);
            config.settings.fishingDetails.extraFishChancePerLevel = Math.Max(0.0, config.settings.fishingDetails.extraFishChancePerLevel);
            config.settings.fishingDetails.maxExtraFishChance = Math.Clamp(config.settings.fishingDetails.maxExtraFishChance, 0.0, 100.0);
            CheckStartingStats();
        }

        public void CheckStartingStats()
        {
            var pi = CreatePlayerInfo();
            var ACQUIRE = getExperiencePercentProc(pi, Skills.ACQUIRE);

            if (ACQUIRE < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.ACQUIRE_LEVEL)) * (-1 * ACQUIRE) / 100);
                Puts("Invalid acquire starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.ACQUIRE_POINTS, pi.XP_MULTIPLIER, ACQUIRE, value);
                config.settings.stats.acquire_points += value;
                pi.ACQUIRE_POINTS += value;
            }

            var CRAFTING = getExperiencePercentProc(pi, Skills.CRAFTING);

            if (CRAFTING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.CRAFTING_LEVEL)) * (-1 * CRAFTING) / 100);
                Puts("Invalid crafting starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.CRAFTING_POINTS, pi.XP_MULTIPLIER, CRAFTING, value);
                config.settings.stats.crafting_points += value;
                pi.CRAFTING_POINTS += value;
            }

            var MINING = getExperiencePercentProc(pi, Skills.MINING);

            if (MINING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.MINING_LEVEL)) * (-1 * MINING) / 100);
                Puts("Invalid mining starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.MINING_POINTS, pi.XP_MULTIPLIER, MINING, value);
                config.settings.stats.mining_points += value;
                pi.MINING_POINTS += value;
            }

            var SKINNING = getExperiencePercentProc(pi, Skills.SKINNING);

            if (SKINNING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.SKINNING_LEVEL)) * (-1 * SKINNING) / 100);
                Puts("Invalid skinning starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.SKINNING_POINTS, pi.XP_MULTIPLIER, SKINNING, value);
                config.settings.stats.skinning_points += value;
                pi.SKINNING_POINTS += value;
            }

            var WOODCUTTING = getExperiencePercentProc(pi, Skills.WOODCUTTING);

            if (WOODCUTTING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.WOODCUTTING_LEVEL)) * (-1 * WOODCUTTING) / 100);
                Puts("Invalid woodcutting starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.WOODCUTTING_POINTS, pi.XP_MULTIPLIER, WOODCUTTING, value);
                config.settings.stats.woodcutting_points += value;
                pi.WOODCUTTING_POINTS += value;
            }

            var FISHING = getExperiencePercentProc(pi, Skills.FISHING);

            if (FISHING < 0.0)
            {
                var value = Math.Ceiling(getPointsNeededForNextLevel(Math.Max(1.0, pi.FISHING_LEVEL)) * (-1 * FISHING) / 100);
                Puts("Invalid fishing starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", pi.FISHING_POINTS, pi.XP_MULTIPLIER, FISHING, value);
                config.settings.stats.fishing_points += value;
                pi.FISHING_POINTS += value;
            }
        }

        private bool canSaveConfig = true;

        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config, true);
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new();
        }

        protected override void LoadDefaultMessages()
        {
            string[] keys = new string[]
            {
                "Default stats UI position saved and all player positions reset.", "Stats UI position reset.", "Stats UI move button hidden. Use /statsui move to move the UI.", "Stats UI move button shown.", "The stats UI move button is disabled. Use /statsui move to move the UI.",
                "<size=18><color=orange>ZLevels</color></size><size=14><color=#ce422b>REMASTERED</color></size>", "/stats - Displays your stats.", "/statsui - Enable/Disable stats UI. Use /statsui reset to restore its position.", "/statsui move - Start/finish moving the UI, or click DONE to finish moving the UI. /statsui button - Show/hide the move button.",
                "/statsonoff - Enable/Disable whole leveling.", "/statinfo - Displays information about skills.", "/stathelp - Displays the help.", "Lv.", "MAX", "* -{0} {1} XP.", "Syntax: zl.pointsperhit skill number", "Possible skills are: WC, M, S, A, C, *(All skills)", "Incorrect number. Must be greater than or equal to 1", 
                "Incorrect skill. Possible skills are: WC, M, S, A, C, *(All skills).", "PlayerData is NULL!", "Incorrect number. Must be greater than 0", "Userdata was successfully reset to zero", "Incorrect skill. Possible skills are: WC, M, S, A, C, F, *(All skills).", "Penalty is now enabled.", "Penalty is now disabled.", "Current points per hit:",
                "New points per hit:", "Syntax: zl.lvl name|steamid skill [OPERATOR]NUMBER", "Example: zl.lvl Player WC /2 -- Player gets his WC level divided by 2.", "Example: zl.lvl * * +3 -- Everyone currently playing in the server gets +3 for all skills.", "Example: zl.lvl ** * /2 -- Everyone (including offline players) gets their level divided by 2.",
                "Instead of names you can use wildcard(*): * - affects online players, ** - affects all players", "Possible operators: *(XP Modified %), +(Adds level), -(Removes level), /(Divides level)", " {0} > {1} |", "Current XP multiplier for player '{0}' is {1}% with vip raising total to {2}%", "Current XP multiplier for player '{0}' is {1}%",
                "New XP multiplier for player '{0}' is {1}% with vip raising total to {2}%", "New XP multiplier for player '{0}' is {1}%", "FieldInfo", "Level", "Points", "XPMultiplier is changeable for all skills! Use * instead of {0}.", "ALL ONLINE PLAYERS", "ALL PLAYERS", "XP rates has changed to {0}% of normal XP for {1}", "{0} / {1} entries in datafile can be removed",
                "{0} / {1} entries in datafile have been removed", "Penalty is currently enabled.", "Penalty is currently disabled.", "Time alive: {0}", "XP rates for you are {0}%", "(DISABLED)", "<color={0}>{1}</color> {2}", "XP per catch: <color={0}>{1:#0.##}</color>", "Bonus: <color={0}>Each level after 1 adds a {1:#0.##}% chance to catch one extra fish, up to {2:#0.##}%.</color>",
                "XP gain: <color={0}>You get {1} XP per {2}s spent crafting.</color>", "Bonus: <color={0}>Crafting time is decreased by {1}% per every level.</color>", "XP per hit: <color={0}>{1}</color>", "Bonus materials per level: <color={0}>{1:#0.##} %</color>", "shortname (itemid)", "bait", "depth", "water", "weight", "type", "any", "junk", "fish", "catchables:\n{0}", "({0} > {1}) ",
                "\nChanges for '{0}': {1}", "{0} Lvl for [{1}] set to: [{2}]", "{0:0} weeks", "{0:0} days", "{0:0} hours", "{0:0} minutes", "{0:0} seconds", "DONE", "MOVE", "New save detected...", "Stats can be reset by > zl.reset <", "Nightbonus points enabled", "Nightbonus points disabled", "There is problem obtaining task.blueprint on 'OnItemCraftFinished' hook! This is usually caused by some incompatable plugins.",
                "{0} ({1}) reset userdata to zero", "CraftData threw an exception!", "Crafting data is null and has been reset!", "Crafting list is null and has been reset!", "StoredData threw an exception!", "Data is null, resetting to defaults!", "Wiping data... new save detected", "Generating new item list...", "Loaded {0} items (Enabled: {1} | Inactive: {2})",
                "Invalid acquire starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", "Invalid crafting starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", "Invalid mining starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}",
                "Invalid skinning starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", "Invalid woodcutting starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}", "Invalid fishing starting points: {0} ({1}% multiplier) resulting in {2}% towards next level. Points adjusted to: {3}",
                "Updating item list from protocol {0} to protocol {1}.", "WARNING: {0} picked up undefined item: {1}. Defaulting to {2} skill.",
            };
            Dictionary<string, string> messages = new()
            {
                {"StatsHeadline", "Level stats (/statinfo - To get more information about skills)"},
                {"StatsText",   "-{0}\nLevel: {1} (+{4}% bonus) \nXP: {2}/{3} [{5}].\n<color=red>-{6} XP loss on death.</color>"},
                {"LevelUpText", "{0} Level up\nLevel: {1} (+{4}% bonus) \nXP: {2}/{3}"},
                {"LevelUpTextBroadcast", "<color=#5af>{0}</color> has reached level <color=#5af>{1}</color> in <color={2}>{3}</color>"},
                {"PenaltyText", "<color=orange>You have lost XP for dying:{0}</color>"},
                {"NoPermission", "You don't have permission to use this command"},
                {"WOODCUTTINGSkill", "Woodcutting"},
                {"MININGSkill", "Mining"},
                {"SKINNINGSkill", "Skinning"},
                {"CRAFTINGSkill", "Crafting" },
                {"FISHINGSkill", "Fishing" },
                {"ACQUIRESkill", "Acquire" },
                {"XPM", "XP Multiplier"},
                {"STATS", "Stats for player: "},
                {"INFO USE", "Usage: zl.info name|steamid"},
                {"RESET USE", "Usage: zl.reset true | Resets all userdata to zero"},
                {"XPM USE 1", "Syntax: zl.playerxpm name|steamid (to show current XP multiplier)"},
                {"XPM USE 2", "Syntax: zl.playerxpm name|steamid number (to set current XP multiplier >= 100)"},
                {"PLAYER NOT FOUND", "Player not found!"},
                {"NightBonusOn", "Nightbonus for points per hit enabled"},
                {"NightBonusOff", "Nightbonus for points per hit disabled"},
                {"PluginPlayerOn", "The plugin functions are now enabled again"},
                {"PluginPlayerOff", "The plugin functions are now disabled for your character"},
                {"SkillTreeXP", "You have received <color=#FFFF00>{0} XP</color> for leveling up!"},
                {"ServerRewardPoints", "You have received <color=#FFFF00>{0} RP</color> for leveling up!"},
                {"EconomicsDeposit", "You have received <color=#FFFF00>${0}</color> for leveling up!"},
                {"NoSlots", "You don't have enough slots to craft!"},
            };
            for (int index = 0; index < keys.Length; index++)
            {
                messages[keys[index]] = keys[index];
            }
            lang.RegisterMessages(messages, this);
        }

        #endregion Config

        #region API

        private double GetMultiplier(ulong userID, string skill = "A")
        {
            double multiplier = 1;
            if (userID.IsSteamId())
            {
                switch (skill.ToUpper())
                {
                    case "A":
                        return getGathMult(GetLevel(userID, skill), Skills.ACQUIRE);
                    case "C":
                        return getGathMult(GetLevel(userID, skill), Skills.CRAFTING);
                    case "M":
                        return getGathMult(GetLevel(userID, skill), Skills.MINING);
                    case "S":
                        return getGathMult(GetLevel(userID, skill), Skills.SKINNING);
                    case "WC":
                        return getGathMult(GetLevel(userID, skill), Skills.WOODCUTTING);
                    case "F":
                    case "FISHING":
                        return 1.0 + GetFishingBonusChance(GetLevel(userID, skill)) / 100.0;
                }
            }

            return multiplier;
        }

        private double GetLevel(ulong userID, string skill = "A")
        {
            if (data.PlayerInfo.TryGetValue(userID, out var pi))
            {
                switch (skill.ToUpper())
                {
                    case "A":
                    case "ACQUIRE":
                        return pi.ACQUIRE_LEVEL;
                    case "C":
                    case "CRAFTING":
                        return pi.CRAFTING_LEVEL;
                    case "M":
                    case "MINING":
                        return pi.MINING_LEVEL;
                    case "S":
                    case "SKINNING":
                        return pi.SKINNING_LEVEL;
                    case "WC":
                    case "WOODCUTTING":
                        return pi.WOODCUTTING_LEVEL;
                    case "F":
                    case "FISHING":
                        return pi.FISHING_LEVEL;
                }
            }

            return 0;
        }

        private string api_GetPlayerInfo(ulong playerid)
        {
            if (playerid != 0)
            {
                var pi = GetPlayerInfo(playerid);
                return pi.ACQUIRE_LEVEL + "|" +
                    pi.ACQUIRE_POINTS + "|" +
                    pi.CRAFTING_LEVEL + "|" +
                    pi.CRAFTING_POINTS + "|" +
                    pi.CUI + "|" +
                    pi.LAST_DEATH + "|" +
                    pi.MINING_LEVEL + "|" +
                    pi.MINING_POINTS + "|" +
                    pi.ENABLED + "|" +
                    pi.SKINNING_LEVEL + "|" +
                    pi.SKINNING_POINTS + "|" +
                    pi.WOODCUTTING_LEVEL + "|" +
                    pi.WOODCUTTING_POINTS + "|" +
                    getXpMulti(pi, playerid.ToString()) + "|" +
                    pi.FISHING_LEVEL + "|" +
                    pi.FISHING_POINTS;
            }
            return string.Empty;
        }

        private bool api_SetPlayerInfo(ulong userid, string data)
        {
            if (userid == 0 || data == null) { return false; }
            if (!this.data.PlayerInfo.TryGetValue(userid, out var pi))
            {
                this.data.PlayerInfo[userid] = pi = CreatePlayerInfo();
                pi.LAST_DEATH = ToEpochTime(DateTime.UtcNow);
                pi.CUI = config.generic.playerCuiDefaultEnabled;
                pi.ENABLED = config.generic.playerPluginDefaultEnabled;
            }
            string[] split = data.Split('|');
            if (split.Length < 14)
            {
                return false;
            }
            pi.ACQUIRE_LEVEL = double.Parse(split[0]);
            pi.ACQUIRE_POINTS = double.Parse(split[1]);
            pi.CRAFTING_LEVEL = double.Parse(split[2]);
            pi.CRAFTING_POINTS = double.Parse(split[3]);
            pi.CUI = bool.Parse(split[4]);
            pi.LAST_DEATH = double.Parse(split[5]);
            pi.MINING_LEVEL = double.Parse(split[6]);
            pi.MINING_POINTS = double.Parse(split[7]);
            pi.ENABLED = bool.Parse(split[8]);
            pi.SKINNING_LEVEL = double.Parse(split[9]);
            pi.SKINNING_POINTS = double.Parse(split[10]);
            pi.WOODCUTTING_LEVEL = double.Parse(split[11]);
            pi.WOODCUTTING_POINTS = double.Parse(split[12]);
            pi.XP_MULTIPLIER = double.Parse(split[13]);
            if (split.Length >= 16)
            {
                pi.FISHING_LEVEL = double.Parse(split[14]);
                pi.FISHING_POINTS = double.Parse(split[15]);
            }
            BasePlayer target = BasePlayer.FindByID(userid);
            if (target != null && target.IsConnected)
            {
                CreateGUI(target, pi);
            }
            return true;
        }

        #endregion API
    }
}