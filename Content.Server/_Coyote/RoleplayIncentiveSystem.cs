using System.Linq;
using Content.Server._NF.Bank;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Chat;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._Coyote;

/// <summary>
/// This handles...
/// </summary>
public sealed class RoleplayIncentiveSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = null!;
    [Dependency] private readonly BankSystem _bank = null!;
    [Dependency] private readonly PopupSystem _popupSystem = null!;
    [Dependency] private readonly ChatSystem _chatsys = null!;

    private const float GoodlenSpeaking = 75;
    private const float GoodlenWhispering = 75;
    private const float GoodlenEmoting = 50;
    private const float GoodlenQuickEmoting = 0;
    private const float GoodlenSubtling = 100;
    private const float GoodlenRadio = 50; // idk

    private const bool ListenerMultSpeaking = true;
    private const bool ListenerMultWhispering = true;
    private const bool ListenerMultEmoting = true;
    private const bool ListenerMultQuickEmoting = false;
    private const bool ListenerMultSubtling = true;
    private const bool ListenerMultRadio = false; // idk

    private const int MaxListenerMult = 5;

    private const int TaxBracket1 = 15000;
    private const int TaxBracket2 = 40000;
    private const int TaxBracket3 = 100000;
    private const int TaxBracket1Flat = 50;
    private const int TaxBracket2Flat = 40;
    private const int TaxBracket3Flat = 20;
    private const int TaxBracketRest = 10;

    /// <inheritdoc/>
    public override void Initialize()
    {
        // get the component this thing is attached to
        SubscribeLocalEvent<RoleplayIncentiveComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<RoleplayIncentiveComponent, RoleplayIncentiveEvent>(OnGotRoleplayIncentiveEvent);
    }

    private void OnComponentInit(EntityUid uid, RoleplayIncentiveComponent component, ComponentInit args)
    {
        // set the next payward time
        component.NextPayward = _timing.CurTime + component.PaywardInterval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<RoleplayIncentiveComponent>();
        while (query.MoveNext(out var uid, out var rpic))
        {
            if (_timing.CurTime < rpic.NextPayward)
                continue;
            rpic.NextPayward = _timing.CurTime + rpic.PaywardInterval;
            // check if they have a bank account
            if (!TryComp<BankAccountComponent>(uid, out _))
            {
                continue; // no bank account, no pramgle
            }
            // pay the player
            UpdatePayward(uid, rpic);
        }
    }

    /// <summary>
    /// This is called when a roleplay incentive event is received.
    /// It checks if it should be done, then it does it when it happensed
    /// </summary>
    /// <param name="uid">The entity that did the thing</param>
    /// <param name="rpic">The roleplay incentive component on the entity</param>
    /// <param name="args">The roleplay incentive event that was received</param>
    /// <remarks>
    /// piss
    /// </remarks>
    private void OnGotRoleplayIncentiveEvent(
        EntityUid uid,
        RoleplayIncentiveComponent rpic,
        RoleplayIncentiveEvent args)
    {
        // first, check if the uid has the component
        if (!TryComp<RoleplayIncentiveComponent>(uid, out var incentive))
        {
            Log.Warning($"RoleplayIncentiveComponent not found on entity {uid}!");
            return;
        } // i guess?
        // then, check if the channel in the args can be translated to a RoleplayAct
        var actOut = GetRoleplayActFromChannel(args.Channel);
        if (actOut == RoleplayActs.None)
        {
            return; // lot of stuff happens and it dont
        }
        // if its EmotingOrQuickEmoting, we need to doffgerentiate thewween the tween the two
        if (actOut == RoleplayActs.EmotingOrQuickEmoting)
        {
            actOut = DoffgerentiateEmotingAndQuickEmoting(
                args.Source,
                args.Message
                );
        }
        // make the thing
        var action = new RoleplayAction(
            actOut,
            _timing.CurTime,
            args.Message,
            args.PeoplePresent
        );
        // add it to the actions taken
        incentive.ActionsTaken.Add(action);
        // and we're good
    }

    /*
     * None
     * Local -> RoleplayActs.Speaking
     * Whisper -> RoleplayActs.Whispering
     * Server
     * Damage
     * Radio -> RoleplayActs.Radio
     * LOOC
     * OOC
     * Visual
     * Notifications
     * Emotes -> RoleplayActs.Emoting OR RoleplayActs.QuickEmoting
     * Dead
     * Admin
     * AdminAlert
     * AdminChat
     * Unspecified
     * Telepathic
     * Subtle -> RoleplayActs.Subtling
     * rest are just null
     */
    private static RoleplayActs GetRoleplayActFromChannel(ChatChannel channel)
    {
        // this is a bit of a hack, but it works
        return channel switch
        {
            ChatChannel.Local => RoleplayActs.Speaking,
            ChatChannel.Whisper => RoleplayActs.Whispering,
            ChatChannel.Emotes => RoleplayActs.EmotingOrQuickEmoting, // we dont know yet
            ChatChannel.Radio => RoleplayActs.Radio,
            ChatChannel.Subtle => RoleplayActs.Subtling,
            // the rest are not roleplay actions
            _ => RoleplayActs.None,
        };
    }

    private RoleplayActs DoffgerentiateEmotingAndQuickEmoting(
        EntityUid source,
        string message
        )
    {
        if (_chatsys.TryEmoteChatInput(source, message))
        {
            // if the message is a valid emote, then its a quick emote
            return RoleplayActs.QuickEmoting;
        }
        return RoleplayActs.Emoting;

        // well i cant figure out how the system does it, so im just gonnasay if theres
        // no spaces, its a quick emote
        // return !message.Contains(' ')
        //     ? RoleplayActs.QuickEmoting
        //     // otherwise, its a normal emote
        //     : RoleplayActs.Emoting;
    }

    /// <summary>
    /// Goes through all the relevant actions taken and stored, judges them,
    /// And gives the player a payward if they did something good.
    /// It also checks for things like duplicate actions, if theres people around, etc.
    /// Basically if you do stuff, you get some pay for it!
    /// </summary>
    private void UpdatePayward(EntityUid uid, RoleplayIncentiveComponent rpic)
    {
        //first check if this rpic is actually on the uid
        if (!TryComp<RoleplayIncentiveComponent>(uid, out var incentive))
        {
            Log.Warning($"RoleplayIncentiveComponent not found on entity {uid}!");
            return;
        }

        // go through all the actions, and judge them into a cooler format
        var bestSay        = 0f;
        var bestWhisper    = 0f;
        var bestEmote      = 0f;
        var bestQuickEmote = 0f;
        var bestSubtle     = 0f;
        var bestRadio      = 0f;
        // go through all the actions taken, sort and judge them
        var actionsToRemove = new List<RoleplayAction>();
        foreach (var action in incentive.ActionsTaken.Where(action => !(action.Judgement > 0)))
        {
            JudgeAction(action, out var judgement);
            // slot it into the best action for that type
            switch (action.Action)
            {
                case RoleplayActs.Speaking:
                    if (judgement > bestSay)
                    {
                        bestSay = judgement;
                    }

                    break;
                case RoleplayActs.Whispering:
                    if (judgement > bestWhisper)
                    {
                        bestWhisper = judgement;
                    }

                    break;
                case RoleplayActs.Emoting:
                    if (judgement > bestEmote)
                    {
                        bestEmote = judgement;
                    }

                    break;
                case RoleplayActs.QuickEmoting:
                    if (judgement > bestQuickEmote)
                    {
                        bestQuickEmote = judgement;
                    }

                    break;
                case RoleplayActs.Subtling:
                    if (judgement > bestSubtle)
                    {
                        bestSubtle = judgement;
                    }

                    break;
                case RoleplayActs.Radio:
                    if (judgement > bestRadio)
                    {
                        bestRadio = judgement;
                    }

                    break;
                default:
                    Log.Warning($"Unknown roleplay action {action.Action} on entity {uid}!");
                    break;
            }
            action.Judgement = judgement; // set the judgement on the action
            actionsToRemove.Add(action); // add the action to the removal list
        }
        foreach (var action in actionsToRemove)
        {
            incentive.ActionsTaken.Remove(action); // remove actions after iteration
        }
        var judgeAmount = (int) (bestSay + bestWhisper + bestEmote + bestQuickEmote + bestSubtle + bestRadio);
        var payFlat = judgeAmount switch
        {
            < TaxBracket1 => TaxBracket1Flat,
            < TaxBracket2 => TaxBracket2Flat,
            < TaxBracket3 => TaxBracket3Flat,
            _ => TaxBracketRest,
        };
        var payAmount = Math.Clamp(judgeAmount * payFlat, 20, int.MaxValue); // at least 20 bucks, bui
        // pay the player
        if (!_bank.TryBankDeposit(uid, payAmount))
        {
            Log.Warning($"Failed to deposit {payAmount} into bank account of entity {uid}!");
            return;
        }
        if (payAmount <= 100)
        {
            // if the pay amount is less than or equal to 100, we don't need to tell them
            return;
        }
        // tell the player they got paid!
        var message = Loc.GetString("coyote-rp-incentive-payward-message",
            ("amount", payAmount)
        );
        _popupSystem.PopupEntity(
            message,
            uid,
            uid
            );
    }

    /// <summary>
    /// Passes judgement on the action
    /// Based on a set of criteria, it will return a judgement value
    /// It will be judged based on:
    /// - How long the text was
    /// - How many people were present
    /// - and thats it for now lol
    /// </summary>
    private void JudgeAction(RoleplayAction action, out float judgement)
    {
        var lengthMult = GetMessageLengthMultiplier(action.Action, action.Message?.Length ?? 1);
        var listenerMult = GetListenerMultiplier(action.Action, action.PeoplePresent);
        // if the action is a quick emote, it gets no judgement
        judgement = lengthMult + listenerMult + 1f;
    }

    /// <summary>
    /// Gets the multiplier for the number of listeners present
    /// </summary>
    /// <param name="action">The action being performed</param>
    /// <param name="listeners">The number of listeners present</param>
    private float GetListenerMultiplier(RoleplayActs action, int listeners)
    {
        // if there are no listeners, return 0
        if (listeners <= 0)
            return 1f;
        var shouldMult = action switch
        {
            RoleplayActs.Speaking => ListenerMultSpeaking,
            RoleplayActs.Whispering => ListenerMultWhispering,
            RoleplayActs.Emoting => ListenerMultEmoting,
            RoleplayActs.QuickEmoting => ListenerMultQuickEmoting,
            RoleplayActs.Subtling => ListenerMultSubtling,
            RoleplayActs.Radio => ListenerMultRadio,
            _ => false,
        };
        if (!shouldMult)
        {
            // if the action does not have a multiplier, return 1
            return 1f;
        }

        // clamp the multiplier to a maximum of MaxListenerMult
        return Math.Clamp(listeners * listeners, 0f, MaxListenerMult);
    }
    /// <summary>
    /// Gets the message length multiplier for the action
    /// </summary>
    /// <param name="action">The action being performed</param>
    /// <param name="messageLength">The length of the message</param>
    private float GetMessageLengthMultiplier(RoleplayActs action, int messageLength)
    {
        // if the message length is 0, return 1
        if (messageLength <= 0)
            return 1f;

        if (action == RoleplayActs.QuickEmoting)
        {
            // if the action is a quick emote, return 0
            return 1f;
        }

        // get the good length for this action
        var goodlen = action switch
        {
            RoleplayActs.Speaking => GoodlenSpeaking,
            RoleplayActs.Whispering => GoodlenWhispering,
            RoleplayActs.Emoting => GoodlenEmoting,
            RoleplayActs.Subtling => GoodlenSubtling,
            RoleplayActs.Radio => GoodlenRadio,
            _ => 50,
        };

        // if the message length is less than the good length, return 1
        if (messageLength < goodlen)
            return 1f;

        // otherwise, return the message length divided by the good length
        return Math.Clamp(messageLength / goodlen, 1f, 5f);
    }
}
