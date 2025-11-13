using Photon.Pun;
using Photon.Realtime;
using System;
using Random = UnityEngine.Random;
using UnityEngine;

public class TimerManager : MonoBehaviourPunCallbacks
{
    // So other scripts can easily access this script by using 'TimerManager.Instance'. '{ get; private set; }' means only this script can set this value.
    public static TimerManager Instance { get; private set; }

    // These events allow other scripts to be notified only whenever the timer or target is changed.
    public static event Action<float> OnCurrentTimerChanged;
    public static event Action<Player> OnCurrentTargetChanged;

    // After the timer is passed, how long it takes before the timer can be passed again.
    const float timerPassCooldown = 1f;

    // CurrentTimer and CurrentTarget are essentially easy ways to access and set the actual values of currentTimer and currentTarget. 
    // Even within this script, only use CurrentTimer and CurrentTarget or SetTimer and SetTarget instead of directly setting currentTimer or currentTarget.
    public static float CurrentTimer { get { return Instance.currentTimer; } set { Instance.SetTimer(value); } }
    public static Player CurrentTarget { get { return Instance.currentTarget; } set { Instance.SetTarget(value); } }

    [SerializeField] float timerDuration = 60f;
    [SerializeField] float timerCooldown = 2.5f;

    float lastTargetChange = -999999f; // Set to a very low value so there are no issues with the target being unable to be switched.
    float lastDetonation = 999999f; // Set to a very high value so there are no issues with the code trying to switch players before someone has detonated.
    float currentTimer;
    Player currentTarget;

    // regions are a sort of special marker that can be used for code organisation within Visual Studio and other code editors / IDEs. You can collapse and expand regions to view different parts of code as needed.
    #region Initialisation
    void Awake ()
    {
        // If there is already a TimerManager, destroy this one (you don't want to have two at once). Otherwise, make this the "Instance".
        if (Instance)
        {
            // nameof gets the script's name, and will still work if the script is renamed.
            Debug.Log($"An instance of {nameof(TimerManager)} already exists. Destroying this {nameof(TimerManager)}");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start ()
    {
        // Stop execution of this function if we aren't the Master Client.
        // The if (condition) return; statement is useful for code clarity, as it means you don't have to wrap the function's code in curly brackets {}.
        if (!PhotonNetwork.IsMasterClient)
            return;

        // Separate initialistion into a separate function for code clarity.
        Initialise();
    }

    void Initialise ()
    {
        SetTimer(timerDuration);
        FindRandomTarget();
    }

    #endregion

    #region Player Joining

    // This is to update new players to tell them the current target and timer.
    public override void OnPlayerEnteredRoom (Player newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        photonView.RPC(nameof(RPC_SetTimer), newPlayer, currentTimer);
        photonView.RPC(nameof(RPC_SetTarget), newPlayer, currentTarget.ActorNumber);
    }

    #endregion

    #region Countdowns

    // Personally, I like to use FixedUpdate for pretty much any case where actual game logic is running.
    void FixedUpdate ()
    {
        SetTimer(currentTimer - Time.fixedDeltaTime, true);
        TrySelectNewTarget();
    }

    #endregion

    #region Timer and Target Setting

    // This function is public in case you want to access it from other scripts.
    public void FindRandomTarget ()
    {
        Player[] players = PhotonNetwork.PlayerList;

        int playerIndex = Random.Range(0, players.Length);
        Player randomTarget = players[playerIndex];

        SetTarget(randomTarget);
    }

    // This is for clients to call, in order to ask the Master Client to change the target.
    public void RequestTargetChange (Player player)
    {
        photonView.RPC(nameof(RPC_RequestTargetChange), PhotonNetwork.MasterClient, player.ActorNumber);
    }

    void SetTimer (float timerValue)
    {
        // Call the other variant of this functions to avoid having to write the code twice.
        SetTimer(timerValue, false);
    }

    void SetTimer (float timerValue, bool local)
    {
        if (!PhotonNetwork.InRoom) return;

        if (local)
        {
            photonView.RPC(nameof(RPC_SetTimer), PhotonNetwork.LocalPlayer, timerValue);
            return;
        }

        if (!PhotonNetwork.InRoom) return;
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(nameof(RPC_SetTimer), RpcTarget.All, timerValue); // Call RPC_SetTimer on everyone. This makes the timer syncing logic the same for all players.
    }

    void SetTarget (Player target)
    {
        // Call the other variant of this functions to avoid having to write the code twice.
        SetTarget(target.ActorNumber);
    }

    void SetTarget (int targetActorNumber)
    {
        if(!PhotonNetwork.InRoom) return;
        if (!PhotonNetwork.IsMasterClient) return;

        // Don't set the target or send the message that the target has been set, if the target was changed recently.
        if (Time.time < lastTargetChange + timerPassCooldown)
            return;

        if (!PhotonNetwork.InRoom) return;
        photonView.RPC(nameof(RPC_SetTarget), RpcTarget.All, targetActorNumber);
    }

    // PhotonMessageInfo can be added to an RPC's parameters to automatically get extra information about the RPC.
    [PunRPC]
    void RPC_SetTimer (float timerValue, PhotonMessageInfo info)
    {
        // Don't execute this code if it's not from the MasterClient or yourself.
        if (info.Sender != PhotonNetwork.MasterClient && info.Sender != PhotonNetwork.LocalPlayer)
            return;

        currentTimer = timerValue;

        OnCurrentTimerChanged?.Invoke(timerValue);
    }

    [PunRPC]
    void RPC_SetTarget (int targetActorNumber, PhotonMessageInfo info)
    {
        // Don't execute this code if it's not from the MasterClient or yourself.
        if (info.Sender != PhotonNetwork.MasterClient && info.Sender != PhotonNetwork.LocalPlayer)
            return;

        currentTarget = PhotonNetwork.CurrentRoom.GetPlayer(targetActorNumber, true);
        lastTargetChange = Time.time;

        OnCurrentTargetChanged?.Invoke(currentTarget);
    }

    [PunRPC]
    void RPC_RequestTargetChange (int targetActorNumber, PhotonMessageInfo info)
    {
        // Validation check: Stop running this code if the sender is not the current target. Ignore this if the sender is the MasterClient.
        if (CurrentTarget != info.Sender && info.Sender != PhotonNetwork.MasterClient)
            return;

        SetTarget(targetActorNumber);
    }

    #endregion

    #region Detonation
    // This region manages selecting players after a detonation has occured.

    public void PlayerDetonated ()
    {
        lastDetonation = Time.time;
    }

    void TrySelectNewTarget ()
    {
        // Don't execute code if the timer is still cooling down, or if the timer is still going.
        if (!PhotonNetwork.IsMasterClient)
            return;
        if (Time.time < lastDetonation + timerCooldown)
            return;
        if (currentTimer > 0)
            return;

        SetTimer(timerDuration);
        FindRandomTarget();
    }

    #endregion

    #region Utilities

    public static bool IsLocalPlayerTarget ()
    {
        return CurrentTarget == PhotonNetwork.LocalPlayer;
    }

    #endregion

}
