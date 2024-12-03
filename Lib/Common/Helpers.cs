using ACE.Entity;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common.Spells;
using ACE.Server.Factories.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ACE.Mods.Legend.Lib.Common.Constants;

namespace ACE.Mods.Legend.Lib.Common
{
    public static class Helpers
    {
        public static Position LocToPosition(string location)
        {
            var parameters = location.Split(' ');
            uint cell;

            if (parameters[0].StartsWith("0x"))
            {
                string strippedcell = parameters[0].Substring(2);
                cell = (uint)int.Parse(strippedcell, System.Globalization.NumberStyles.HexNumber);
            }
            else
                cell = (uint)int.Parse(parameters[0], System.Globalization.NumberStyles.HexNumber);

            var positionData = new float[7];
            for (uint i = 0u; i < 7u; i++)
            {
                if (i > 2 && parameters.Length < 8)
                {
                    positionData[3] = 1;
                    positionData[4] = 0;
                    positionData[5] = 0;
                    positionData[6] = 0;
                    break;
                }

                if (!float.TryParse(parameters[i + 1].Trim(new char[] { ' ', '[', ']' }), out var position))
                    throw new Exception();

                positionData[i] = position;
            }

            return new Position(cell, positionData[0], positionData[1], positionData[2], positionData[4], positionData[5], positionData[6], positionData[3]);
        }

        public static string BuildItemInfo(WorldObject wo)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"{wo.NameWithMaterial}");

            var weaponType = wo.GetProperty(Entity.Enum.Properties.PropertyInt.WeaponType);
            if (weaponType.HasValue && weaponType.Value > 0)
            {
                sb.Append(", ");
                if (Dictionaries.MasteryInfo.ContainsKey(weaponType.Value))
                    sb.Append($"({Dictionaries.MasteryInfo[weaponType.Value]})");
                else
                    sb.Append("(Unknown mastery)");
            }

            var equipSet = wo.GetProperty(Entity.Enum.Properties.PropertyInt.EquipmentSetId);
            if (equipSet.HasValue && equipSet.Value > 0)
            {
                sb.Append(", ");
                if (Dictionaries.AttributeSetInfo.ContainsKey(equipSet.Value))
                    sb.Append(Dictionaries.AttributeSetInfo[equipSet.Value]);
                else
                    sb.Append($"Unknown set {equipSet.Value}");
            }

            var armorLevel = wo.GetProperty(Entity.Enum.Properties.PropertyInt.ArmorLevel);
            if (armorLevel.HasValue && armorLevel.Value > 0)
                sb.Append($", AL {armorLevel.Value}");

            var imbued = wo.GetProperty(Entity.Enum.Properties.PropertyInt.ImbuedEffect);
            if (imbued.HasValue && imbued.Value > 0)
            {
                sb.Append(",");
                if ((imbued.Value & 1) == 1) sb.Append(" CS");
                if ((imbued.Value & 2) == 2) sb.Append(" CB");
                if ((imbued.Value & 4) == 4) sb.Append(" AR");
                if ((imbued.Value & 8) == 8) sb.Append(" SlashRend");
                if ((imbued.Value & 16) == 16) sb.Append(" PierceRend");
                if ((imbued.Value & 32) == 32) sb.Append(" BludgeRend");
                if ((imbued.Value & 64) == 64) sb.Append(" AcidRend");
                if ((imbued.Value & 128) == 128) sb.Append(" FrostRend");
                if ((imbued.Value & 256) == 256) sb.Append(" LightRend");
                if ((imbued.Value & 512) == 512) sb.Append(" FireRend");
                if ((imbued.Value & 1024) == 1024) sb.Append(" MeleeImbue");
                if ((imbued.Value & 4096) == 4096) sb.Append(" MagicImbue");
                if ((imbued.Value & 8192) == 8192) sb.Append(" Hematited");
                if ((imbued.Value & 536870912) == 536870912) sb.Append(" MagicAbsorb");
            }


            var numberOfTinks = wo.GetProperty(Entity.Enum.Properties.PropertyInt.NumTimesTinkered);
            if (numberOfTinks.HasValue && numberOfTinks.Value > 0)
                sb.Append($", Tinks {numberOfTinks.Value}");

