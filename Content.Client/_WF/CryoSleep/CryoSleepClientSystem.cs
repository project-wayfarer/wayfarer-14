// Wayfarer: Character resume from cryosleep feature - client side
using Content.Shared._WF.CryoSleep;
using Robust.Shared.GameObjects;

namespace Content.Client._WF.CryoSleep;

/// <summary>
/// Client-side system for handling cryo sleep network events
/// </summary>
public sealed class CryoSleepClientSystem : EntitySystem
{
    public event Action<GetStoredCharactersResponseMessage>? OnCharactersResponse;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GetStoredCharactersResponseMessage>(OnGetStoredCharactersResponse);
    }

    private void OnGetStoredCharactersResponse(GetStoredCharactersResponseMessage msg)
    {
        OnCharactersResponse?.Invoke(msg);
    }

    public void RequestStoredCharacters()
    {
        RaiseNetworkEvent(new GetStoredCharactersRequestMessage());
    }

    public void RequestResumeCharacter(NetEntity body)
    {
        RaiseNetworkEvent(new ResumeCharacterRequestMessage(body));
    }
}
