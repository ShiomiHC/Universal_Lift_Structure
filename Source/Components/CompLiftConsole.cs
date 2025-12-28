using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Universal_Lift_Structure;

public class CompProperties_LiftConsole : CompProperties
{
    public CompProperties_LiftConsole()
    {
        compClass = typeof(CompLiftConsole);
    }
}

public class CompLiftConsole : ThingComp
{
    private List<ULS_LiftRequest> pendingRequests = new List<ULS_LiftRequest>();

    public CompProperties_LiftConsole Props => (CompProperties_LiftConsole)props;

    public bool HasPendingRequests => pendingRequests.Count > 0;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref pendingRequests, "pendingRequests", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingRequests == null)
        {
            pendingRequests = new List<ULS_LiftRequest>();
        }
    }

    public void EnqueueRequest(ULS_LiftRequest request)
    {
        if (request == null)
        {
            return;
        }

        if (pendingRequests == null)
        {
            pendingRequests = new List<ULS_LiftRequest>();
        }

        // 移除针对同一控制器的旧请求
        for (int i = pendingRequests.Count - 1; i >= 0; i--)
        {
            if (pendingRequests[i].controller == request.controller)
            {
                pendingRequests.RemoveAt(i);
            }
        }

        pendingRequests.Add(request);
        UpdateLiftDesignation();
    }

    public void NotifyFlicked()
    {
        if (pendingRequests == null || pendingRequests.Count == 0)
        {
            return;
        }

        // 复制列表以避免在迭代中修改（尽管我们是清空）
        List<ULS_LiftRequest> requestsToExecute = new List<ULS_LiftRequest>(pendingRequests);
        pendingRequests.Clear();

        foreach (var request in requestsToExecute)
        {
            if (request.controller == null || request.controller.Destroyed || !request.controller.Spawned)
            {
                continue;
            }

            if (request.type == ULS_LiftRequestType.RaiseGroup)
            {
                request.controller.GizmoRaiseGroup();
            }
            else
            {
                request.controller.GizmoLowerGroup(request.startCell);
            }
        }

        UpdateLiftDesignation();
    }

    private void UpdateLiftDesignation()
    {
        if (parent.Map == null) return;

        Designation designation =
            parent.Map.designationManager.DesignationOn(parent, ULS_DesignationDefOf.ULS_FlickLiftStructure);
        bool hasRequests = pendingRequests.Count > 0;

        if (hasRequests && designation == null)
        {
            parent.Map.designationManager.AddDesignation(new Designation(parent,
                ULS_DesignationDefOf.ULS_FlickLiftStructure));
        }
        else if (!hasRequests && designation != null)
        {
            designation.Delete();
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (respawningAfterLoad)
        {
            // 确保 Designation 状态与 pendingRequests 同步
            UpdateLiftDesignation();
        }
    }
}
