﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Zorro.UI.Effects;

namespace ItemInfoDisplay;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    private static GUIManager guiManager;
    private static TextMeshProUGUI itemInfoDisplayTextMesh;
    private static Dictionary<string, string> effectColors = new Dictionary<string, string>();
    private static float lastKnownSinceItemAttach;
    private static bool hasChanged;
    private static ConfigEntry<float> configFontSize;
    private static ConfigEntry<float> configOutlineWidth;
    private static ConfigEntry<float> configLineSpacing;
    private static ConfigEntry<float> configSizeDeltaX;
    private static ConfigEntry<float> configForceUpdateTime;

    private void Awake()
    {
        Log = Logger;
        InitEffectColors(effectColors);
        lastKnownSinceItemAttach = 0f;
        hasChanged = true;
        configFontSize = ((BaseUnityPlugin)this).Config.Bind<float>("ItemInfoDisplay", "Font Size", 20f, "Customize the Font Size for description text.");
        configOutlineWidth = ((BaseUnityPlugin)this).Config.Bind<float>("ItemInfoDisplay", "Outline Width", 0.08f, "Customize the Outline Width for item description text.");
        configLineSpacing = ((BaseUnityPlugin)this).Config.Bind<float>("ItemInfoDisplay", "Line Spacing", -35f, "Customize the Line Spacing for item description text.");
        configSizeDeltaX = ((BaseUnityPlugin)this).Config.Bind<float>("ItemInfoDisplay", "Size Delta X", 550f, "Customize the horizontal length of the container for the mod. Increasing moves text left, decreasing moves text right.");
        configForceUpdateTime = ((BaseUnityPlugin)this).Config.Bind<float>("ItemInfoDisplay", "Force Update Time", 1f, "Customize the time in seconds until the mod forces an update for the item.");
        Harmony.CreateAndPatchAll(typeof(ItemInfoDisplayUpdatePatch));
        Harmony.CreateAndPatchAll(typeof(ItemInfoDisplayEquipPatch));
        Harmony.CreateAndPatchAll(typeof(ItemInfoDisplayFinishCookingPatch));
        Harmony.CreateAndPatchAll(typeof(ItemInfoDisplayReduceUsesRPCPatch));
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    private static class ItemInfoDisplayUpdatePatch 
    {
        [HarmonyPatch(typeof(CharacterItems), "Update")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayUpdate(CharacterItems __instance)
        {
            try
            {
                if (guiManager == null)
                {
                    AddDisplayObject();
                }
                else
                {
                    if (Character.observedCharacter.data.currentItem != null)
                    {
                        if (hasChanged)
                        {
                            hasChanged = false;
                            ProcessItemGameObject();
                        }
                        else if (Mathf.Abs(Character.observedCharacter.data.sinceItemAttach - lastKnownSinceItemAttach) >= configForceUpdateTime.Value)
                        {
                            hasChanged = true;
                            lastKnownSinceItemAttach = Character.observedCharacter.data.sinceItemAttach;
                        }

                        if (!itemInfoDisplayTextMesh.gameObject.activeSelf)
                        {
                            itemInfoDisplayTextMesh.gameObject.SetActive(true);
                        }
                    }
                    else
                    {
                        if (itemInfoDisplayTextMesh.gameObject.activeSelf) 
                        {
                            itemInfoDisplayTextMesh.gameObject.SetActive(false);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError(e.Message + e.StackTrace);
            }
        }
    }

    private static class ItemInfoDisplayEquipPatch
    {
        [HarmonyPatch(typeof(CharacterItems), "Equip")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayEquip(CharacterItems __instance)
        {
            try
            {
                if (Character.ReferenceEquals(Character.observedCharacter, __instance.character))
                {
                    hasChanged = true;
                }
            }
            catch (Exception e)
            {
                Log.LogError(e.Message + e.StackTrace);
            }
        }
    }

    private static class ItemInfoDisplayFinishCookingPatch
    {
        [HarmonyPatch(typeof(ItemCooking), "FinishCooking")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayFinishCooking(ItemCooking __instance)
        {
            try
            {
                if (Character.ReferenceEquals(Character.observedCharacter, __instance.item.holderCharacter))
                {
                    hasChanged = true;
                }
            }
            catch (Exception e)
            {
                Log.LogError(e.Message + e.StackTrace);
            }
        }
    }

    private static class ItemInfoDisplayReduceUsesRPCPatch
    {
        [HarmonyPatch(typeof(Action_ReduceUses), "ReduceUsesRPC")]
        [HarmonyPostfix]
        private static void ItemInfoDisplayReduceUsesRPC(Action_ReduceUses __instance)
        {
            try
            {
                if (Character.ReferenceEquals(Character.observedCharacter, __instance.character))
                {
                    hasChanged = true;
                }
            }
            catch (Exception e)
            {
                Log.LogError(e.Message + e.StackTrace);
            }
        }
    }

    private static void ProcessItemGameObject()
    {
        Item item = Character.observedCharacter.data.currentItem; // not sure why this broke after THE MESA update, made no changes (just rebuilt)
        GameObject itemGameObj = item.gameObject;
        Component[] itemComponents = itemGameObj.GetComponents(typeof(Component));
        bool isConsumable = false;
        string prefixStatus = "";
        string suffixWeight = "";
        string suffixUses = "";
        string suffixCooked = "";
        string suffixAfflictions = "";
        itemInfoDisplayTextMesh.text = "";

        if (Ascents.itemWeightModifier > 0)
        {
            suffixWeight += effectColors["Weight"] + ((item.carryWeight + Ascents.itemWeightModifier) * 2.5f).ToString("F1").Replace(".0", "") + " 重量</color>";
        }
        else
        {
            suffixWeight += effectColors["Weight"] + (item.carryWeight * 2.5f).ToString("F1").Replace(".0", "") + " 重量</color>";
        }

        if (itemGameObj.name.Equals("Bugle(Clone)"))
        {
            itemInfoDisplayTextMesh.text += "大声喧哗吧\n";
        }
        else if (itemGameObj.name.Equals("Pirate Compass(Clone)"))
        {
            itemInfoDisplayTextMesh.text += effectColors["Injury"] + "指向</color>最近的行李\n";
        }
        else if (itemGameObj.name.Equals("Compass(Clone)"))
        {
            itemInfoDisplayTextMesh.text += effectColors["Injury"] + "指向</color>北方山顶\n";
        }
        else if (itemGameObj.name.Equals("Shell Big(Clone)"))
        {
            itemInfoDisplayTextMesh.text += "试试" + effectColors["Hunger"] + "扔向</color>椰子\n";
        }

        for (int i = 0; i < itemComponents.Length; i++)
        {
            if (itemComponents[i].GetType() == typeof(ItemUseFeedback))
            {
                ItemUseFeedback itemUseFeedback = (ItemUseFeedback)itemComponents[i];
                if (itemUseFeedback.useAnimation.Equals("Eat") || itemUseFeedback.useAnimation.Equals("Drink") || itemUseFeedback.useAnimation.Equals("Heal"))
                {
                    isConsumable = true;
                }
            }
            else if (itemComponents[i].GetType() == typeof(Action_Consume))
            {
                isConsumable = true;
            }
            else if (itemComponents[i].GetType() == typeof(Action_RestoreHunger))
            {
                Action_RestoreHunger effect = (Action_RestoreHunger)itemComponents[i];
                prefixStatus += ProcessEffect((effect.restorationAmount * -1f), "Hunger");
            }
            else if (itemComponents[i].GetType() == typeof(Action_GiveExtraStamina))
            {
                Action_GiveExtraStamina effect = (Action_GiveExtraStamina)itemComponents[i];
                prefixStatus += ProcessEffect(effect.amount, "Extra Stamina");
            }
            else if (itemComponents[i].GetType() == typeof(Action_InflictPoison))
            {
                Action_InflictPoison effect = (Action_InflictPoison)itemComponents[i];
                prefixStatus += "在" + effect.delay.ToString() + "秒后，" + ProcessEffectOverTime(effect.poisonPerSecond, 1f, effect.inflictionTime, "Poison");
            }
            else if (itemComponents[i].GetType() == typeof(Action_AddOrRemoveThorns))
            {
                Action_AddOrRemoveThorns effect = (Action_AddOrRemoveThorns)itemComponents[i];
                prefixStatus += ProcessEffect((effect.thornCount * 0.05f), "Thorns"); // TODO: Search for thorns amount per applied thorn
            }
            else if (itemComponents[i].GetType() == typeof(Action_ModifyStatus))
            {
                Action_ModifyStatus effect = (Action_ModifyStatus)itemComponents[i];
                prefixStatus += ProcessEffect(effect.changeAmount, effect.statusType.ToString());
            }
            else if (itemComponents[i].GetType() == typeof(Action_ApplyMassAffliction))
            {
                Action_ApplyMassAffliction effect = (Action_ApplyMassAffliction)itemComponents[i];
                suffixAfflictions += "<#CCCCCC>附近玩家会获得：</color>\n";
                suffixAfflictions += ProcessAffliction(effect.affliction);
                if (effect.extraAfflictions.Length > 0)
                {
                    for (int j = 0; j < effect.extraAfflictions.Length; j++)
                    {
                        if (suffixAfflictions.EndsWith('\n'))
                        {
                            suffixAfflictions = suffixAfflictions.Remove(suffixAfflictions.Length - 1);
                        }
                        suffixAfflictions += ",\n" + ProcessAffliction(effect.extraAfflictions[j]);
                    }
                }
            }
            else if (itemComponents[i].GetType() == typeof(Action_ApplyAffliction))
            {
                Action_ApplyAffliction effect = (Action_ApplyAffliction)itemComponents[i];
                suffixAfflictions += ProcessAffliction(effect.affliction);
            }
            else if (itemComponents[i].GetType() == typeof(Action_ClearAllStatus))
            {
                Action_ClearAllStatus effect = (Action_ClearAllStatus)itemComponents[i];
                itemInfoDisplayTextMesh.text += effectColors["ItemInfoDisplayPositive"] + "清除所有状态</color>";
                if (effect.excludeCurse)
                {
                    itemInfoDisplayTextMesh.text += " 但不包括" + effectColors["Curse"] + "诅咒</color>";
                }
                if (effect.otherExclusions.Count > 0)
                {
                    foreach (CharacterAfflictions.STATUSTYPE exclusion in effect.otherExclusions)
                    {
                        itemInfoDisplayTextMesh.text += ", " + effectColors[exclusion.ToString()] + exclusion.ToString().ToUpper() + "</color>";
                    }
                }
                itemInfoDisplayTextMesh.text = itemInfoDisplayTextMesh.text.Replace(", <#E13542>CRAB</color>", "") + "\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_ConsumeAndSpawn))
            {
                Action_ConsumeAndSpawn effect = (Action_ConsumeAndSpawn)itemComponents[i];
                if (effect.itemToSpawn.ToString().Contains("Peel"))
                {
                    itemInfoDisplayTextMesh.text += "<#CCCCCC>食用后获得果皮</color>\n";
                }
            }
            else if (itemComponents[i].GetType() == typeof(Action_ReduceUses))
            {
                OptionableIntItemData uses = (OptionableIntItemData)item.data.data[DataEntryKey.ItemUses];
                if (uses.HasData)
                {
                    if (uses.Value > 1)
                    {
                        suffixUses += "   " + uses.Value + " 次可用";
                    }
                }
            }
            else if (itemComponents[i].GetType() == typeof(Lantern))
            {
                Lantern lantern = (Lantern)itemComponents[i];
                if (itemGameObj.name.Equals("Torch(Clone)")){
                    itemInfoDisplayTextMesh.text += "可以点燃\n";
                }
                else {
                    suffixAfflictions += "<#CCCCCC>点燃后，附近玩家会获得：</color>\n";
                }

                if (itemGameObj.name.Equals("Lantern_Faerie(Clone)"))
                {
                    StatusField effect = itemGameObj.transform.Find("FaerieLantern/Light/Heat").GetComponent<StatusField>();
                    suffixAfflictions += ProcessEffectOverTime(effect.statusAmountPerSecond, 1f, lantern.startingFuel, effect.statusType.ToString());
                    foreach (StatusField.StatusFieldStatus status in effect.additionalStatuses)
                    {
                        if (suffixAfflictions.EndsWith('\n'))
                        {
                            suffixAfflictions = suffixAfflictions.Remove(suffixAfflictions.Length - 1);
                        }
                        suffixAfflictions += ",\n" + ProcessEffectOverTime(status.statusAmountPerSecond, 1f, lantern.startingFuel, status.statusType.ToString());
                    }
                }
                else if (itemGameObj.name.Equals("Lantern(Clone)"))
                {
                    StatusField effect = itemGameObj.transform.Find("GasLantern/Light/Heat").GetComponent<StatusField>();
                    suffixAfflictions += ProcessEffectOverTime(effect.statusAmountPerSecond, 1f, lantern.startingFuel, effect.statusType.ToString());
                }
            }
            else if (itemComponents[i].GetType() == typeof(Action_RaycastDart))
            {
                Action_RaycastDart effect = (Action_RaycastDart)itemComponents[i];
                isConsumable = true;
                suffixAfflictions += "<#CCCCCC>射出飞镖将施加：</color>\n";
                for (int j = 0; j < effect.afflictionsOnHit.Length; j++)
                {
                    suffixAfflictions += ProcessAffliction(effect.afflictionsOnHit[j]);
                    if (suffixAfflictions.EndsWith('\n'))
                    {
                        suffixAfflictions = suffixAfflictions.Remove(suffixAfflictions.Length - 1);
                    }
                    suffixAfflictions += ",\n";
                }
                if (suffixAfflictions.EndsWith('\n'))
                {
                    suffixAfflictions = suffixAfflictions.Remove(suffixAfflictions.Length - 2);
                }
                suffixAfflictions += "\n";
            }
            else if (itemComponents[i].GetType() == typeof(MagicBugle))
            {
                itemInfoDisplayTextMesh.text += "吹奏喇叭时，";
            }
            else if (itemComponents[i].GetType() == typeof(ClimbingSpikeComponent))
            {
                itemInfoDisplayTextMesh.text += "放置可抓取岩钉以" + effectColors["Extra Stamina"] + "恢复体力</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_Flare))
            {
                itemInfoDisplayTextMesh.text += "可以点燃\n";
            }
            else if (itemComponents[i].GetType() == typeof(Backpack))
            {
                itemInfoDisplayTextMesh.text += "丢下以放入物品\n";
            }
            else if (itemComponents[i].GetType() == typeof(BananaPeel))
            {
                itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "踩到</color>会滑倒\n";
            }
            else if (itemComponents[i].GetType() == typeof(Constructable))
            {
                Constructable effect = (Constructable)itemComponents[i];
                if (effect.constructedPrefab.name.Equals("PortableStovetop_Placed"))
                {
                    itemInfoDisplayTextMesh.text += "放置一个" + effectColors["Injury"] + "烹饪</color>炉，持续" + effect.constructedPrefab.GetComponent<Campfire>().burnsFor.ToString() + "秒\n";
                }
                else
                {
                    itemInfoDisplayTextMesh.text += "可放置\n";
                }
            }
            else if (itemComponents[i].GetType() == typeof(RopeSpool))
            {
                RopeSpool effect = (RopeSpool)itemComponents[i];
                if (effect.isAntiRope)
                {
                    itemInfoDisplayTextMesh.text += "放置一根向上漂浮的绳子\n";
                }
                else
                {
                    itemInfoDisplayTextMesh.text += "放置一根绳子\n";
                }
                itemInfoDisplayTextMesh.text += "长度从" + (effect.minSegments / 4f).ToString("F2").Replace(".0", "") + "米，最长" 
                    + (Rope.MaxSegments / 4f).ToString("F1").Replace(".0", "") + "米\n";
                //using force update here for remaining length since Rope has no character distinction for Detach_Rpc() hook, maybe unless OK with any player triggering this
                if (configForceUpdateTime.Value <= 1f)
                {
                    suffixUses += "   " + (effect.RopeFuel / 4f).ToString("F2").Replace(".00", "") + "米剩余";
                }
            }
            else if (itemComponents[i].GetType() == typeof(RopeShooter))
            {
                RopeShooter effect = (RopeShooter)itemComponents[i];
                itemInfoDisplayTextMesh.text += "发射绳锚以放置一根绳子，";
                if (effect.ropeAnchorWithRopePref.name.Equals("RopeAnchorForRopeShooterAnti"))
                {
                    itemInfoDisplayTextMesh.text += "向上漂浮 ";
                }
                else
                {
                    itemInfoDisplayTextMesh.text += "向下垂落 ";
                }
                itemInfoDisplayTextMesh.text += (effect.maxLength / 4f).ToString("F1").Replace(".0", "") + "米\n";
            }
            else if (itemComponents[i].GetType() == typeof(Antigrav))
            {
                Antigrav effect = (Antigrav)itemComponents[i];
                if (effect.intensity != 0f)
                {
                    suffixAfflictions += effectColors["Injury"] + "警告：</color><#CCCCCC>丢弃会飞走</color>\n";
                }
            }
            else if (itemComponents[i].GetType() == typeof(Action_Balloon))
            {
                suffixAfflictions += "可附着在角色上\n";
            }
            else if (itemComponents[i].GetType() == typeof(VineShooter))
            {
                VineShooter effect = (VineShooter)itemComponents[i];
                itemInfoDisplayTextMesh.text += "射出链条，将你的位置与目标连接，最远可达"
                    + (effect.maxLength / (5f / 3f)).ToString("F1").Replace(".0", "") + "米\n";
            }
            else if (itemComponents[i].GetType() == typeof(ShelfShroom))
            {
                ShelfShroom effect = (ShelfShroom)itemComponents[i];
                if (effect.instantiateOnBreak.name.Equals("HealingPuffShroomSpawn"))
                {
                    GameObject effect1 = effect.instantiateOnBreak.transform.Find("VFX_SporeHealingExplo").gameObject;
                    AOE effect1AOE = effect1.GetComponent<AOE>();
                    GameObject effect2 = effect1.transform.Find("VFX_SporePoisonExplo").gameObject;
                    AOE effect2AOE = effect2.GetComponent<AOE>();
                    AOE[] effect2AOEs = effect2.GetComponents<AOE>();
                    TimeEvent effect2TimeEvent = effect2.GetComponent<TimeEvent>();
                    RemoveAfterSeconds effect2RemoveAfterSeconds = effect2.GetComponent<RemoveAfterSeconds>();
                    itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "扔出</color>释放气体，将会：\n";
                    itemInfoDisplayTextMesh.text += ProcessEffect((Mathf.Round(effect1AOE.statusAmount * 0.9f * 40f) / 40f), effect1AOE.statusType.ToString()); // incorrect? calculates strangely so i somewhat manually adjusted the values
                    itemInfoDisplayTextMesh.text += ProcessEffectOverTime((Mathf.Round(effect2AOE.statusAmount * (1f / effect2TimeEvent.rate) * 40f) / 40f), 1f, effect2RemoveAfterSeconds.seconds, effect2AOE.statusType.ToString()); // incorrect?
                    if (effect2AOEs.Length > 1)
                    {
                        itemInfoDisplayTextMesh.text += ProcessEffectOverTime((Mathf.Round(effect2AOEs[1].statusAmount * (1f / effect2TimeEvent.rate) * 40f) / 40f), 1f, (effect2RemoveAfterSeconds.seconds + 1f), effect2AOEs[1].statusType.ToString()); // incorrect?
                    } // didn't handle dynamically because there were 2 poison removal AOEs but 1 doesn't seem to work or they are buggy in some way (probably time event rate)?
                }
                else if (effect.instantiateOnBreak.name.Equals("ShelfShroomSpawn"))
                {
                    itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "扔出</color>生成一个平台\n";
                }
                else if (effect.instantiateOnBreak.name.Equals("BounceShroomSpawn"))
                {
                    itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "扔出</color>生成一个弹跳垫\n";
                }
            }
            else if (itemComponents[i].GetType() == typeof(ScoutEffigy))
            {
                itemInfoDisplayTextMesh.text += effectColors["Extra Stamina"] + "复活</color>一名死亡玩家\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_Die))
            {
                itemInfoDisplayTextMesh.text += "使用后你会" + effectColors["Curse"] + "死亡</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_SpawnGuidebookPage))
            {
                isConsumable = true;
                itemInfoDisplayTextMesh.text += "可打开\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_Guidebook))
            {
                itemInfoDisplayTextMesh.text += "可阅读\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_CallScoutmaster))
            {
                itemInfoDisplayTextMesh.text += effectColors["Injury"] + "使用后违反规则0</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_MoraleBoost))
            {
                Action_MoraleBoost effect = (Action_MoraleBoost)itemComponents[i];
                if (effect.boostRadius < 0)
                {
                   itemInfoDisplayTextMesh.text += effectColors["ItemInfoDisplayPositive"] + "GAIN</color> " + effectColors["Extra Stamina"] + (effect.baselineStaminaBoost * 100f).ToString("F1").Replace(".0", "") + " 额外耐力</color>\n";
                }
                else if (effect.boostRadius > 0)
                {
                   itemInfoDisplayTextMesh.text += "<#CCCCCC>附近玩家</color>" + effectColors["ItemInfoDisplayPositive"] + " 获得</color> " + effectColors["Extra Stamina"] + (effect.baselineStaminaBoost * 100f).ToString("F1").Replace(".0", "") + " 额外耐力</color>\n";
                }
            }
            else if (itemComponents[i].GetType() == typeof(Breakable))
            {
                itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "扔出</color>砸开\n";
            }
            else if (itemComponents[i].GetType() == typeof(Bonkable))
            {
                itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "扔向头部" + effectColors["Injury"] + "敲击</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(MagicBean))
            {
                MagicBean effect = (MagicBean)itemComponents[i];
                itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "扔出</color>种植一根藤蔓，垂直于地形生长，最长"
                    + (effect.plantPrefab.maxLength / 2f).ToString("F1").Replace(".0", "") + "米或直到碰到障碍物\n";
            }
            else if (itemComponents[i].GetType() == typeof(BingBong))
            {
                itemInfoDisplayTextMesh.text += "Bingbong航空吉祥物\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_Passport))
            {
                itemInfoDisplayTextMesh.text += "打开以自定义角色\n";
            }
            else if (itemComponents[i].GetType() == typeof(Actions_Binoculars))
            {
                itemInfoDisplayTextMesh.text += "使用可看得更远\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_WarpToRandomPlayer))
            {
                itemInfoDisplayTextMesh.text += "传送到随机玩家处\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_WarpToBiome))
            {
                Action_WarpToBiome effect = (Action_WarpToBiome)itemComponents[i];
                itemInfoDisplayTextMesh.text += "传送到 " + effect.segmentToWarpTo.ToString().ToUpper() + "\n";
            }
            else if (itemComponents[i].GetType() == typeof(Parasol))
            {
                itemInfoDisplayTextMesh.text += "打开可减缓下落速度\n";
            }
            else if (itemComponents[i].GetType() == typeof(Frisbee))
            {
                itemInfoDisplayTextMesh.text += effectColors["Hunger"] + "把它扔出去</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(Action_ConstructableScoutCannonScroll))
            {
                itemInfoDisplayTextMesh.text += "\n<#CCCCCC>放置后点燃引信：</color>\n将桶中的侦察员发射出去\n";
                    //+ "\n<#CCCCCC>LIMITS GRAVITATIONAL ACCELERATION\n(PREVENTS OR LOWERS FALL DAMAGE)</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(Dynamite))
            {
                Dynamite effect = (Dynamite)itemComponents[i];
                itemInfoDisplayTextMesh.text += effectColors["Injury"] + "爆炸</color>造成最高" + effectColors["Injury"] 
                    + (effect.explosionPrefab.GetComponent<AOE>().statusAmount * 100f).ToString("F1").Replace(".0", "") + "点伤害</color>\n<#CCCCCC>手持时造成额外伤害</color>\n";
            }
            else if (itemComponents[i].GetType() == typeof(Scorpion))
            {
                Scorpion effect = (Scorpion)itemComponents[i]; // wanted to hide poison info if dead, but mob state does not immediately update on equip leading to visual bug
                if (configForceUpdateTime.Value <= 1f)
                {
                    float effectPoison = Mathf.Max(0.5f, (1f - item.holderCharacter.refs.afflictions.statusSum + 0.05f)) * 100f; // v.1.23.a BASED ON Scorpion.InflictAttack
                    itemInfoDisplayTextMesh.text += "活着时会" + effectColors["Poison"] + "蜇你</color>\n" + effectColors["Curse"] + "烹饪后死亡</color>\n"
                        + "<#CCCCCC>下次蜇伤将造成：</color>\n" + effectColors["Poison"] + effectPoison.ToString("F1").Replace(".0", "") + "点毒素</color>，持续" 
                        + effect.totalPoisonTime.ToString("F1").Replace(".0", "") + "秒\n<#CCCCCC>（越健康伤害越高）</color>\n";
                }
                else
                {
                    itemInfoDisplayTextMesh.text += "活着时会" + effectColors["Poison"] + "蜇你</color>\n" + effectColors["Curse"] 
                        + "烹饪后死亡</color>\n\n" + "<#CCCCCC>下次蜇伤将造成：</color>\n至少"
                        + effectColors["Poison"] + "50点毒素</color>，持续" + effect.totalPoisonTime.ToString("F1").Replace(".0", "") + "秒\n至多"
                        + effectColors["Poison"] + "105点毒素</color>，持续" + effect.totalPoisonTime.ToString("F1").Replace(".0", "")
                        + "秒\n<#CCCCCC>（越健康伤害越高）</color>\n";
                }
            }
            else if (itemComponents[i].GetType() == typeof(Action_Spawn))
            {
                Action_Spawn effect = (Action_Spawn)itemComponents[i];
                if (effect.objectToSpawn.name.Equals("VFX_Sunscreen"))
                {
                    AOE effectAOE = effect.objectToSpawn.transform.Find("AOE").GetComponent<AOE>();
                    RemoveAfterSeconds effectTime = effect.objectToSpawn.transform.Find("AOE").GetComponent<RemoveAfterSeconds>();
                    itemInfoDisplayTextMesh.text += "<#CCCCCC>喷洒" + effectTime.seconds.ToString("F1").Replace(".0", "") + "秒雾气，效果为：</color>\n"
                        + ProcessAffliction(effectAOE.affliction);                
                }
            }
            else if (itemComponents[i].GetType() == typeof(CactusBall))
            {
                CactusBall effect = (CactusBall)itemComponents[i];
                itemInfoDisplayTextMesh.text += effectColors["Thorns"] + "粘在你身上</color>\n\n可通过" + effectColors["Hunger"] 
                    + "使用</color>扔出，至少蓄力" + (effect.throwChargeRequirement * 100f).ToString("F1").Replace(".0", "") + "%\n";
            }
            else if (itemComponents[i].GetType() == typeof(BingBongShieldWhileHolding))
            {
                itemInfoDisplayTextMesh.text += "<#CCCCCC>装备时获得：</color>\n" + effectColors["Shield"] + "护盾</color>（无敌）\n";
            }
            else if (itemComponents[i].GetType() == typeof(ItemCooking))
            {
                ItemCooking itemCooking = (ItemCooking)itemComponents[i];
                if (itemCooking.wreckWhenCooked && (itemCooking.timesCookedLocal >= 1))
                {
                    suffixCooked += "\n" + effectColors["Curse"] + "烹饪后破碎</color>";
                }
                else if (itemCooking.wreckWhenCooked)
                {
                    suffixCooked += "\n" + effectColors["Curse"] + "烹饪后会破碎</color>";
                }
                else if (itemCooking.timesCookedLocal >= ItemCooking.COOKING_MAX)
                {
                    suffixCooked += "   " + effectColors["Curse"] + itemCooking.timesCookedLocal.ToString() + "次烹饪\n无法再次烹饪</color>";
                }
                else if (itemCooking.timesCookedLocal == 0)
                {
                    suffixCooked += "\n" + effectColors["Extra Stamina"] + "可以烹饪</color>";
                }
                else if (itemCooking.timesCookedLocal == 1)
                {
                    suffixCooked += "   " + effectColors["Extra Stamina"] + itemCooking.timesCookedLocal.ToString() + "次烹饪</color>\n" + effectColors["Hunger"] + "可以烹饪</color>";
                }
                else if (itemCooking.timesCookedLocal == 2)
                {
                    suffixCooked += "   " + effectColors["Hunger"] + itemCooking.timesCookedLocal.ToString() + "次烹饪</color>\n" + effectColors["Injury"] + "可以烹饪</color>";
                }
                else if (itemCooking.timesCookedLocal == 3)
                {
                    suffixCooked += "   " + effectColors["Injury"] + itemCooking.timesCookedLocal.ToString() + "次烹饪</color>\n" + effectColors["Poison"] + "可以烹饪</color>";
                }
                else if (itemCooking.timesCookedLocal >= 4)
                {
                    suffixCooked += "   " + effectColors["Poison"] + itemCooking.timesCookedLocal.ToString() + "次烹饪\n可以烹饪</color>";
                }
            }
        }

        if ((prefixStatus.Length > 0) && isConsumable)
        {
            itemInfoDisplayTextMesh.text = prefixStatus + "\n" + itemInfoDisplayTextMesh.text;
        }
        if (suffixAfflictions.Length > 0)
        {
            itemInfoDisplayTextMesh.text += "\n" + suffixAfflictions;
        }
        itemInfoDisplayTextMesh.text += "\n" + suffixWeight + suffixUses + suffixCooked;
        itemInfoDisplayTextMesh.text = itemInfoDisplayTextMesh.text.Replace("\n\n\n", "\n\n");
    }

    private static string ProcessEffect(float amount, string effect)
    {
        string result = "";

        if (amount == 0)
        {
            return result;
        }
        else if (amount > 0)
        {
            if (effect.Equals("Extra Stamina"))
            {
                result += effectColors["ItemInfoDisplayPositive"];
            }
            else
            {
                result += effectColors["ItemInfoDisplayNegative"];
            }
            result += "获得</color> ";
        }
        else if (amount < 0)
        {
            if (effect.Equals("Extra Stamina"))
            {
                result += effectColors["ItemInfoDisplayNegative"];
            }
            else
            {
                result += effectColors["ItemInfoDisplayPositive"];
            }
            result += "移除</color> ";
        }
        result += effectColors[effect] + (Mathf.Abs(amount) * 100f).ToString("F1").Replace(".0", "") + " " + GetChineseEffectName(effect) + "</color>\n";

        return result;
    }

    private static string ProcessEffectOverTime(float amountPerSecond, float rate, float time, string effect)
    {
        string result = "";

        if ((amountPerSecond == 0) || (time == 0))
        {
            return result;
        }
        else if (amountPerSecond > 0)
        {
            if (effect.Equals("Extra Stamina"))
            {
                result += effectColors["ItemInfoDisplayPositive"];
            }
            else
            {
                result += effectColors["ItemInfoDisplayNegative"];
            }
            result += "获得</color> ";
        }
        else if (amountPerSecond < 0)
        {
            if (effect.Equals("Extra Stamina"))
            {
                result += effectColors["ItemInfoDisplayNegative"];
            }
            else
            {
                result += effectColors["ItemInfoDisplayPositive"];
            }
            result += "移除</color> ";
        }
        result += effectColors[effect] + ((Mathf.Abs(amountPerSecond) * time * (1 / rate)) * 100f).ToString("F1").Replace(".0", "") + " " + GetChineseEffectName(effect) + "</color>，持续 " + time.ToString() + "秒\n";

        return result;
    }

    private static string ProcessAffliction(Peak.Afflictions.Affliction affliction)
    {
        string result = "";

        if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.FasterBoi)
        {
            Peak.Afflictions.Affliction_FasterBoi effect = (Peak.Afflictions.Affliction_FasterBoi)affliction;
            result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " + (effect.totalTime + effect.climbDelay).ToString("F1").Replace(".0", "") + "秒"
                + effectColors["Extra Stamina"] + Mathf.Round(effect.moveSpeedMod * 100f).ToString("F1").Replace(".0", "") + "%奔跑速度加成</color> 或\n"
                + effectColors["ItemInfoDisplayPositive"] + "获得</color> " + effect.totalTime.ToString("F1").Replace(".0", "") + "秒" + effectColors["Extra Stamina"] 
                + Mathf.Round(effect.climbSpeedMod * 100f).ToString("F1").Replace(".0", "") + "%攀爬速度加成</color>\n之后，" + effectColors["ItemInfoDisplayNegative"] 
                + "获得</color> " + effectColors["Drowsy"] + (effect.drowsyOnEnd * 100f).ToString("F1").Replace(".0", "") + "点困倦</color>\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.ClearAllStatus)
        {
            Peak.Afflictions.Affliction_ClearAllStatus effect = (Peak.Afflictions.Affliction_ClearAllStatus)affliction;
            result += effectColors["ItemInfoDisplayPositive"] + "清除所有状态</color>";
            if (effect.excludeCurse)
            {
                result += " 但不包括" + effectColors["Curse"] + "诅咒</color>";
            }
            result += "\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.AddBonusStamina)
        {
            Peak.Afflictions.Affliction_AddBonusStamina effect = (Peak.Afflictions.Affliction_AddBonusStamina)affliction;
            result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " + effectColors["Extra Stamina"] + (effect.staminaAmount * 100f).ToString("F1").Replace(".0", "") + "点额外体力</color>\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.InfiniteStamina)
        {
            Peak.Afflictions.Affliction_InfiniteStamina effect = (Peak.Afflictions.Affliction_InfiniteStamina)affliction;
            if (effect.climbDelay > 0)
            {
                result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " + (effect.totalTime + effect.climbDelay).ToString("F1").Replace(".0", "") + "秒"
                    + effectColors["Extra Stamina"] + "无限奔跑体力</color> 或\n" + effectColors["ItemInfoDisplayPositive"] + "获得</color> " 
                    + effect.totalTime.ToString("F1").Replace(".0", "") + "秒" + effectColors["Extra Stamina"] + "无限攀爬体力</color>\n";
            }
            else
            {
                result += effectColors["ItemInfoDisplayPositive"] + "获得</color> " + (effect.totalTime).ToString("F1").Replace(".0", "") + "秒" + effectColors["Extra Stamina"] + "无限体力\n";
            }
            if (effect.drowsyAffliction != null)
            {
                result += "之后，" + ProcessAffliction(effect.drowsyAffliction);
            }
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.AdjustStatus)
        {
            Peak.Afflictions.Affliction_AdjustStatus effect = (Peak.Afflictions.Affliction_AdjustStatus)affliction;
            if (effect.statusAmount > 0)
            {
                if (effect.Equals("Extra Stamina"))
                {
                    result += effectColors["ItemInfoDisplayPositive"];
                }
                else
                {
                    result += effectColors["ItemInfoDisplayNegative"];
                }
                result += "获得</color> ";
            }
            else
            {
                if (effect.Equals("Extra Stamina"))
                {
                    result += effectColors["ItemInfoDisplayNegative"];
                }
                else
                {
                    result += effectColors["ItemInfoDisplayPositive"];
                }
                result += "移除</color> ";
            }
            result += effectColors[effect.statusType.ToString()] + (Mathf.Abs(effect.statusAmount) * 100f).ToString("F1").Replace(".0", "")
                + " " + GetChineseEffectName(effect.statusType.ToString()) + "</color>\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.DrowsyOverTime)
        {
            Peak.Afflictions.Affliction_AdjustDrowsyOverTime effect = (Peak.Afflictions.Affliction_AdjustDrowsyOverTime)affliction; // 1.6.a
            if (effect.statusPerSecond > 0)
            {
                result += effectColors["ItemInfoDisplayNegative"] + "获得</color> ";
            }
            else
            {
                result += effectColors["ItemInfoDisplayPositive"] + "移除</color> ";
            }
            result += effectColors["Drowsy"] + (Mathf.Round((Mathf.Abs(effect.statusPerSecond) * effect.totalTime * 100f) * 0.4f) / 0.4f).ToString("F1").Replace(".0", "")
                + "点困倦</color>，持续" + effect.totalTime.ToString("F1").Replace(".0", "") + "秒\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.ColdOverTime)
        {
            Peak.Afflictions.Affliction_AdjustColdOverTime effect = (Peak.Afflictions.Affliction_AdjustColdOverTime)affliction; // 1.6.a
            if (effect.statusPerSecond > 0)
            {
                result += effectColors["ItemInfoDisplayNegative"] + "获得</color> ";
            }
            else
            {
                result += effectColors["ItemInfoDisplayPositive"] + "移除</color> ";
            }
            result += effectColors["Cold"] + (Mathf.Abs(effect.statusPerSecond) * effect.totalTime * 100f).ToString("F1").Replace(".0", "")
                + "点寒冷</color>，持续" + effect.totalTime.ToString("F1").Replace(".0", "") + "秒\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.Chaos)
        {
            result += effectColors["ItemInfoDisplayPositive"] + "清除所有状态</color>，然后随机化\n" + effectColors["Hunger"] + "饥饿</color>，"
                + effectColors["Extra Stamina"] + "额外体力</color>，" + effectColors["Injury"] + "受伤</color>，\n" + effectColors["Poison"] + "中毒</color>，"
                + effectColors["Cold"] + "寒冷</color>，" + effectColors["Hot"] + "热量</color>，" + effectColors["Drowsy"] + "困倦</color>\n";
        }
        else if (affliction.GetAfflictionType() is Peak.Afflictions.Affliction.AfflictionType.Sunscreen)
        {
            Peak.Afflictions.Affliction_Sunscreen effect = (Peak.Afflictions.Affliction_Sunscreen)affliction;
            result += "在MESA阳光下防止过热，持续" + effect.totalTime.ToString("F1").Replace(".0", "") + "秒\n";
        }

        return result;
    }

    private static string GetChineseEffectName(string effect)
    {
        switch (effect)
        {
            case "Hunger": return "饥饿";
            case "Extra Stamina": return "额外体力";
            case "Injury": return "受伤";
            case "Crab": return "螃蟹";
            case "Poison": return "中毒";
            case "Cold": return "寒冷";
            case "Heat":
            case "Hot": return "热量";
            case "Sleepy":
            case "Drowsy": return "困倦";
            case "Curse": return "诅咒";
            case "Weight": return "重量";
            case "Thorns": return "荆棘";
            case "Shield": return "护盾";
            default: return effect;
        }
    }

    private static void AddDisplayObject()
    {
        GameObject guiManagerGameObj = GameObject.Find("GAME/GUIManager");
        guiManager = guiManagerGameObj.GetComponent<GUIManager>();
        TMPro.TMP_FontAsset font = guiManager.heroDayText.font;

        GameObject invGameObj = guiManagerGameObj.transform.Find("Canvas_HUD/Prompts/ItemPromptLayout").gameObject;
        GameObject itemInfoDisplayGameObj = new GameObject("ItemInfoDisplay");
        itemInfoDisplayGameObj.transform.SetParent(invGameObj.transform);
        itemInfoDisplayTextMesh = itemInfoDisplayGameObj.AddComponent<TextMeshProUGUI>();
        RectTransform itemInfoDisplayRect = itemInfoDisplayGameObj.GetComponent<RectTransform>();

        itemInfoDisplayRect.sizeDelta = new Vector2(configSizeDeltaX.Value, 0f); // Y is 0, otherwise moves other item prompts
        itemInfoDisplayTextMesh.font = font;
        itemInfoDisplayTextMesh.fontSize = configFontSize.Value;
        itemInfoDisplayTextMesh.alignment = TextAlignmentOptions.BottomLeft;
        itemInfoDisplayTextMesh.lineSpacing = configLineSpacing.Value;
        itemInfoDisplayTextMesh.text = "";
        itemInfoDisplayTextMesh.outlineWidth = configOutlineWidth.Value;
    }
    private static void InitEffectColors(Dictionary<string, string> dict)
    {
        dict.Add("Hunger", "<#FFBD16>");
        dict.Add("Extra Stamina", "<#BFEC1B>");
        dict.Add("Injury", "<#FF5300>");
        dict.Add("Crab", "<#E13542>");
        dict.Add("Poison", "<#A139FF>");
        dict.Add("Cold", "<#00BCFF>");
        dict.Add("Heat", "<#C80918>");
        dict.Add("Hot", "<#C80918>");
        dict.Add("Sleepy", "<#FF5CA4>");
        dict.Add("Drowsy", "<#FF5CA4>");
        dict.Add("Curse", "<#1B0043>");
        dict.Add("Weight", "<#A65A1C>");
        dict.Add("Thorns", "<#768E00>");
        dict.Add("Shield", "<#D48E00>");

        dict.Add("ItemInfoDisplayPositive", "<#DDFFDD>");
        dict.Add("ItemInfoDisplayNegative", "<#FFCCCC>");
    }
}