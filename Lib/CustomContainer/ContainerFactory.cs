using ACE.Database;
using ACE.Database.Models.Shard;
using ACE.Entity;
using ACE.Entity.Enum.Properties;
using ACE.Server.Factories;
using ACE.Server.Managers;

namespace ACE.Mods.Legend.Lib.CustomContainer;

public static class ContainerFactory
{
    private static uint GetContainerId(string keycode)
    {
        using (var ctx = new ShardDbContext())
        {
            var query = from container in ctx.Biota
                        join cType in ctx.BiotaPropertiesString on container.Id equals cType.ObjectId
                        where cType.Type == (ushort)PropertyString.Name && cType.Value == keycode
                        select container.Id;

            var containerId = query.FirstOrDefault();

            return containerId;
        }
    }

    public static Chest CreateContainer(string keycode, Position containerPosition)
    {
        var containerId = GetContainerId(keycode);

        Chest chest;
        var weenie = DatabaseManager.World.GetCachedWeenie((uint)WeenieClassName.W_CHEST_CLASS);

        var lb = LandblockManager.GetLandblock(containerPosition.LandblockId, false, true);

        if (lb == null)
            throw new Exception($"The landblock for the auction container with id: {containerId} does not exist");

        chest = (Chest)lb.GetObject(new ObjectGuid(containerId), false);

        if (chest != null)
            return chest;

        if (containerId == 0)
        {
            var guid = GuidManager.NewDynamicGuid();
            chest = (Chest)WorldObjectFactory.CreateWorldObject(weenie, guid);
        }
        else
        {
            // use the biota if it exists, else abort
            var biota = DatabaseManager.Shard.BaseDatabase.GetBiota(containerId);

            // should never happen
            if (biota == null)
                throw new Exception($"Failed to retrieve container biota with id: {containerId}, contact an admin!");

            chest = (Chest)WorldObjectFactory.CreateWorldObject(biota);
        }

        chest.DisplayName = keycode;
        chest.Location = new Position(containerPosition);
        chest.TimeToRot = -1;
        chest.SetProperty(PropertyInt.ItemsCapacity, int.MaxValue);
        chest.SetProperty(PropertyInt.ContainersCapacity, int.MaxValue);
        chest.SetProperty(PropertyInt.EncumbranceCapacity, int.MaxValue);
        chest.SetProperty(PropertyString.Name, keycode);
        chest.SaveBiotaToDatabase();

        if (chest == null)
            throw new Exception($"Failed to create container with id: {containerId}");

        return chest;
    }
}