            var maxDamage = wo.GetProperty(Entity.Enum.Properties.PropertyInt.Damage);
            var variance = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.DamageVariance);

            if (weaponType.HasValue && maxDamage.HasValue && maxDamage.Value > 0 && variance.HasValue && variance.Value > 0)
            {
                sb.Append($", {((maxDamage.Value) - (maxDamage.Value * variance.Value)).ToString("N2")}-{maxDamage.Value}");
            }
            else if (maxDamage.HasValue && maxDamage != 0 && variance == 0)
            {
                sb.Append($", {maxDamage.Value}");
            }

            var elemBonus = wo.GetProperty(Entity.Enum.Properties.PropertyInt.ElementalDamageBonus);
            if (elemBonus.HasValue && elemBonus.Value > 0)
                sb.Append($", {elemBonus.Value}");

            var damageMod = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.DamageMod);
            if (damageMod.HasValue && damageMod.Value != 1)
                sb.Append($", {Math.Round((damageMod.Value - 1) * 100)}%");

            var eleDamageMod = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.ElementalDamageMod);
            if (eleDamageMod.HasValue && eleDamageMod.Value != 1)
                sb.Append($", {Math.Round((eleDamageMod.Value - 1) * 100)}%vs. Monsters");

            var attackBonus = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.WeaponOffense);
            if (attackBonus.HasValue && attackBonus.Value != 1)
                sb.Append($", {Math.Round((attackBonus.Value - 1) * 100)}%a");

            var meleeBonus = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.WeaponDefense);
            if (meleeBonus.HasValue && meleeBonus.Value != 1)
                sb.Append($", {Math.Round((meleeBonus.Value - 1) * 100)}%md");

            var magicBonus = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.WeaponMagicDefense);
            if (magicBonus.HasValue && magicBonus.Value != 1)
                sb.Append($", {Math.Round((magicBonus.Value - 1) * 100)}%mgc.d");

            var missileBonus = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.WeaponMissileDefense);
            if (missileBonus.HasValue && missileBonus.Value != 1)
                sb.Append($", {Math.Round((missileBonus.Value - 1) * 100)}%msl.d");

            var manacBonus = wo.GetProperty(Entity.Enum.Properties.PropertyFloat.ManaConversionMod);
            if (manacBonus.HasValue && manacBonus.Value != 1)
                sb.Append($", {Math.Round((manacBonus.Value) * 100)}%mc");

            List<int> spells = wo.Biota.GetKnownSpellsIds(wo.BiotaDatabaseLock);

            if (spells.Count > 0)
            {
                spells.Sort();
                spells.Reverse();

                foreach (int spell in spells)
                {
                    var spellById = SpellTools.GetSpell(spell);

                    // If the item is not loot generated, show all spells
                    var material = wo.GetProperty(Entity.Enum.Properties.PropertyInt.MaterialType);
                    if (material == null)
                        goto ShowSpell;

                    // Always show Minor/Major/Epic Impen
                    if (spellById.Name.Contains("Minor Impenetrability") || spellById.Name.Contains("Major Impenetrability") || spellById.Name.Contains("Epic Impenetrability") || spellById.Name.Contains("Legendary Impenetrability"))
                        goto ShowSpell;

                    // Always show trinket spells
                    if (spellById.Name.Contains("Augmented"))
                        goto ShowSpell;

                    var resistMagic = wo.GetProperty(Entity.Enum.Properties.PropertyInt.ResistMagic);
                    if (resistMagic.HasValue && resistMagic.Value >= 9999)
                    {
                        // Show banes and impen on unenchantable equipment
                        if (spellById.Name.Contains(" Bane") || spellById.Name.Contains("Impen") || spellById.Name.StartsWith("Brogard"))
                            goto ShowSpell;
                    }
                    else
                    {
                        // Hide banes and impen on enchantable equipment
                        if (spellById.Name.Contains(" Bane") || spellById.Name.Contains("Impen") || spellById.Name.StartsWith("Brogard"))
                            continue;
                    }

                    if ((spellById.Family >= 152 && spellById.Family <= 158) || spellById.Family == 195 || spellById.Family == 325)
                    {
                        // This is a weapon buff

                        // Lvl 6
                        if (spellById.Difficulty == 250)
                            continue;

                        // Lvl 7
                        if (spellById.Difficulty == 300)
                            goto ShowSpell;

                        // Lvl 8+
                        if (spellById.Difficulty >= 400)
                            goto ShowSpell;

                        continue;
                    }

                    // This is not a weapon buff.

                    // Filter all 1-5 spells
                    if (spellById.Name.EndsWith(" I") || spellById.Name.EndsWith(" II") || spellById.Name.EndsWith(" III") || spellById.Name.EndsWith(" IV") || spellById.Name.EndsWith(" V"))
                        continue;

                    // Filter 6's
                    if (spellById.Name.EndsWith(" VI"))
                        continue;

                    // Filter 7's
                    if (spellById.Difficulty == 300)
                        continue;

                    // Filter 8's
                    if (spellById.Name.Contains("Incantation"))
                        continue;

                    ShowSpell:

                    sb.Append(", " + spellById.Name);
                }
            }

            var weaponSkill = wo.GetProperty(Entity.Enum.Properties.PropertyInt.WeaponSkill);
            var wieldDiff = wo.GetProperty(Entity.Enum.Properties.PropertyInt.WieldDifficulty);
            if (!weaponSkill.HasValue || weaponSkill.Value == (int)Skill.None)
            {
                var wieldLevelReq = wo.GetProperty(Entity.Enum.Properties.PropertyInt.WieldRequirements);
                if (wieldLevelReq.HasValue && wieldLevelReq.Value == (int)WieldRequirement.Level && wieldDiff.HasValue && wieldDiff.Value > 0)
                {
                    sb.Append($", Wield Lvl {wieldDiff.Value}");
                }
            } else
            {
                if (Dictionaries.SkillInfo.ContainsKey(weaponSkill.Value) && wieldDiff.HasValue)
                    sb.Append($", {Dictionaries.SkillInfo[weaponSkill.Value]} {wieldDiff.Value}");
                else
                    sb.Append($", UnknownSkill: {weaponSkill.Value}");
            }

            var useReqLevel = wo.GetProperty(Entity.Enum.Properties.PropertyInt.UseRequiresLevel);

            if (useReqLevel.HasValue && useReqLevel.Value > 0)
            {
                sb.Append($", Lvl {useReqLevel.Value}");
            }

            var itemSkillLevelLimit = wo.GetProperty(Entity.Enum.Properties.PropertyInt.ItemSkillLevelLimit);
            var itemSkillLimit = wo.ItemSkillLimit;

            if (itemSkillLimit.HasValue && itemSkillLevelLimit.HasValue)
            {
                if (Dictionaries.SkillInfo.ContainsKey((int)itemSkillLimit.Value))
                    sb.Append($", {Dictionaries.SkillInfo[(int)itemSkillLimit.Value]} {itemSkillLevelLimit.Value}");
                else
                    sb.Append($", Unknown skill{itemSkillLimit.Value}");
            }


            var itemDiff = wo.GetProperty(Entity.Enum.Properties.PropertyInt.ItemDifficulty);

            if (itemDiff.HasValue && itemDiff.Value > 0)
            {
                sb.Append($", Diff {itemDiff.Value}");
            }

            if (wo.ItemType == ItemType.TinkeringMaterial && wo.Workmanship.HasValue)
            {
                sb.Append($", Work {wo.Workmanship.Value.ToString("N2")}");
            }
            else
            {
                if (wo.ItemWorkmanship > 0 && wo.NumTimesTinkered != 10)
                    sb.Append($", Craft {wo.Workmanship}");
            }

            if (wo.Value > 0)
                sb.Append($", Value {wo.Value:n0}");

            if (wo.EncumbranceVal > 0)
                sb.Append($", BU {wo.EncumbranceVal:n0}");

            return sb.ToString();
        }
    }
}
