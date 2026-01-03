using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.Server._DV.CustomObjectiveSummary;
using Content.Server._NF.Bank;
using Content.Server._NF.GameRule.Components;
using Content.Server._NF.GameTicking.Events;
using Content.Server.Cargo.Components;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules;
using Content.Server.Preferences.Managers;
using Content.Server._NF.ShuttleRecords;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Robust.Server;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._NF.GameRule;

/// <summary>
/// This handles the dungeon and trading post spawning, as well as round end capitalism summary
/// </summary>
public sealed class NFAdventureRuleSystem : GameRuleSystem<NFAdventureRuleComponent>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly PointOfInterestSystem _poi = default!;
    [Dependency] private readonly IBaseServer _baseServer = default!;
    [Dependency] private readonly IEntitySystemManager _entSys = default!;
    [Dependency] private readonly ShuttleRecordsSystem _shuttleRecordsSystem = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly CustomObjectiveSummarySystem _customObjectiveSummary = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;

    private readonly HttpClient _httpClient = new();

    private readonly ProtoId<GamePresetPrototype> _fallbackPresetID = "NFPirates";
    private ISawmill _sawmill = default!;
    private DateTime _roundStartTime;

    public sealed class PlayerRoundBankInformation
    {
        // Initial balance, obtained on spawn
        public int StartBalance;
        // Ending balance, obtained on game end or detach (NOTE: multiple detaches possible), whichever happens first.
        public int EndBalance;
        // Entity name: used for display purposes ("The Feel of Fresh Bills earned 100,000 spesos")
        public string Name;
        // User ID: used to validate incoming information.
        // If, for whatever reason, another player takes over this character, their initial balance is inaccurate.
        public NetUserId UserId;
        // Job/Role name
        public string Role;

        public PlayerRoundBankInformation(int startBalance, string name, NetUserId userId, string role)
        {
            StartBalance = startBalance;
            EndBalance = -1;
            Name = name;
            UserId = userId;
            Role = role;
        }
    }

    // A list of player bank account information stored by the controlled character's entity.
    [ViewVariables]
    private Dictionary<EntityUid, PlayerRoundBankInformation> _players = new();

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawningEvent);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetachedEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _player.PlayerStatusChanged += PlayerManagerOnPlayerStatusChanged;
        _sawmill = Logger.GetSawmill("debris");
    }

    protected override void AppendRoundEndText(EntityUid uid, NFAdventureRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent ev)
    {
        _sawmill.Info("AppendRoundEndText called! Starting round end processing...");
        ev.AddLine(Loc.GetString("adventure-list-start"));
        var allScore = new List<Tuple<string, int>>();

        var sortedPlayers = _players.ToList();
        sortedPlayers.Sort((p1, p2) => p1.Value.Name.CompareTo(p2.Value.Name));

        foreach (var (player, playerInfo) in sortedPlayers)
        {
            var endBalance = playerInfo.EndBalance;
            if (_bank.TryGetBalance(player, out var bankBalance))
            {
                endBalance = bankBalance;
            }

            // Check if endBalance is valid (non-negative)
            if (endBalance < 0)
                continue;

            var profit = endBalance - playerInfo.StartBalance;
            string summaryText;
            if (profit < 0)
            {
                summaryText = Loc.GetString("adventure-list-loss", ("amount", BankSystemExtensions.ToSpesoString(-profit)));
            }
            else
            {
                summaryText = Loc.GetString("adventure-list-profit", ("amount", BankSystemExtensions.ToSpesoString(profit)));
            }
            ev.AddLine($"- {playerInfo.Name} {summaryText}");
            allScore.Add(new Tuple<string, int>(playerInfo.Name, profit));
        }

        // Save round summary to database (do this regardless of score count)
        _ = SaveRoundSummaryToDatabase(allScore);

        if (!(allScore.Count >= 1))
            return;

        var relayText = Loc.GetString("adventure-webhook-list-high");
        relayText += '\n';
        var highScore = allScore.OrderByDescending(h => h.Item2).ToList();

        for (var i = 0; i < 10 && highScore.Count > 0; i++)
        {
            if (highScore.First().Item2 < 0)
                break;
            var profitText = Loc.GetString("adventure-webhook-top-profit", ("amount", BankSystemExtensions.ToSpesoString(highScore.First().Item2)));
            relayText += $"{highScore.First().Item1} {profitText}";
            relayText += '\n';
            highScore.RemoveAt(0);
        }
        relayText += '\n'; // Extra line separating the highest and lowest scores
        relayText += Loc.GetString("adventure-webhook-list-low");
        relayText += '\n';
        highScore.Reverse();
        for (var i = 0; i < 10 && highScore.Count > 0; i++)
        {
            if (highScore.First().Item2 > 0)
                break;
            var lossText = Loc.GetString("adventure-webhook-top-loss", ("amount", BankSystemExtensions.ToSpesoString(-highScore.First().Item2)));
            relayText += $"{highScore.First().Item1} {lossText}";
            relayText += '\n';
            highScore.RemoveAt(0);
        }
        // Fire and forget.
        _ = ReportRound(relayText);
        _ = ReportLedger();
        _ = ReportShipyardStats();
    }

    private void OnPlayerSpawningEvent(PlayerSpawnCompleteEvent ev)
    {
        if (ev.Player.AttachedEntity is { Valid: true } mobUid)
        {
            EnsureComp<CargoSellBlacklistComponent>(mobUid);

            // Store player info with the bank balance - we have it directly, and BankSystem won't have a cache yet.
            if (!_players.ContainsKey(mobUid)
                && HasComp<BankAccountComponent>(mobUid))
            {
                // Get the player's job/role
                var role = "Unknown";
                if (ev.JobId != null)
                {
                    role = ev.JobId;
                }
                
                _players[mobUid] = new PlayerRoundBankInformation(ev.Profile.BankBalance, MetaData(mobUid).EntityName, ev.Player.UserId, role);
            }
        }
    }

    private void OnPlayerDetachedEvent(PlayerDetachedEvent ev)
    {
        if (ev.Entity is not { Valid: true } mobUid)
            return;

        if (_players.ContainsKey(mobUid))
        {
            if (_players[mobUid].UserId == ev.Player.UserId &&
                _bank.TryGetBalance(ev.Player, out var bankBalance))
            {
                _players[mobUid].EndBalance = bankBalance;
            }
        }
    }

    private void PlayerManagerOnPlayerStatusChanged(object? _, SessionStatusEventArgs e)
    {
        // Treat all disconnections as being possibly final.
        if (e.NewStatus != SessionStatus.Disconnected ||
            e.Session.AttachedEntity == null)
            return;

        var mobUid = e.Session.AttachedEntity.Value;
        if (_players.ContainsKey(mobUid))
        {
            if (_players[mobUid].UserId == e.Session.UserId &&
                _bank.TryGetBalance(e.Session, out var bankBalance))
            {
                _players[mobUid].EndBalance = bankBalance;
            }
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _players.Clear();
    }

    protected override void Started(EntityUid uid, NFAdventureRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _roundStartTime = DateTime.UtcNow;
        _sawmill.Info($"NFAdventure rule started! Round start time recorded: {_roundStartTime}");
        var mapUid = GameTicker.DefaultMap;

        //First, we need to grab the list and sort it into its respective spawning logics
        List<PointOfInterestPrototype> depotProtos = new();
        List<PointOfInterestPrototype> marketProtos = new();
        List<PointOfInterestPrototype> requiredProtos = new();
        List<PointOfInterestPrototype> optionalProtos = new();
        Dictionary<string, List<PointOfInterestPrototype>> remainingUniqueProtosBySpawnGroup = new();

        var currentPreset = _ticker.CurrentPreset?.ID ?? _fallbackPresetID;

        foreach (var location in _proto.EnumeratePrototypes<PointOfInterestPrototype>())
        {
            // Check if any preset is accepted (empty) or if current preset is supported.
            if (location.SpawnGamePreset.Length > 0 && !location.SpawnGamePreset.Contains(currentPreset))
                continue;

            if (location.SpawnGroup == "CargoDepot")
                depotProtos.Add(location);
            else if (location.SpawnGroup == "MarketStation")
                marketProtos.Add(location);
            else if (location.SpawnGroup == "Required")
                requiredProtos.Add(location);
            else if (location.SpawnGroup == "Optional")
                optionalProtos.Add(location);
            else // the remainder are done on a per-poi-per-group basis
            {
                if (!remainingUniqueProtosBySpawnGroup.ContainsKey(location.SpawnGroup))
                    remainingUniqueProtosBySpawnGroup[location.SpawnGroup] = new();
                remainingUniqueProtosBySpawnGroup[location.SpawnGroup].Add(location);
            }
        }
        _poi.GenerateDepots(mapUid, depotProtos, out component.CargoDepots);
        _poi.GenerateMarkets(mapUid, marketProtos, out component.MarketStations);
        _poi.GenerateRequireds(mapUid, requiredProtos, out component.RequiredPois);
        _poi.GenerateOptionals(mapUid, optionalProtos, out component.OptionalPois);
        _poi.GenerateUniques(mapUid, remainingUniqueProtosBySpawnGroup, out component.UniquePois);

        base.Started(uid, component, gameRule, args);

        // Using invalid entity, we don't have a relevant entity to reference here.
        RaiseLocalEvent(EntityUid.Invalid, new StationsGeneratedEvent(), broadcast: true); // TODO: attach this to a meaningful entity.
    }

    private async Task ReportRound(string message, int color = 0x77DDE7)
    {
        _sawmill.Info(message);
        string webhookUrl = _cfg.GetCVar(NFCCVars.DiscordLeaderboardWebhook);
        if (webhookUrl == string.Empty)
            return;

        var serverName = _baseServer.ServerName;
        var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();
        var runId = gameTicker != null ? gameTicker.RoundId : 0;

        var payload = new WebhookPayload
        {
            Embeds = new List<Embed>
            {
                new()
                {
                    Title = Loc.GetString("adventure-webhook-list-start"),
                    Description = message,
                    Color = color,
                    Footer = new EmbedFooter
                    {
                        Text = Loc.GetString(
                            "adventure-webhook-footer",
                            ("serverName", serverName),
                            ("roundId", runId)),
                    },
                },
            },
        };
        await SendWebhookPayload(webhookUrl, payload);
    }

    private async Task ReportLedger(int color = 0xBF863F)
    {
        string webhookUrl = _cfg.GetCVar(NFCCVars.DiscordLeaderboardWebhook);
        if (webhookUrl == string.Empty)
            return;

        var ledgerPrintout = _bank.GetLedgerPrintout();
        if (string.IsNullOrEmpty(ledgerPrintout))
            return;
        _sawmill.Info(ledgerPrintout);

        var serverName = _baseServer.ServerName;
        var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();
        var runId = gameTicker != null ? gameTicker.RoundId : 0;

        var payload = new WebhookPayload
        {
            Embeds = new List<Embed>
            {
                new()
                {
                    Title = Loc.GetString("adventure-webhook-ledger-start"),
                    Description = ledgerPrintout,
                    Color = color,
                    Footer = new EmbedFooter
                    {
                        Text = Loc.GetString(
                            "adventure-webhook-footer",
                            ("serverName", serverName),
                            ("roundId", runId)),
                    },
                },
            },
        };
        await SendWebhookPayload(webhookUrl, payload);
    }

    private async Task ReportShipyardStats(int color = 0x55DD3F)
    {
        string webhookUrl = _cfg.GetCVar(NFCCVars.DiscordLeaderboardWebhook);
        if (webhookUrl == string.Empty)
            return;

        var shipyardStats = _shuttleRecordsSystem.GetStatsPrintout();
        if (shipyardStats is null)
            return;

        var shipyardStatsPrintout = shipyardStats.Value.Item1;
        var serialisedData = shipyardStats.Value.Item2;

        Logger.InfoS("discord", shipyardStatsPrintout);

        var serverName = _baseServer.ServerName;
        var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();
        var runId = gameTicker != null ? gameTicker.RoundId : 0;

        var payload = new WebhookPayload
        {
            Embeds = new List<Embed>
            {
                new()
                {
                    Title = Loc.GetString("adventure-webhook-shipstats-start"),
                    Description = shipyardStatsPrintout,
                    Color = color,
                    Footer = new EmbedFooter
                    {
                        Text = Loc.GetString(
                            "adventure-webhook-footer",
                            ("serverName", serverName),
                            ("roundId", runId)),
                    },
                },
            },
        };

        MultipartFormDataContent form = new MultipartFormDataContent();
        var ser_payload = JsonSerializer.Serialize(payload);
        var content = new StringContent(ser_payload, Encoding.UTF8, "application/json");
        form.Add(content, "payload_json");
        if (serialisedData is not null)
        {
            form.Add(new ByteArrayContent(serialisedData, 0, serialisedData.Length), "Document", $"shipstats-{serverName}-{runId}.json");
        }
        await SendWebhookPayload(webhookUrl, form);
    }

    private async Task SendWebhookPayload(string webhookUrl, WebhookPayload payload)
    {
        var ser_payload = JsonSerializer.Serialize(payload);
        var content = new StringContent(ser_payload, Encoding.UTF8, "application/json");
        var request = await _httpClient.PostAsync($"{webhookUrl}?wait=true", content);
        var reply = await request.Content.ReadAsStringAsync();
        if (!request.IsSuccessStatusCode)
        {
            _sawmill.Error($"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {reply}");
        }
    }

    private async Task SendWebhookPayload(string webhookUrl, MultipartFormDataContent payload)
    {
        var request = await _httpClient.PostAsync($"{webhookUrl}?wait=true", payload);
        var reply = await request.Content.ReadAsStringAsync();
        if (!request.IsSuccessStatusCode)
        {
            _sawmill.Error($"Discord returned bad status code when posting message: {request.StatusCode}\nResponse: {reply}");
        }
    }

    private async Task SaveRoundSummaryToDatabase(List<Tuple<string, int>> allScore)
    {
        try
        {
            _sawmill.Info("SaveRoundSummaryToDatabase: Starting...");
            
            var gameTicker = _entSys.GetEntitySystemOrNull<GameTicker>();
            if (gameTicker == null)
            {
                _sawmill.Warning("SaveRoundSummaryToDatabase: GameTicker is null");
                return;
            }

            var roundId = gameTicker.RoundId;
            var roundEndTime = DateTime.UtcNow;

            _sawmill.Info($"SaveRoundSummaryToDatabase: Round {roundId}, Players count: {_players.Count}");

            // Build profit/loss data with username and character name
            var profitLossData = new List<Dictionary<string, object>>();
            var playerManifestData = new List<Dictionary<string, object>>();

            var sortedPlayers = _players.ToList();
            sortedPlayers.Sort((p1, p2) => p1.Value.Name.CompareTo(p2.Value.Name));

            foreach (var (player, playerInfo) in sortedPlayers)
            {
                var endBalance = playerInfo.EndBalance;
                if (_bank.TryGetBalance(player, out var bankBalance))
                {
                    endBalance = bankBalance;
                }

                if (endBalance < 0)
                    continue;

                var profit = endBalance - playerInfo.StartBalance;
                
                // Get username from NetUserId
                var username = playerInfo.UserId.ToString();
                if (_player.TryGetSessionById(playerInfo.UserId, out var session))
                {
                    username = session.Name;
                }

                // Get profile ID for this character
                int? profileId = null;
                if (_prefsManager.TryGetCachedPreferences(playerInfo.UserId, out var prefs))
                {
                    var characterSlot = prefs.SelectedCharacterIndex;
                    profileId = await _db.GetProfileIdAsync(playerInfo.UserId, characterSlot);
                }

                // Add to profit/loss data
                profitLossData.Add(new Dictionary<string, object>
                {
                    { "username", username },
                    { "characterName", playerInfo.Name },
                    { "profitLoss", profit }
                });

                // Add to player manifest
                var manifestEntry = new Dictionary<string, object>
                {
                    { "username", username },
                    { "characterName", playerInfo.Name },
                    { "role", playerInfo.Role }
                };
                
                if (profileId.HasValue)
                {
                    manifestEntry["profileId"] = profileId.Value;
                }
                
                playerManifestData.Add(manifestEntry);
            }

            _sawmill.Info($"SaveRoundSummaryToDatabase: Profit/Loss entries: {profitLossData.Count}");

            // Serialize to JSON documents
            var profitLossJson = JsonDocument.Parse(JsonSerializer.Serialize(profitLossData));
            var playerManifestJson = JsonDocument.Parse(JsonSerializer.Serialize(playerManifestData));
            
            // Get player stories from CustomObjectiveSummarySystem
            var playerStoriesData = new List<Dictionary<string, object>>();
            var rawPlayerStories = _customObjectiveSummary.GetPlayerStories();
            
            foreach (var (userId, storyData) in rawPlayerStories)
            {
                // Get username from NetUserId
                var username = userId.ToString();
                if (_player.TryGetSessionById(userId, out var session))
                {
                    username = session.Name;
                }
                
                var storyEntry = new Dictionary<string, object>
                {
                    { "username", username },
                    { "characterName", storyData.CharacterName },
                    { "story", storyData.Story }
                };
                
                // Add profileId if available
                if (storyData.ProfileId.HasValue)
                {
                    storyEntry["profileId"] = storyData.ProfileId.Value;
                }
                
                playerStoriesData.Add(storyEntry);
            }
            
            var playerStoriesJson = JsonDocument.Parse(JsonSerializer.Serialize(playerStoriesData));
            
            _sawmill.Info($"SaveRoundSummaryToDatabase: Player stories count: {playerStoriesData.Count}");

            _sawmill.Info($"SaveRoundSummaryToDatabase: Calling database save for round {roundId}");

            // Save to database
            await _db.AddWayfarerRoundSummary(
                roundId,
                _roundStartTime,
                roundEndTime,
                profitLossJson,
                playerStoriesJson,
                playerManifestJson
            );

            _sawmill.Info($"Saved round {roundId} summary to database successfully");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to save round summary to database: {ex}");
        }
    }

    // https://discord.com/developers/docs/resources/channel#message-object-message-structure
    private struct WebhookPayload
    {
        [JsonPropertyName("username")] public string? Username { get; set; } = null;

        [JsonPropertyName("avatar_url")] public string? AvatarUrl { get; set; } = null;

        [JsonPropertyName("content")] public string Message { get; set; } = "";

        [JsonPropertyName("embeds")] public List<Embed>? Embeds { get; set; } = null;

        [JsonPropertyName("allowed_mentions")]
        public Dictionary<string, string[]> AllowedMentions { get; set; } =
            new()
            {
                { "parse", Array.Empty<string>() },
            };

        public WebhookPayload()
        {
        }
    }

    // https://discord.com/developers/docs/resources/channel#embed-object-embed-structure
    private struct Embed
    {
        [JsonPropertyName("title")] public string Title { get; set; } = "";

        [JsonPropertyName("description")] public string Description { get; set; } = "";

        [JsonPropertyName("color")] public int Color { get; set; } = 0;

        [JsonPropertyName("footer")] public EmbedFooter? Footer { get; set; } = null;

        public Embed()
        {
        }
    }

    // https://discord.com/developers/docs/resources/channel#embed-object-embed-footer-structure
    private struct EmbedFooter
    {
        [JsonPropertyName("text")] public string Text { get; set; } = "";

        [JsonPropertyName("icon_url")] public string? IconUrl { get; set; }

        public EmbedFooter()
        {
        }
    }
}
