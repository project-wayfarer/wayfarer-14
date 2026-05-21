using System.Linq;
using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Events;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._WF.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class MeteorSwarmCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MeteorSwarmSystem _meteorSwarm = default!;

    public override string Command => "wfmeteorswarm";
    public override string Description => "Spawns a meteor swarm of the given severity. Optionally targets a specific grid by name.";
    public override string Help => $"Usage: {Command} <severity> [target name or 'random']";

    private static readonly Dictionary<string, string> SeverityToPrototype = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SpaceDustMinor"] = "GameRuleSpaceDustMinor",
        ["SpaceDustMajor"] = "GameRuleSpaceDustMajor",
        ["MeteorSwarmSmall"] = "GameRuleMeteorSwarmSmall",
        ["MeteorSwarmMedium"] = "GameRuleMeteorSwarmMedium",
        ["MeteorSwarmLarge"] = "GameRuleMeteorSwarmLarge",
        ["UristSwarm"] = "GameRuleUristSwarm",
        ["ClownSwarm"] = "GameRuleClownSwarm",
        ["CowSwarm"] = "GameRuleCowSwarm",
        ["PotatoSwarm"] = "GameRulePotatoSwarm",
        ["FunSwarm"] = "GameRuleFunSwarm",
    };

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!SeverityToPrototype.TryGetValue(args[0], out var prototypeId))
        {
            shell.WriteError($"Unknown severity '{args[0]}'. Valid: {string.Join(", ", SeverityToPrototype.Keys)}.");
            return;
        }

        EntityUid? targetUid = null;
        if (args.Length > 1 && !string.Equals(args[1], "random", StringComparison.OrdinalIgnoreCase))
        {
            var targetName = string.Join(' ', args.Skip(1)).Trim();
            var candidates = FindGridsByName(targetName);

            switch (candidates.Count)
            {
                case 0:
                    shell.WriteError($"No POI or active player ship matches '{targetName}'.");
                    return;
                case > 1:
                    var names = string.Join(", ", candidates.Select(c => $"\"{EntityManager.GetComponent<MetaDataComponent>(c).EntityName}\""));
                    shell.WriteError($"Multiple matches for '{targetName}': {names}. Be more specific.");
                    return;
                default:
                    targetUid = candidates[0];
                    break;
            }
        }

        var ruleEntity = _gameTicker.AddGameRule(prototypeId);
        _meteorSwarm.SetSilent(ruleEntity);

        if (targetUid is { } target)
        {
            _meteorSwarm.SetTargetGrid(ruleEntity, target);
            var targetName = EntityManager.GetComponent<MetaDataComponent>(target).EntityName;
            shell.WriteLine($"Meteor swarm '{args[0]}' targeting \"{targetName}\".");
        }
        else
            shell.WriteLine($"Meteor swarm '{args[0]}' targeting random POI or active player ship.");

        if (!_gameTicker.StartGameRule(ruleEntity))
            shell.WriteError($"Failed to start game rule '{prototypeId}'.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(SeverityToPrototype.Keys, "<severity>");

        if (args.Length == 2)
        {
            var names = CollectTargetNames();
            return CompletionResult.FromHintOptions(names.Prepend("random"), "<target name>");
        }

        return CompletionResult.Empty;
    }

    private List<EntityUid> FindGridsByName(string query)
    {
        var matches = new List<EntityUid>();
        foreach (var grid in _meteorSwarm.GetTargetableGrids())
        {
            var name = EntityManager.GetComponent<MetaDataComponent>(grid).EntityName;
            if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
                return [grid];

            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                matches.Add(grid);
        }
        return matches;
    }

    private IEnumerable<string> CollectTargetNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var grid in _meteorSwarm.GetTargetableGrids())
        {
            names.Add(EntityManager.GetComponent<MetaDataComponent>(grid).EntityName);
        }
        return names;
    }
}
