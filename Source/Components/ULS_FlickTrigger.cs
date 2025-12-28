namespace Universal_Lift_Structure;

public class CompProperties_ULS_FlickTrigger : CompProperties
{
    public CompProperties_ULS_FlickTrigger()
    {
        compClass = typeof(ULS_FlickTrigger);
    }
}

public class ULS_FlickTrigger : ThingComp
{
    private List<ULS_LiftRequest> pendingRequests = new();


    private bool flickRequested;


    private void RequestPulseFlickIfNeeded()
    {
        if (flickRequested)
        {
            return;
        }

        flickRequested = true;
        ULS_FlickUtility.RequestPulseFlick(parent);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();

        Scribe_Collections.Look(ref pendingRequests, "pendingRequests", LookMode.Deep);
        if (Scribe.mode is LoadSaveMode.PostLoadInit && pendingRequests is null)
        {
            pendingRequests = new();
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);


        if (respawningAfterLoad && pendingRequests is { Count: > 0 })
        {
            RequestPulseFlickIfNeeded();
        }
    }


    public void EnqueueRequest(ULS_LiftRequest request)
    {
        if (request is null)
        {
            return;
        }

        pendingRequests ??= new();


        Building_WallController controller = request.controller;
        for (int i = pendingRequests.Count - 1; i >= 0; i--)
        {
            if (pendingRequests[i]?.controller == controller)
            {
                pendingRequests.RemoveAt(i);
            }
        }

        pendingRequests.Add(request);
        RequestPulseFlickIfNeeded();
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);

        if (signal is not ("FlickedOn" or "FlickedOff"))
        {
            return;
        }

        TryExecuteAllRequests();
    }


    private void TryExecuteAllRequests()
    {
        if (pendingRequests is not { Count: > 0 })
        {
            return;
        }


        List<ULS_LiftRequest> requests = new(pendingRequests);
        pendingRequests.Clear();


        flickRequested = false;

        foreach (var request in requests)
        {
            Building_WallController controller = request?.controller;
            if (controller is null || controller.Destroyed || !controller.Spawned || controller.Map is null)
            {
                Messages.Message("ULS_LiftRequest_ControllerInvalid".Translate(), MessageTypeDefOf.RejectInput,
                    false);
                continue;
            }

            if (request.type == ULS_LiftRequestType.RaiseGroup)
            {
                controller.GizmoRaiseGroup();
            }
            else
            {
                controller.GizmoLowerGroup(request.startCell);
            }
        }


        if (pendingRequests.Count > 0)
        {
            RequestPulseFlickIfNeeded();
        }
    }
}