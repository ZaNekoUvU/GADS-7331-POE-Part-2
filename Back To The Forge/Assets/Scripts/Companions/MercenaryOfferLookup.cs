using System.Collections.Generic;
using UnityEngine;

/// <summary>Runtime lookup of <see cref="HireableCompanionOffer"/> by combat unit id.</summary>
public static class MercenaryOfferLookup
{
    private static readonly Dictionary<int, HireableCompanionOffer> ByUnitId = new();

    public static void RegisterCatalog(MercenaryRosterCatalog catalog)
    {
        if (catalog?.Recruits == null)
            return;

        foreach (var entry in catalog.Recruits)
        {
            if (entry.offer != null && entry.offer.UnitId > 0)
                ByUnitId[entry.offer.UnitId] = entry.offer;
        }
    }

    public static void RegisterOffer(HireableCompanionOffer offer)
    {
        if (offer != null && offer.UnitId > 0)
            ByUnitId[offer.UnitId] = offer;
    }

    public static bool TryGet(int unitId, out HireableCompanionOffer offer) =>
        ByUnitId.TryGetValue(unitId, out offer);

    public static void Clear() => ByUnitId.Clear();
}
