namespace Universal_Lift_Structure;

public partial class Building_WallController
{
    private static void MessageReject(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        if (MessagesRepeatAvoider.MessageShowAllowed(key, 1f))
        {
            Messages.Message(
                key.Translate(args),
                lookTargets,
                MessageTypeDefOf.RejectInput,
                historical: false);
        }
    }


    private static void MessageNeutral(string key, LookTargets lookTargets, params NamedArgument[] args)
    {
        if (MessagesRepeatAvoider.MessageShowAllowed(key, 1f))
        {
            Messages.Message(
                key.Translate(args),
                lookTargets,
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }
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


    // 升起检测时忽略控制器本身及其框架/蓝图
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

        if (defInstance == ULS_ThingDefOf.ULS_WallController)
        {
            return true;
        }

        return defInstance.entityDefToBuild == ULS_ThingDefOf.ULS_WallController;
    }


    // 控制器被销毁时清算 stored 建筑：等价于其被"击杀"。
    // 走 KillFinalize 死亡掉落（def.killedLeavings：石块 / 金属残骸 ≈ 25%）而非全额退款，
    // 与"桥控制器损毁 → 同格 edifice 也跟随损毁"的一体化语义保持一致。
    // 注：storedThing 处于 unspawned 状态，Thing.Destroy 不会触发 DeSpawn → 不会自动产生 leavings；
    //     必须先手动调用 GenLeaving.DoLeavingsFor 落地资源，再 Destroy 弃置实体。
    internal void DestroyStored(Map map)
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
            GenLeaving.DoLeavingsFor(storedThing, map, DestroyMode.KillFinalize);
        }

        storedThing.Destroy();

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