using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Models;
using ACE.Mods.Legend.Lib.Auction.Models;
using ACE.Mods.Legend.Lib.Auction.Network;
using ACE.Mods.Legend.Lib.Common.Errors;
using ACE.Mods.Legend.Lib.Database;
using ACE.Mods.Legend.Lib.Database.Models;
using ACE.Server;
using ACE.Server.Managers;
using ACE.Server.Network.GameMessages;
using ACE.Server.Network.GameMessages.Messages;
using ACE.Server.Network.Managers;
using ACE.Shared;
using Microsoft.EntityFrameworkCore;
using static ACE.Server.Network.Managers.InboundMessageManager;

namespace ACE.Mods.Legend.Lib.Auction
{
    [HarmonyPatchCategory(nameof(AuctionPatches))]
    public static class AuctionPatches
    {
        static Settings Settings => PatchClass.Settings;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Program), "AutoApplyDatabaseUpdates")]
        public static void PostAutoApplyDatabaseUpdates(ref Program __instance)
        {
            DbPatcher.PatchDatabase(
                "Shard",
                ConfigManager.Config.MySql.Shard.Host,
                ConfigManager.Config.MySql.Shard.Port,
                ConfigManager.Config.MySql.Shard.Username,
                ConfigManager.Config.MySql.Shard.Password,
                ConfigManager.Config.MySql.Authentication.Database,
                ConfigManager.Config.MySql.Shard.Database,
                ConfigManager.Config.MySql.World.Database);

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShardDbContext), "OnModelCreating", new Type[] { typeof(ModelBuilder) })]
        public static void PostOnModelCreating(ModelBuilder modelBuilder, ref ShardDbContext __instance)
        {
            modelBuilder.Entity<AuctionSellOrder>(entity =>
            {
                entity.ToTable("auction_sell_order");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .IsRequired()
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.SellerId)
                   .HasColumnName("seller_id")
                   .IsRequired();

                entity.HasMany(e => e.Listings)
                      .WithOne(e => e.SellOrder)
                      .HasForeignKey(e => e.SellOrderId);
            });

            modelBuilder.Entity<AuctionListing>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");

                entity.ToTable("auction_listing");

                entity.Property(e => e.Id)
                      .HasColumnName("id")
                      .IsRequired()
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemId)
                      .HasColumnName("item_id")
                      .IsRequired();

                entity.Property(e => e.SellOrderId)
                      .HasColumnName("sell_order_id")
                      .IsRequired();

                entity.Property(e => e.SellerId)
                      .HasColumnName("seller_id")
                      .IsRequired();

                entity.Property(e => e.SellerName)
                  .HasColumnName("seller_name")
                  .IsRequired();

                entity.Property(e => e.StartPrice)
                      .HasColumnName("start_price")
                      .IsRequired();

                entity.Property(e => e.BuyoutPrice)
                      .HasColumnName("buyout_price")
                      .IsRequired();

                entity.Property(e => e.StackSize)
                      .HasColumnName("stack_size")
                      .IsRequired();

                entity.Property(e => e.NumberOfStacks)
                      .HasColumnName("number_of_stacks")
                      .IsRequired();

                entity.Property(e => e.CurrencyType)
                      .HasColumnName("currency_type")
                      .IsRequired();

                entity.Property(e => e.HighestBidAmount)
                      .HasColumnName("highest_bid_amount")
                      .IsRequired();

                entity.Property(e => e.HighestBidId)
                      .HasColumnName("highest_bid_id");

                entity.Property(e => e.HighestBidderId)
                      .HasColumnName("highest_bidder_id");

                entity.Property(e => e.Status)
                      .HasColumnName("status")
                      .IsRequired()
                      .HasDefaultValue(AuctionListingStatus.active)
                      .HasConversion(
                          v => v.ToString(),
                          v => (AuctionListingStatus)Enum.Parse(typeof(AuctionListingStatus), v)
                      );

                entity.Property(e => e.StartTime)
                      .HasColumnName("start_time")
                      .IsRequired();

                entity.Property(e => e.EndTime)
                      .HasColumnName("end_time")
                      .IsRequired();

                entity.HasMany(a => a.Bids)
                      .WithOne(b => b.AuctionListing)
                      .HasForeignKey(b => b.AuctionListingId);

                entity.HasOne(b => b.SellOrder)
                     .WithMany(a => a.Listings)
                     .HasForeignKey(b => b.SellOrderId);

                entity.HasIndex(a => a.Status)
                      .HasDatabaseName("idx_auction_listing_status");

                entity.HasIndex(a => new { a.Status, a.EndTime })
                      .HasDatabaseName("idx_auction_listing_status_endtime");
            });

            modelBuilder.Entity<AuctionBid>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");

                entity.ToTable("auction_bid");

                entity.Property(e => e.Id)
                      .HasColumnName("id")
                      .IsRequired()
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.BidderId)
                      .HasColumnName("bidder_id")
                      .IsRequired();

                entity.Property(e => e.AuctionListingId)
                      .HasColumnName("auction_listing_id")
                      .IsRequired();

                entity.Property(e => e.BidderName)
                  .HasColumnName("bidder_name")
                  .IsRequired();

                entity.Property(e => e.BidAmount)
                      .HasColumnName("bid_amount")
                      .IsRequired();

                entity.Property(e => e.Resolved)
                      .HasColumnName("resolved")
                      .IsRequired();

                entity.Property(e => e.BidTime)
                      .HasColumnName("bid_time")
                      .IsRequired();

                entity.HasOne(b => b.AuctionListing)
                      .WithMany(a => a.Bids)
                      .HasForeignKey(b => b.AuctionListingId);

                entity.HasMany(a => a.AuctionBidItems)
                      .WithOne(b => b.AuctionBid)
                      .HasForeignKey(b => b.BidId);

            });

            modelBuilder.Entity<AuctionBidItem>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("PRIMARY");

                entity.ToTable("auction_bid_item");

                entity.Property(e => e.Id)
                      .HasColumnName("id")
                      .IsRequired()
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.BidId)
                      .HasColumnName("bid_id")
                      .IsRequired();

                entity.Property(e => e.ItemId)
                      .HasColumnName("item_id")
                      .IsRequired();
            });

            modelBuilder.Entity<MailItem>(entity =>
            {
                entity.ToTable("mail_item");

                entity.HasKey(e => e.Id).HasName("PRIMARY");

                entity.Property(e => e.Id)
                      .HasColumnName("id")
                      .IsRequired()
                      .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemId)
                      .HasColumnName("item_id")
                      .IsRequired();

                entity.Property(e => e.From)
                      .HasColumnName("from");

                entity.Property(e => e.ReceiverId)
                      .HasColumnName("receiver_id")
                      .IsRequired();

                entity.Property(e => e.Status)
                      .HasColumnName("status")
                      .IsRequired()
                      .HasDefaultValue(MailStatus.pending)
                      .HasConversion(
                          v => v.ToString(),
                          v => (MailStatus)Enum.Parse(typeof(MailStatus), v)
                      );

                entity.HasIndex(e => new { e.ReceiverId, e.Status })
                      .HasDatabaseName("idx_mail_item_receiver_status");
            });
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldManager), nameof(WorldManager.UpdateGameWorld))]
        public static void PostUpdateGameWorld(ref bool __result)
        {
            AuctionManager.Tick(Time.GetUnixTime());
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InboundMessageManager), "DefineMessageHandlers")]
        public static void PostDefineMessageHandlers()
        {
            messageHandlers = messageHandlers ?? new Dictionary<GameMessageOpcode, MessageHandlerInfo>();

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                foreach (var methodInfo in type.GetMethods())
                {
                    foreach (var messageHandlerAttribute in methodInfo.GetCustomAttributes<GameMessageAttribute>())
                    {
                        var messageHandler = new MessageHandlerInfo()
                        {
                            Handler = (MessageHandler)Delegate.CreateDelegate(typeof(MessageHandler), methodInfo),
                            Attribute = messageHandlerAttribute
                        };

                        messageHandlers[messageHandlerAttribute.Opcode] = messageHandler;
                    }
                }
            }
        }

        [CommandHandler("ah-list", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 0, "Show auction house listings.", "Usage /ah-list [optional LISTING_ID]")]
        public static void HandleAuctionList(Session session, params string[] parameters)
        {
            var response = new JsonResponse<List<AuctionItem>>(
               data: null,
               success: false,
               errorCode: (int)FailureCode.Auction.SellValidation,
               errorMessage: "No active auctions!");

            session.Network.EnqueueSend(new GameMessageAuctionGetAllListings(response));
            return;
        }

        /*[CommandHandler("ah-bid", AccessLevel.Player, CommandHandlerFlag.RequiresWorld, 2, "Bid on an auction listing.", "Usage /ah-bid <LISTING_ID> <BID_AMOUNT>")]
        public static void HandleAuctionBid(Session session, params string[] parameters)
        {
            if (parameters.Length == 2 &&
                uint.TryParse(parameters[0], out var listingId) &&
                uint.TryParse(parameters[1], out var bidAmount))
            {
                try
                {
                    session.Player.PlaceAuctionBid(listingId, bidAmount);
                }
                catch (AuctionFailure ex)
                {
                    session.Player.SendAuctionMessage(ex.Message);
                }
                catch (Exception ex)
                {
                    ModManager.Log(ex.Message, ModManager.LogLevel.Error);
                    session.Player.SendAuctionMessage($"An unexpected error occurred");
                }
            }
        }*/
    }
}
