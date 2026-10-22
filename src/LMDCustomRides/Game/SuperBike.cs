using Il2CppMegagon.Downhill.Config;
using Il2CppMegagon.Downhill.Vehicle.Configuration;

namespace LMDCustomRides.Game;

/// <summary>
/// The daily rides' Super Bike ("Bike_G", vehicle 7000) as a choice of the New Ride page's Bike row. It is never
/// written into the saved profile (CustomSession.ProfileSave): a profile saved with 7000 would make the plain game
/// ride the Super Bike on ranked rides. Leaving the Custom Rides tab puts the last normal bike back.
/// </summary>
internal static class SuperBike
{
    private static int _lastNormalBike;

    private static VehicleConfigurationDatabase Database
    {
        get
        {
            try
            {
                return GameConfig.ActiveConfig?.vehicleDB;
            }
            catch
            {
                return null; // the game config is not loaded yet
            }
        }
    }

    public static int NormalBike => _lastNormalBike != 0 ? _lastNormalBike : Database?.defaultConfiguration?.ID ?? 1000;

    /// <summary>Leaving the Custom Rides tab: the normal menus get the last normal bike back.</summary>
    public static void PutBackNormalBike()
    {
        var properties = GameState.Properties;
        if (properties != null && properties.vhl == GameState.SuperBikeVehicleId)
        {
            properties.ActiveVehicle = NormalBike;
        }
    }

    /// <summary>Per frame: remembers the last normal bike.</summary>
    public static void Update()
    {
        var vehicle = GameState.ActiveVehicle;
        if (vehicle != 0 && vehicle != GameState.SuperBikeVehicleId)
        {
            _lastNormalBike = vehicle;
        }
    }

    /// <summary>Unlocked, with its paint job owned, so the ride has one to show, as for any bike. In memory only.</summary>
    public static void Dress(VehicleConfiguration bike)
    {
        bike.m_unlocked = true;
        bike.m_owned = true;
        var variants = bike.m_vehicleCustomizationVariants;
        var owned = bike.m_vehicleCustomizationVariantsOwned;
        for (var i = 0; variants != null && i < variants.Count; i++)
        {
            var variant = variants[i];
            if (variant == null)
            {
                continue;
            }

            variant.m_unlocked = true;
            variant.m_owned = true;
            if (owned != null && !owned.Contains(variant))
            {
                owned.Add(variant);
            }
        }
    }
}
