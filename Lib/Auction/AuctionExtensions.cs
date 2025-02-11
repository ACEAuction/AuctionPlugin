using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Common;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Server.Network.GameMessages.Messages;
using static ACE.Server.WorldObjects.Player;
using ACE.Entity;
using ACE.Mods.Legend.Lib.Common.Spells;
using ACE.Mods.Legend.Lib.Auction.Network;

namespace ACE.Mods.Legend.Lib.Auction;

public static class AuctionExtensions
{
    private const string AuctionPrefix = "[AuctionHouse]";

    public static void SendAuctionMessage(this Player player, string message, ChatMessageType messageType = ChatMessageType.System)
    {
        player.Session.Network.EnqueueSend(new GameMessageSystemChat($"{AuctionPrefix} {message}", messageType));
    }

    /// <summary>
    /// This is called when a mail item is sent, to trigger the client to fetch updates list of mail items
    /// </summary>
    /// <param name="player"></param>
    public static void SendMailNotification(this Player player)
    {
        var response = new JsonResponse<object>(data: null, success: true);
        player.Session.Network.EnqueueSend(new GameMessageInboxNotificationResponse(response));
    }

    public static bool HasItemOnPerson(this Player player, uint itemId, out WorldObject foundItem)
    {
        foundItem = player.FindObject(itemId, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);
        return foundItem != null;
    }

    public static void RemoveItemForTransfer(this Player player, uint itemToTransfer, out WorldObject itemToRemove, int? amount = null)
    {
        if (player.IsBusy || player.Teleporting || player.suicideInProgress)
            throw new AuctionFailure($"The item cannot be transferred, you are too busy", FailureCode.Auction.TransferItemFailure);

        var item = player.FindObject(itemToTransfer, SearchLocations.MyInventory | SearchLocations.MyEquippedItems, out var itemFoundInContainer, out var itemRootOwner, out var itemWasEquipped);

        if (item == null)
            throw new AuctionFailure($"The item cannot be transferred, item with Id = {itemToTransfer} was not found on your person", FailureCode.Auction.TransferItemFailure);

        if (item.IsAttunedOrContainsAttuned)
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial} is attuned", FailureCode.Auction.TransferItemFailure);

