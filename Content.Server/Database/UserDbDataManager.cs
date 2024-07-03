using System.Threading;
using System.Threading.Tasks;
using Content.Server.Players.PlayTimeTracking;
using Content.Server.Consent;
using Content.Server.Preferences.Managers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Database;

/// <summary>
/// Manages per-user data that comes from the database. Ensures it is loaded efficiently on client connect,
/// and ensures data is loaded before allowing players to spawn or such.
/// </summary>
/// <remarks>
/// Actual loading code is handled by separate managers such as <see cref="IServerPreferencesManager"/>.
/// This manager is simply a centralized "is loading done" controller for other code to rely on.
/// </remarks>
public sealed class UserDbDataManager : IPostInjectInit
{
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private readonly IServerConsentManager _consent = default!;

    private readonly Dictionary<NetUserId, UserData> _users = new();
    private readonly List<OnLoadPlayer> _onLoadPlayer = [];
    private readonly List<OnFinishLoad> _onFinishLoad = [];
    private readonly List<OnPlayerDisconnect> _onPlayerDisconnect = [];

    private ISawmill _sawmill = default!;

    // TODO: Ideally connected/disconnected would be subscribed to IPlayerManager directly,
    // but this runs into ordering issues with game ticker.
    public void ClientConnected(ICommonSession session)
    {
        _sawmill.Verbose($"Initiating load for user {session}");

        DebugTools.Assert(!_users.ContainsKey(session.UserId), "We should not have any cached data on client connect.");

        var cts = new CancellationTokenSource();
        var task = Load(session, cts.Token);
        var data = new UserData(cts, task);

        _users.Add(session.UserId, data);
    }

    public void ClientDisconnected(ICommonSession session)
    {
        // Harmony Queue Start
        if (!_users.ContainsKey(session.UserId))
            return; // No session to clean up, was in the queue and not the game
        // Harmoney Queue End
        _users.Remove(session.UserId, out var data);
        if (data == null)
            throw new InvalidOperationException("Did not have cached data in ClientDisconnect!");

        data.Cancel.Cancel();
        data.Cancel.Dispose();

        _prefs.OnClientDisconnected(session);
        _playTimeTracking.ClientDisconnected(session);
        _consent.OnClientDisconnected(session); //TODO: use new AddOnPlayerDisconnect in consent manager instead?
    }

    private async Task Load(ICommonSession session, CancellationToken cancel)
    {
        await Task.WhenAll(
            _prefs.LoadData(session, cancel),
            _playTimeTracking.LoadData(session, cancel),
            _consent.LoadData(session, cancel)); // TODO: use the new AddOnLoadPlayer instead, that was added in #28085
    }

    public Task WaitLoadComplete(ICommonSession session)
    {
        return _users[session.UserId].Task;
    }

    public bool IsLoadComplete(ICommonSession session)
    {
        return GetLoadTask(session).IsCompletedSuccessfully;
    }

    public Task GetLoadTask(ICommonSession session)
    {
        return _users[session.UserId].Task;
    }

    public void AddOnLoadPlayer(OnLoadPlayer action)
    {
        _onLoadPlayer.Add(action);
    }

    public void AddOnFinishLoad(OnFinishLoad action)
    {
        _onFinishLoad.Add(action);
    }

    public void AddOnPlayerDisconnect(OnPlayerDisconnect action)
    {
        _onPlayerDisconnect.Add(action);
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("userdb");
    }

    private sealed record UserData(CancellationTokenSource Cancel, Task Task);

    public delegate Task OnLoadPlayer(ICommonSession player, CancellationToken cancel);

    public delegate void OnFinishLoad(ICommonSession player);

    public delegate void OnPlayerDisconnect(ICommonSession player);
}
