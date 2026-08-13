using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    public static RelayManager Instance { get; private set; }

    private UnityTransport transport;

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        if (UnityServices.State == ServicesInitializationState.Uninitialized)
        {
            await UnityServices.InitializeAsync();
        }

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log("Unity Services initialized.");
    }

    public async Task<string> StartHost()
    {
        try
        {
            await InitializeUnityServices();

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(6);

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(
                    allocation.AllocationId
                );

            RelayServerData relayServerData =
                AllocationUtils.ToRelayServerData(allocation, "dtls");

            transport.SetRelayServerData(relayServerData);

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("Relay Host started!");
                Debug.Log("JOIN CODE: " + joinCode);

                return joinCode;
            }

            Debug.LogError("Failed to start Host.");
            return null;
        }
        catch (Exception e)
        {
            Debug.LogError("Relay Host error: " + e);
            return null;
        }
    }

    public async Task<bool> StartClient(string joinCode)
    {
        try
        {
            await InitializeUnityServices();

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            RelayServerData relayServerData =
                AllocationUtils.ToRelayServerData(allocation, "dtls");

            transport.SetRelayServerData(relayServerData);

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("Relay Client started!");
                return true;
            }

            Debug.LogError("Failed to start Client.");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError("Relay Client error: " + e);
            return false;
        }
    }
}