        if (player.IsTrading && item.IsBeingTradedOrContainsItemBeingTraded(player.ItemsInTradeWindow))
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial}, the item is currently being traded", FailureCode.Auction.TransferItemFailure);

        var removeAmount = amount.HasValue ? amount.Value : item.StackSize ?? 1;

        if (!player.RemoveItemForGive(item, itemFoundInContainer, itemWasEquipped, itemRootOwner, removeAmount, out WorldObject itemToGive))
            throw new AuctionFailure($"The item cannot be transferred {item.NameWithMaterial}, failed to remove item from location", FailureCode.Auction.TransferItemFailure);

        itemToGive.SaveBiotaToDatabase();

        itemToRemove = itemToGive;
    }

    public static Position LocToPosition(this Position pos, string location)
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

    public static string BuildItemInfo(this WorldObject wo)
    {
        StringBuilder sb = new StringBuilder();

        sb.Append($"{wo.NameWithMaterial}");

        var weaponType = wo.GetProperty(Entity.Enum.Properties.PropertyInt.WeaponType);
        if (weaponType.HasValue && weaponType.Value > 0)
        {
            sb.Append(", ");
            if (Constants.Dictionaries.MasteryInfo.ContainsKey(weaponType.Value))
                sb.Append($"({Constants.Dictionaries.MasteryInfo[weaponType.Value]})");
            else
                sb.Append("(Unknown mastery)");
        }

        var equipSet = wo.GetProperty(Entity.Enum.Properties.PropertyInt.EquipmentSetId);
        if (equipSet.HasValue && equipSet.Value > 0)
        {
            sb.Append(", ");
            if (Constants.Dictionaries.AttributeSetInfo.ContainsKey(equipSet.Value))
                sb.Append(Constants.Dictionaries.AttributeSetInfo[equipSet.Value]);
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
        }
        else
        {
            if (Constants.Dictionaries.SkillInfo.ContainsKey(weaponSkill.Value) && wieldDiff.HasValue)
                sb.Append($", {Constants.Dictionaries.SkillInfo[weaponSkill.Value]} {wieldDiff.Value}");
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
            if (Constants.Dictionaries.SkillInfo.ContainsKey((int)itemSkillLimit.Value))
                sb.Append($", {Constants.Dictionaries.SkillInfo[(int)itemSkillLimit.Value]} {itemSkillLevelLimit.Value}");
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

    /*private static void ValidateAuctionBid(this Player player, WorldObject listing, uint bidAmount)
    {
        if (listing.GetListingStatus() != "active")
            throw new AuctionFailure($"Failed to place auction bid, the listing for this bid is not currently active", FailureCode.Auction.Unknown);

        var sellerId = listing.GetSellerId();

        if (sellerId > 0 && sellerId == player.Account.AccountId)
            throw new AuctionFailure($"Failed to place auction bid, you cannot bid on items you are selling", FailureCode.Auction.Unknown);

        if (listing.GetHighestBidder() == player.Account.AccountId)
            throw new AuctionFailure($"Failed to place auction bid, you are already the highest bidder", FailureCode.Auction.Unknown);

        var listingHighBid = listing.GetHighestBid();

        if (listingHighBid > 0 && listingHighBid > bidAmount)
            throw new AuctionFailure($"Failed to place auction bid, your bid isn't high enough", FailureCode.Auction.Unknown);

        var currencyType = listing.GetCurrencyType();

        if (currencyType == 0)
            throw new AuctionFailure($"Failed to place auction bid, this listing does not have a currency type", FailureCode.Auction.Unknown);

        var listingStartPrice = listing.GetListingStartPrice();

        var endTime = listing.GetListingEndTimestamp();

        if (endTime > 0 && Time.GetDateTimeFromTimestamp(endTime) < DateTime.UtcNow)
            throw new AuctionFailure($"Failed to place auction bid, this listing has already expired", FailureCode.Auction.Unknown);

        var numOfItems = player.GetNumInventoryItemsOfWCID((uint)currencyType);

        if (bidAmount < listingStartPrice)
            throw new AuctionFailure($"Failed to place auction bid, your bid amount is less than the starting price", FailureCode.Auction.Unknown);

        if (numOfItems < listingHighBid || numOfItems < listingStartPrice)
            throw new AuctionFailure($"Failed to place auction bid, you do not have enough currency items to bid on this listing", FailureCode.Auction.Unknown);
    }

    public static void PlaceAuctionBid(this Player player, uint listingId, uint bidAmount)
    {
        var listing = AuctionManager.GetListingById(listingId) ?? throw new AuctionFailure($"Listing with Id = {listingId} does not exist", FailureCode.Auction.Unknown);

        List<WorldObject> newBidItems = new List<WorldObject>();

        List<WorldObject> oldBidItems = AuctionManager.ItemsContainer.Inventory.Values
                     .Where(item => item.GetBidOwnerId() > 0 && item.GetBidOwnerId() == listing.GetHighestBidder()).ToList();

        var previousState = CapturePreviousBidState(listing);

        try
        {
            ValidateAuctionBid(player, listing, bidAmount);
            RemovePreviousBidItems(listing, previousState.PreviousBidderId, oldBidItems);
            PlaceNewBid(player, listing, bidAmount, newBidItems);
            UpdateListingWithNewBid(listing, player, bidAmount);
            NotifyPlayerOfSuccess(player, listing, bidAmount);
        }
        catch (AuctionFailure ex)
        {
            HandleBidFailure(ex, player, listing, previousState, oldBidItems, newBidItems);
        }
    }

    private static (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) CapturePreviousBidState(WorldObject listing)
    {
        return (
            PreviousBidderId: listing.GetHighestBidder(),
            PreviousBidderName: listing.GetHighestBidderName(),
            PreviousHighBid: listing.GetHighestBid()
        );
    }

    private static void RemovePreviousBidItems(WorldObject listing, uint previousBidderId, List<WorldObject> oldBidItems)
    {
        foreach (var item in oldBidItems)
        {
            if (!AuctionManager.TryRemoveFromInventory(item) ||
                !BankManager.TryAddToInventory(item, previousBidderId))
                throw new AuctionFailure($"Failed to process previous bid items for listing {listing.Guid.Full}", FailureCode.Auction.Unknown);

            ResetBidItemProperties(item, previousBidderId);
        }
    }

    private static void ResetBidItemProperties(WorldObject item, uint previousBidderId)
    {
        item.SetProperty(FakeIID.BidOwnerId, 0);
        item.SetProperty(FakeIID.ListingId, 0);
        item.SetProperty(FakeIID.BankId, previousBidderId);
    }

    private static void PlaceNewBid(Player player, WorldObject listing, uint bidAmount, List<WorldObject> newBidItems)
    {
        List<WorldObject> bidItems = player.GetInventoryItemsOfWCID((uint)listing.GetCurrencyType()).ToList();

        var remainingAmount = (int)bidAmount;
        var bidTime = DateTime.UtcNow;

        foreach (var item in bidItems)
        {
            if (remainingAmount <= 0) break;

            var amount = Math.Min(item.StackSize ?? 1, remainingAmount);
            player.RemoveItemForTransfer(item.Guid.Full, out var removedItem, amount);
            ConfigureBidItem(removedItem, player.Account.AccountId, listing.Guid.Full, bidTime);

            if (!AuctionManager.TryAddToInventory(removedItem))
                throw new AuctionFailure($"Failed to add bid item to Auction Items Chest", FailureCode.Auction.Unknown);

            remainingAmount -= amount;
            newBidItems.Add(removedItem);
        }

        if (remainingAmount > 0)
            throw new AuctionFailure($"Insufficient bid items to meet the bid amount for listing {listing.GetListingId()}", FailureCode.Auction.Unknown);
    }

    private static void ConfigureBidItem(WorldObject item, uint bidderId, uint listingId, DateTime bidTime)
    {
        item.SetProperty(FakeFloat.BidTimestamp, Time.GetUnixTime(bidTime));
        item.SetProperty(FakeIID.BidOwnerId, bidderId);
        item.SetProperty(FakeIID.ListingId, listingId);
    }

    private static void UpdateListingWithNewBid(WorldObject listing, Player player, uint bidAmount)
    {
        listing.SetProperty(FakeIID.HighestBidderId, player.Account.AccountId);
        listing.SetProperty(FakeString.HighestBidderName, player.Name);
        listing.SetProperty(FakeInt.ListingHighBid, (int)bidAmount);
    }

    private static void NotifyPlayerOfSuccess(Player player, WorldObject listing, uint bidAmount)
    {
        player.SendAuctionMessage(
            $"Successfully created an auction bid on listing with Id = {listing.Guid.Full}, Seller = {listing.GetSellerName()}, BidAmount = {bidAmount}",
            ChatMessageType.Broadcast
        );
    }

    private static void HandleBidFailure(AuctionFailure ex, Player player, WorldObject listing, (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) previousState, List<WorldObject> oldBidItems, List<WorldObject> newBidItems)
    {
        LogErrorAndNotifyPlayer(ex, player);
        RestorePreviousListingState(listing, previousState);
        RevertOldBidItems(oldBidItems, previousState.PreviousBidderId, listing.Guid.Full);
        RevertNewBidItems(player, newBidItems);
    }

    private static void LogErrorAndNotifyPlayer(AuctionFailure ex, Player player)
    {
        ModManager.Log(ex.Message, ModManager.LogLevel.Error);
        player.SendAuctionMessage(ex.Message);
    }

    private static void RestorePreviousListingState(WorldObject listing, (uint PreviousBidderId, string PreviousBidderName, int PreviousHighBid) previousState)
    {
        listing.SetProperty(FakeIID.HighestBidderId, previousState.PreviousBidderId);
        listing.SetProperty(FakeString.HighestBidderName, previousState.PreviousBidderName);
        listing.SetProperty(FakeInt.ListingHighBid, previousState.PreviousHighBid);
    }

    private static void RevertOldBidItems(List<WorldObject> oldBidItems, uint previousBidderId, uint listingId)
    {
        foreach (var item in oldBidItems)
        {
            item.SetProperty(FakeIID.BankId, 0);
            item.SetProperty(FakeIID.BidOwnerId, previousBidderId);
            item.SetProperty(FakeIID.ListingId, listingId);

            BankManager.TryRemoveFromInventory(item, previousBidderId);
            AuctionManager.TryAddToInventory(item);
        }
    }

    private static void RevertNewBidItems(Player player, List<WorldObject> removedNewBidItems)
    {
        foreach (var item in removedNewBidItems)
        {
            item.RemoveProperty(FakeIID.BidOwnerId);
            item.RemoveProperty(FakeIID.ListingId);

            AuctionManager.TryRemoveFromInventory(item);
            player.TryCreateInInventoryWithNetworking(item);
        }
    }*/
}
