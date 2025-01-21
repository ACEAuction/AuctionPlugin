using ACE.Entity.Models;

namespace ACE.Mods.Legend.Lib.Auction.Models;

public class CreateSellOrder
{
    public WorldObject Item { get; set; }
    public Player Seller { get; set; }
    public Weenie Currency { get; set; }
    public uint StartPrice { get; set; }
    public uint NumberOfStacks { get; internal set; }
    public uint StackSize { get; internal set; }
    public uint BuyoutPrice { get; set; }
    public uint HoursDuration { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

