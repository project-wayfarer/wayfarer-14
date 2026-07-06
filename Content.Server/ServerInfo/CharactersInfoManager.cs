using System;
using System.Linq;
using System.Net;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Content.Server.Preferences.Managers;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Robust.Server.Player;
using Robust.Server.ServerStatus;
using Robust.Shared.Asynchronous;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.ServerInfo;

/// <summary>
/// Provides the /characters endpoint that returns connected players and their character names
/// </summary>
public sealed class CharactersInfoManager
{
    [Dependency] private readonly IStatusHost _statusHost = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly ITaskManager _taskManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public void Initialize()
    {
        _statusHost.AddHandler(HandleCharactersRequest);
        // Wayfarer
        _statusHost.AddHandler(HandleShiftTimeRemainingRequest);
        // End Wayfarer
    }

    private async Task<bool> HandleCharactersRequest(IStatusHandlerContext context)
    {
        if (!context.IsGetLike || context.Url.AbsolutePath != "/characters")
        {
            return false;
        }

        var jObject = new JsonObject();
        var characters = new JsonArray();
        var hiddenCount = 0;

        foreach (var session in _playerManager.Sessions)
        {
            // Check if player has preferences and wants to hide from playerlist
            if (_prefsManager.TryGetCachedPreferences(session.UserId, out var prefs))
            {
                if (prefs.SelectedCharacter is HumanoidCharacterProfile profile && profile.HideFromPlayerlist)
                {
                    // Skip this character - they don't want to appear on the web playerlist
                    hiddenCount++;
                    continue;
                }
            }

            var character = new JsonObject
            {
                ["username"] = session.Name
            };

            // Add character IC name if player has a spawned entity
            if (session.AttachedEntity != null)
            {
                character["characterName"] = Identity.Name(session.AttachedEntity.Value, _entityManager);
            }
            else
            {
                character["characterName"] = null;
            }

            // Add profile ID from the database
            int? profileId = null;
            if (prefs != null)
            {
                var selectedSlot = prefs.SelectedCharacterIndex;
                profileId = await _db.GetProfileIdAsync(session.UserId, selectedSlot);
            }
            character["profileId"] = profileId;

            characters.Add(character);
        }

        jObject["characters"] = characters;
        jObject["hiddenCount"] = hiddenCount;

        context.ResponseHeaders["Content-Type"] = "application/json";
        context.ResponseHeaders["Access-Control-Allow-Origin"] = "*";
        context.ResponseHeaders["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        context.ResponseHeaders["Access-Control-Allow-Headers"] = "Content-Type";

        await context.RespondAsync(jObject.ToJsonString(), HttpStatusCode.OK, "application/json");
        return true;
    }

    // Wayfarer
    private async Task<bool> HandleShiftTimeRemainingRequest(IStatusHandlerContext context)
    {
        if (!context.IsGetLike || context.Url.AbsolutePath != "/shift-time-remaining")
        {
            return false;
        }

        var responseData = await RunOnMainThread(() =>
        {
            var ticker = _entityManager.System<GameTicker>();

            var hasShiftEndTime = ticker.RunLevel == GameRunLevel.InRound && ticker.ShiftEndTime.HasValue;
            var timeRemaining = TimeSpan.Zero;
            DateTime? shiftEndTimeUtc = null;

            if (hasShiftEndTime)
            {
                var remaining = ticker.ShiftEndTime!.Value - _timing.RealTime;
                if (remaining > TimeSpan.Zero)
                {
                    timeRemaining = remaining;
                    shiftEndTimeUtc = DateTime.UtcNow + remaining;
                }
                else
                {
                    shiftEndTimeUtc = DateTime.UtcNow;
                }
            }

            return (hasShiftEndTime, timeRemaining, shiftEndTimeUtc);
        });

        var jObject = new JsonObject
        {
            ["hasShiftEndTime"] = responseData.hasShiftEndTime,
            ["timeRemainingSeconds"] = (int) Math.Ceiling(responseData.timeRemaining.TotalSeconds),
            ["shiftEndTimeUtc"] = responseData.shiftEndTimeUtc?.ToString("o")
        };

        context.ResponseHeaders["Content-Type"] = "application/json";
        context.ResponseHeaders["Access-Control-Allow-Origin"] = "*";
        context.ResponseHeaders["Access-Control-Allow-Methods"] = "GET, OPTIONS";
        context.ResponseHeaders["Access-Control-Allow-Headers"] = "Content-Type";

        await context.RespondAsync(jObject.ToJsonString(), HttpStatusCode.OK, "application/json");
        return true;
    }
    // End Wayfarer

    private async Task<T> RunOnMainThread<T>(Func<T> func)
    {
        var taskCompletionSource = new TaskCompletionSource<T>();
        _taskManager.RunOnMainThread(() =>
        {
            try
            {
                taskCompletionSource.TrySetResult(func());
            }
            catch (Exception e)
            {
                taskCompletionSource.TrySetException(e);
            }
        });

        return await taskCompletionSource.Task;
    }
}
