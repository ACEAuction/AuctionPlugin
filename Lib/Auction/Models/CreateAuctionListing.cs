using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Mods.Legend.Lib.Auction.Models;

public class CreateAuctionSell
{
    public uint ItemId { get; set; }
    public uint SellerId { get; set; }
    public string SellerName { get; set; } 
    public uint StartPrice { get; set; }
    public uint NumberOfStacks { get; internal set; }
    public uint StackSize { get; internal set; }
    public uint BuyoutPrice { get; set; }
    public uint CurrencyType { get; set; }
    public uint HoursDuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

