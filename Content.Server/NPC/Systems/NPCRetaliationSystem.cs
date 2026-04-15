using Content.Server.NPC.Components;
using Content.Server.NPC.HTN; // #Misfits Add
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server.NPC.Systems;

/// <summary>
///     Handles NPC which become aggressive after being attacked.
/// </summary>
public sealed class NPCRetaliationSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HTNSystem _htn = default!; // #Misfits Add — trigger immediate replan on aggro

    // #Misfits Change — nearby friendly NPCs within roughly their aggro band will assist when one of them is attacked.
    // This fixes the common case where only the directly-hit mob retaliates while its packmates stay idle outside passive scan range.
    private const float DefaultAssistRange = 14f;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<NPCRetaliationComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<NPCRetaliationComponent, DisarmedEvent>(OnDisarmed);
    }

    private void OnDamageChanged(Entity<NPCRetaliationComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (args.Origin is not {} origin)
            return;

        if (!TryRetaliate(ent, origin))
            return;

        TryProvokeNearbyFriendlies(ent.Owner, origin);
    }

    private void OnDisarmed(Entity<NPCRetaliationComponent> ent, ref DisarmedEvent args)
    {
        if (!TryRetaliate(ent, args.Source))
            return;

        TryProvokeNearbyFriendlies(ent.Owner, args.Source);
    }

    private void TryProvokeNearbyFriendlies(EntityUid victim, EntityUid attacker)
    {
        if (!TryComp<NpcFactionMemberComponent>(victim, out var victimFaction))
            return;

        var assistRange = GetAssistRange(victim);
        foreach (var friendly in _npcFaction.GetNearbyFriendlies((victim, victimFaction), assistRange))
        {
            if (!TryComp<NPCRetaliationComponent>(friendly, out var retaliation))
                continue;

            TryRetaliate((friendly, retaliation), attacker);
        }
    }

    private float GetAssistRange(EntityUid victim)
    {
        if (!TryComp<HTNComponent>(victim, out var htn))
            return DefaultAssistRange;

        // #Misfits Change — reuse the victim's configured aggro vision radius so assist behavior tracks per-mob tuning.
        var assistRange = htn.Blackboard.GetValueOrDefault<float>("AggroVisionRadius", EntityManager);
        return assistRange > 0f ? assistRange : DefaultAssistRange;
    }

    public bool TryRetaliate(Entity<NPCRetaliationComponent> ent, EntityUid target)
    {
        // don't retaliate against inanimate objects.
        if (!HasComp<MobStateComponent>(target))
            return false;

        // don't retaliate against the same faction
        if (_npcFaction.IsEntityFriendly(ent.Owner, target))
            return false;

        _npcFaction.AggroEntity(ent.Owner, target);
        if (ent.Comp.AttackMemoryLength is {} memoryLength)
            ent.Comp.AttackMemories[target] = _timing.CurTime + memoryLength;

        // #Misfits Add — Force immediate HTN replan so the NPC responds to aggro without waiting for the next replan window.
        // This cuts perceived combat response delay from 250ms → ~1-2ms (next frame).
        if (TryComp<HTNComponent>(ent.Owner, out var htn))
        {
            _htn.Replan(htn);
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NPCRetaliationComponent, FactionExceptionComponent>();
        while (query.MoveNext(out var uid, out var retaliationComponent, out var factionException))
        {
            // TODO: can probably reuse this allocation and clear it
            foreach (var entity in new ValueList<EntityUid>(retaliationComponent.AttackMemories.Keys))
            {
                if (!TerminatingOrDeleted(entity) && _timing.CurTime < retaliationComponent.AttackMemories[entity])
                    continue;

                _npcFaction.DeAggroEntity((uid, factionException), entity);
                // TODO: should probably remove the AttackMemory, thats the whole point of the ValueList right??
            }
        }
    }
}
