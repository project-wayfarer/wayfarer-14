using System.Linq;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Server._NF.RoundNotifications.Events;
using Content.Shared._WF.RoleplayLeveling;
using Content.Shared._WF.RoleplayLeveling.Components;
using Content.Shared._WF.RoleplayLeveling.Events;
using Content.Shared.GameTicking;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._WF.RoleplayLeveling;

/// <summary>
/// Server-side system for managing roleplay levels and experience
/// </summary>
public sealed class RoleplayLevelingSystem : SharedRoleplayLevelingSystem
{
    [Dependency] private readonly IServerDbManager _dbManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private int _currentRoundId = 0;

    // Track when players joined this round for calculating commend availability
    private readonly Dictionary<NetUserId, TimeSpan> _playerJoinTimes = new();

    // Anti-spam: Track last message time per player
    private readonly Dictionary<EntityUid, TimeSpan> _lastMessageTime = new();
    private const float MessageCooldown = 2.0f; // 2 seconds between XP awards

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStarted);
        SubscribeNetworkEvent<GiveCommendMessage>(OnGiveCommendMessage);
        SubscribeNetworkEvent<RequestAvailableCommendsMessage>(OnRequestAvailableCommends);
        SubscribeNetworkEvent<RequestMyCommendsMessage>(OnRequestMyCommends);
        SubscribeLocalEvent<EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<RoleplayLevelComponent, EmoteEvent>(OnEmote);
    }

    private void OnEntitySpoke(EntitySpokeEvent args)
    {
        // Only award XP for in-character speech (not radio messages)
        if (args.Channel != null)
            return;

        var speaker = args.Source;

        // Check if player has roleplay level component
        if (!TryComp<RoleplayLevelComponent>(speaker, out _))
            return;

        // Anti-spam check
        var currentTime = _timing.CurTime;
        if (_lastMessageTime.TryGetValue(speaker, out var lastTime))
        {
            if ((currentTime - lastTime).TotalSeconds < MessageCooldown)
                return;
        }

        _lastMessageTime[speaker] = currentTime;

        // Award XP for speaking (configurable via CVar)
        var chatXp = _cfg.GetCVar(CCVars.RoleplayXpChat);
        AwardExperience(speaker, chatXp, "Chat message");
    }

    private void OnEmote(EntityUid uid, RoleplayLevelComponent component, ref EmoteEvent args)
    {
        // Anti-spam check (same cooldown as chat)
        var currentTime = _timing.CurTime;
        if (_lastMessageTime.TryGetValue(uid, out var lastTime))
        {
            if ((currentTime - lastTime).TotalSeconds < MessageCooldown)
                return;
        }

        _lastMessageTime[uid] = currentTime;

        // Award XP for emoting (configurable via CVar)
        var emoteXp = _cfg.GetCVar(CCVars.RoleplayXpEmote);
        AwardExperience(uid, emoteXp, "Emote");
    }

    private void OnRoundStarted(RoundStartedEvent ev)
    {
        _currentRoundId = _gameTicker.RoundId;
        _playerJoinTimes.Clear();
    }

    private async void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!TryComp<ActorComponent>(args.Entity, out var actor))
            return;

        var userId = actor.PlayerSession.UserId;

        // Track when this player joined the round for commend calculations
        if (!_playerJoinTimes.ContainsKey(userId))
        {
            _playerJoinTimes[userId] = _timing.CurTime;
        }

        // Load or create roleplay level data from database
        var levelData = await _dbManager.GetOrCreateRoleplayLevel(userId.UserId);

        // Add component to player
        var comp = EnsureComp<RoleplayLevelComponent>(args.Entity);
        comp.UserId = userId.UserId;
        comp.Level = levelData.Level;
        comp.Experience = levelData.Experience;
        comp.ExperienceToNextLevel = levelData.ExperienceToNextLevel;
        comp.TotalCommends = levelData.TotalCommends;

        Dirty(args.Entity, comp);
    }

    private async void OnPlayerDetached(PlayerDetachedEvent args)
    {
        if (!TryComp<RoleplayLevelComponent>(args.Entity, out var comp))
            return;

        // Save to database
        await _dbManager.UpdateRoleplayLevel(
            comp.UserId,
            comp.Level,
            comp.Experience,
            comp.ExperienceToNextLevel,
            comp.TotalCommends);

        RemComp<RoleplayLevelComponent>(args.Entity);
    }

    /// <summary>
    /// Award experience to a player
    /// </summary>
    public void AwardExperience(EntityUid player, long amount, string reason)
    {
        if (!TryComp<RoleplayLevelComponent>(player, out var comp))
            return;

        comp.Experience += amount;

        // Check for level up
        while (comp.Experience >= comp.ExperienceToNextLevel)
        {
            comp.Experience -= comp.ExperienceToNextLevel;
            comp.Level++;
            comp.ExperienceToNextLevel = CalculateExperienceForLevel(comp.Level + 1);

            // Raise level up event
            var levelUpEvent = new RoleplayLevelUpEvent(player, comp.Level);
            RaiseLocalEvent(levelUpEvent);
        }

        Dirty(player, comp);

        // Raise experience gained event
        var expEvent = new RoleplayExperienceGainedEvent(player, amount, reason);
        RaiseLocalEvent(expEvent);

        // Async save to database
        SaveToDatabase(player, comp);
    }

    private async void OnGiveCommendMessage(GiveCommendMessage msg, EntitySessionEventArgs args)
    {
        if (!TryComp<ActorComponent>(args.SenderSession.AttachedEntity, out var actorComp))
            return;

        var giver = args.SenderSession.AttachedEntity.Value;

        // Convert NetEntity to EntityUid
        var recipientEntity = GetEntity(msg.Target);
        if (!recipientEntity.IsValid())
            return;

        // Validation checks
        if (giver == recipientEntity)
            return; // Can't commend yourself

        if (!TryComp<ActorComponent>(giver, out var giverActor))
            return;

        if (!TryComp<ActorComponent>(recipientEntity, out var recipientActor))
            return;

        var giverUserId = giverActor.PlayerSession.UserId;
        var recipientUserId = recipientActor.PlayerSession.UserId;

        // Calculate how many commends the giver has available based on playtime
        var availableCommends = CalculateAvailableCommends(giverUserId);

        // Check how many they've already given this round
        var commendsGiven = await _dbManager.GetRoundCommendsGivenByPlayer(giverUserId.UserId, _currentRoundId);

        if (commendsGiven >= availableCommends)
            return; // No more commends available

        // Get actual profile IDs from database
        var giverPrefs = _prefsManager.GetPreferences(giverUserId);
        var recipientPrefs = _prefsManager.GetPreferences(recipientUserId);

        var giverSlot = giverPrefs.SelectedCharacterIndex;
        var recipientSlot = recipientPrefs.SelectedCharacterIndex;

        var giverProfileId = await _dbManager.GetProfileIdAsync(giverUserId, giverSlot);
        var recipientProfileId = await _dbManager.GetProfileIdAsync(recipientUserId, recipientSlot);

        if (giverProfileId == null || recipientProfileId == null)
            return; // Can't commend if profile doesn't exist in database

        // Save commend to database
        await _dbManager.AddRoleplayCommend(
            _currentRoundId,
            recipientProfileId.Value,
            recipientUserId.UserId,
            giverProfileId.Value,
            giverUserId.UserId,
            msg.Comment,
            msg.IsPrivate);

        // Update recipient's total commends
        if (TryComp<RoleplayLevelComponent>(recipientEntity, out var recipientComp))
        {
            recipientComp.TotalCommends++;
            Dirty(recipientEntity, recipientComp);
            SaveToDatabase(recipientEntity, recipientComp);
        }

        // Award experience for receiving a commend (configurable via CVar)
        var commendXp = _cfg.GetCVar(CCVars.RoleplayXpCommend);
        AwardExperience(recipientEntity, commendXp, "Received commend");

        // Notify recipient that they received a commend
        if (recipientActor?.PlayerSession != null)
        {
            var commendMessage = msg.IsPrivate
                ? Loc.GetString("roleplay-commend-received-private")
                : Loc.GetString("roleplay-commend-received-public", ("giver", Name(giver)));
            _chatManager.DispatchServerMessage(recipientActor.PlayerSession, commendMessage);
        }

        // Raise event
        var commendEvent = new RoleplayCommendReceivedEvent(recipientEntity, giver, msg.Comment, msg.IsPrivate);
        RaiseLocalEvent(commendEvent);

        // Send updated commend count back to the giver
        var remaining = Math.Max(0, availableCommends - (commendsGiven + 1));
        RaiseNetworkEvent(new AvailableCommendsMessage(remaining), args.SenderSession);
    }

    private async void SaveToDatabase(EntityUid player, RoleplayLevelComponent comp)
    {
        await _dbManager.UpdateRoleplayLevel(
            comp.UserId,
            comp.Level,
            comp.Experience,
            comp.ExperienceToNextLevel,
            comp.TotalCommends);
    }

    /// <summary>
    /// Calculate how many commends a player has available based on their playtime this round
    /// </summary>
    private int CalculateAvailableCommends(NetUserId userId)
    {
        var startingCommends = _cfg.GetCVar(CCVars.RoleplayCommendStart);
        var maxCommends = _cfg.GetCVar(CCVars.RoleplayCommendMax);

        if (!_playerJoinTimes.TryGetValue(userId, out var joinTime))
            return startingCommends; // Default to starting commends if join time not tracked

        var playtime = (_timing.CurTime - joinTime).TotalHours;
        var hoursPerCommend = _cfg.GetCVar(CCVars.RoleplayCommendHours);

        // Start with configured starting commends, earn 1 more every X hours, up to max
        var earnedCommends = startingCommends + (int)(playtime / hoursPerCommend);

        return Math.Min(earnedCommends, maxCommends);
    }

    private async void OnRequestAvailableCommends(RequestAvailableCommendsMessage msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        // Calculate available commends
        var availableCommends = CalculateAvailableCommends(userId);

        // Get how many they've already given
        var commendsGiven = await _dbManager.GetRoundCommendsGivenByPlayer(userId.UserId, _currentRoundId);

        // Send back remaining commends
        var remaining = Math.Max(0, availableCommends - commendsGiven);
        RaiseNetworkEvent(new AvailableCommendsMessage(remaining), args.SenderSession);
    }

    private async void OnRequestMyCommends(RequestMyCommendsMessage msg, EntitySessionEventArgs args)
    {
        var userId = args.SenderSession.UserId;

        // Fetch all commends including private ones (it's the player's own)
        var allCommends = await _dbManager.GetPlayerCommends(userId.UserId, includePrivate: true);
        var recent = allCommends.Take(10).ToList();

        var entries = new List<CommendEntryData>();
        foreach (var c in recent)
        {
            string giverName;
            if (c.IsPrivate)
            {
                giverName = "Anonymous";
            }
            else
            {
                giverName = await _dbManager.GetCharacterNameByProfileIdAsync(c.GiverProfileId) ?? "Unknown";
            }

            entries.Add(new CommendEntryData(
                c.Comment ?? "",
                giverName,
                c.IsPrivate,
                c.CreatedAt));
        }

        RaiseNetworkEvent(new MyCommendsMessage(entries), args.SenderSession);
    }
}
