using Content.Shared.Bed.Sleep;

namespace Content.Shared._WF.IPC;

public abstract class SharedRobotBodySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RobotBodyComponent, TryingToSleepEvent>(OnTryingToSleep);
    }

    private void OnTryingToSleep(Entity<RobotBodyComponent> ent, ref TryingToSleepEvent args)
    {
        if (!ent.Comp.CanSleep)
            args.Cancelled = true;
    }
}
