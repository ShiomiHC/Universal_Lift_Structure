namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    private static void MessageReject(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        Messages.Message(
            key.Translate(args),
            lookTargets,
            MessageTypeDefOf.RejectInput,
            historical: false);
    }


    private static void MessageNeutral(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        Messages.Message(
            key.Translate(args),
            lookTargets,
            MessageTypeDefOf.NeutralEvent,
            historical: false);
    }


    private bool IsBlockedForRaise(Map map, IntVec3 spawnCell, Thing storedThing)
    {
        foreach (IntVec3 cell in GenAdj.OccupiedRect(spawnCell, storedRotation, storedThing.def.size))
        {
            if (!cell.InBounds(map))
            {
                return true;
            }


            Building edifice = map.edificeGrid[cell];
            if (edifice != null && edifice.def != ULS_ThingDefOf.ULS_LiftBlocker)
            {
                return true;
            }


            List<Thing> things = map.thingGrid.ThingsListAtFast(cell);
            foreach (var thing in things)
            {
                if (thing == this ||
                    thing == storedThing ||
                    thing.def == ULS_ThingDefOf.ULS_LiftBlocker ||
                    IsWallControllerThing(thing))
                {
                    continue;
                }


                if (thing is Pawn ||
                    thing is Frame ||
                    thing is Blueprint ||
                    (thing.def.category == ThingCategory.Building &&
                     (thing.def.building == null || thing.def.building.isEdifice)))
                {
                    return true;
                }
            }
        }

        return false;
    }


    private bool IsWallControllerThing(Thing thing)
    {
        if (thing == null)
        {
            return false;
        }

        if (thing is Building_WallController)
        {
            return true;
        }

        ThingDef defInstance = thing.def;
        if (defInstance == null)
        {
            return false;
        }

        if (defInstance.defName == "ULS_WallController")
        {
            return true;
        }

        if (defInstance.entityDefToBuild is { defName: "ULS_WallController" })
        {
            return true;
        }

        return false;
    }


    internal void RefundStored(Map map)
    {
        if (!HasStored)
        {
            return;
        }

        Thing storedThing = StoredThing;
        if (storedThing == null)
        {
            storedCell = IntVec3.Invalid;
            storedThingMarketValueIgnoreHp = 0f;
            return;
        }


        innerContainer.Remove(storedThing);
        storedThingMarketValueIgnoreHp = 0f;

        if (storedThing.Destroyed)
        {
            storedCell = IntVec3.Invalid;
            return;
        }


        IntVec3 position = storedCell.IsValid ? storedCell : Position;
        storedThing.Position = position;
        storedThing.Rotation = storedRotation;


        if (map != null)
        {
            GenSpawn.Refund(storedThing, map, CellRect.Empty);
        }
        else
        {
            storedThing.Destroy();
        }

        storedCell = IntVec3.Invalid;
    }


    private static int GetGroupMaxSize()
    {
        int maxSize = UniversalLiftStructureMod.Settings?.groupMaxSize ?? 20;
        if (maxSize < 1)
        {
            return 20;
        }

        return maxSize;
    }
}