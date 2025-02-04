using ACE.Entity.Models;

namespace ACE.Mods.Legend.Lib.Auction.Models;

/// <summary>
/// This context is used throughout the sell order transaction usecase
/// </summary>
public class CreateSellOrderContext
{
    public required List<WorldObject> RemovedItems { get; set; }
    public required WorldObject Item { get; set; }
    public required Player Seller { get; set; }
    public required Weenie Currency { get; set; }
    public required uint StartPrice { get; set; }
    public required uint NumberOfStacks { get; set; }
    public required uint StackSize { get;  set; }
    public required uint BuyoutPrice { get; set; }
    public required uint HoursDuration { get; set; }
    public required DateTime StartTime { get; set; }
    public required DateTime EndTime { get; set; }
    public required TimeSpan RemainingTime { get; set; }
}
