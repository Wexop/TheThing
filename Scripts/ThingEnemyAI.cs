﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using Object = System.Object;
using Random = UnityEngine.Random;

namespace TheThing.Scripts;

public class ThingEnemyAI: EnemyAI
{
    private static readonly int Idle = Animator.StringToHash("idle");
    private static readonly int Jumpscare = Animator.StringToHash("jumpscare");

    public GameObject headObject;
    public GameObject modelObject;
    public Light redLight;

    public AudioClip seePlayerSound;
    public AudioClip scaryAmbiantSound;
    public AudioClip JumpscareSound;
    
    public float aiInterval;
    public int lastBehaviorState;

    public ulong lastPlayerIdToKill;

    private PlayerControllerB playerToKIll = null;
    
    List<LightInformation> lights = new List<LightInformation>();

    public bool playerToKillIsLocal;
    private Vector3 positionJumpScare;
    
    private bool _isActive;
    private bool _shouldResetLights;
    private bool _shouldTpToPlayer;
    private int _sawPlayerCount;

    private float _lightAnimationDuration = 3f;
    private float _lightAnimationTimer;
    private int _lastNodeIndex;
    private float _timeBetweenTeleport = 10f;
    private float _teleportTimer;
    private float _notSeePlayerTimer;
    private float _changePositionSeePlayerDuration = 45f;
    
    
    public override void Start()
    {

        base.Start();
        AllClientOnSwitchBehaviorState();
        ActivateMonster(false);
        agent.speed = 0;
        debugEnemyAI = true;
        _timeBetweenTeleport = TheThingPlugin.instance.timeBetweenTeleport.Value;

    }

    public override void EnableEnemyMesh(bool enable, bool overrideDoNotSet = false)
    {
        return;
    }

    public override void OnSyncPositionFromServer(Vector3 pos)
    {
        transform.position = pos;
    }

    public override void Update()
    {
        
        //base.Update(); 
        aiInterval -= Time.deltaTime;
        _lightAnimationTimer -= Time.deltaTime;
        _teleportTimer -= Time.deltaTime;
        if(currentBehaviourStateIndex == 1 )
        {
            _notSeePlayerTimer -= Time.deltaTime;
        }
        
        if (lastBehaviorState != currentBehaviourStateIndex)
        {
            lastBehaviorState = currentBehaviourStateIndex;
            AllClientOnSwitchBehaviorState();

        }
        
        if (currentBehaviourStateIndex == 2 && GameNetworkManager.Instance.localPlayerController.HasLineOfSightToPosition(transform.position + Vector3.up * 0.25f, 100f, 60))
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.8f);
        }
        
        if (_lightAnimationTimer < -0.5f && _shouldResetLights)
        {
            ResetLights();
            _shouldResetLights = false;
        }

        
        if (aiInterval <= 0 && IsOwner)
        {
            aiInterval = AIIntervalTime;
            DoAIInterval();
        }
        
    }

    private void LateUpdate()
    {
      
        if (currentBehaviourStateIndex == 2)
        {

            if (_lightAnimationTimer > -0.5f)
            {
                lights.ForEach(l =>
                {
                    var instensity = (Math.Clamp(_lightAnimationTimer, 0f, _lightAnimationDuration) / _lightAnimationDuration);
                    if (instensity < 0.2f) instensity = 0f;
                    l.Light.intensity = (instensity * l.intensity);
                    if (l.flashlightItem)
                    {
                        l.flashlightItem.flashlightBulb.color =
                            l.color * instensity;
                        l.flashlightItem.flashlightBulbGlow.color =
                            l.color * instensity;
                    }
                });
            }
        }

        if (currentBehaviourStateIndex == 1 && !targetPlayer)
        {
            var closestPlayer = GetClosestPlayer();
            if(closestPlayer)
            {
                transform.LookAt(closestPlayer.gameplayCamera.transform);
                transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            }
        }

        if (currentBehaviourStateIndex == 4 && targetPlayer)
        {

            if (playerToKillIsLocal)
            {
                var player = GameNetworkManager.Instance.localPlayerController;
                player.gameplayCamera.transform.eulerAngles = new Vector3(0, player.gameplayCamera.transform.eulerAngles.y, player.gameplayCamera.transform.eulerAngles.z);
            }
            positionJumpScare = targetPlayer.gameplayCamera.transform.position + targetPlayer.gameplayCamera.transform.forward * 1.4f;
            transform.position = positionJumpScare - Vector3.up * 2.5f;
            transform.LookAt(targetPlayer.gameplayCamera.transform);
            transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
        }
        
    }


    public override void DoAIInterval()
    {
        SyncPositionToClients();

        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                if (!_isActive && _teleportTimer < 0)
                {
                    var pos = GetRandomNodeObjectPos();
                    if (CheckIfPlayerAreInRange(pos))
                    {
                        _notSeePlayerTimer = _changePositionSeePlayerDuration;
                        transform.position = pos;
                        SyncPositionToClients();
                        SwitchToBehaviourState(1);
                    }
                }
                
                break;
            }
            case 1:
            {
                TargetClosestPlayer(requireLineOfSight: true);
                if (_notSeePlayerTimer < 0)
                {
                    SwitchToBehaviourState(0);
                }
                if(targetPlayer == null) break;
                if (PlayerIsTargetable(targetPlayer))
                {

                    NetworkThing.SetPlayerIdServerRpc(NetworkObjectId, targetPlayer.actualClientId);
                    _sawPlayerCount++;
                    _teleportTimer = _timeBetweenTeleport;
                    SwitchToBehaviourState(2);
                }
                break;
            }
            case 2:
            {
                if (_lightAnimationTimer <= 0.5f)
                {
                     SwitchToBehaviourState(_sawPlayerCount >= TheThingPlugin.instance.maxSeePlayerCount.Value ? 3: 0);
                }
                break;
            }
            case 3:
            {
                break;
            }
            case 4:
            {

                break;
            }

            default: break;
                
        }
    }

    public void AllClientOnSwitchBehaviorState()
    {
        creatureAnimator.SetBool(Jumpscare, false);
        redLight.enabled = false;
        agent.enabled = currentBehaviourStateIndex != 4;
        
        switch (currentBehaviourStateIndex)
        {
            case 0:
            {
                targetPlayer = null;
                ActivateMonster(false);
                _shouldTpToPlayer = false;
                break;
            }
            case 1:
            {
                ActivateMonster(true);
                creatureAnimator.SetBool(Idle, true);
                break;
            }
            case 2:
            {
                creatureSFX.PlayOneShot(seePlayerSound);
                _lightAnimationTimer = _lightAnimationDuration;
                _shouldResetLights = true;
                GetLightsClose();
                break;
            }
            case 3:
            {
                ActivateMonster(false);
                TheThingPlugin.instance.InstantiateRoom();
                TheThingPlugin.instance.actualRoomObjectManager.ThingEnemyAI = this;
                SpawnShovel();
                if (targetPlayer)
                {
                    StartCoroutine(DropItemsAndTeleportPlayer());
                }
                TheThingPlugin.instance.actualRoomObjectManager.OnPlayerSpawnIntoRoom();

                break;
            }
            case 4:
            {
                _sawPlayerCount = 0;
                ActivateMonster(true);
                redLight.enabled = true;
                StartCoroutine(JumpScareAnimation());
                creatureAnimator.SetBool(Jumpscare, true);
                creatureSFX.PlayOneShot(JumpscareSound);
                positionJumpScare = targetPlayer.gameplayCamera.transform.position + targetPlayer.gameplayCamera.transform.forward * 1.4f;
                if (playerToKillIsLocal)
                {
                    var player = GameNetworkManager.Instance.localPlayerController;
                    player.JumpToFearLevel(0.9f);
                    player.disableMoveInput = true;
                    player.disableLookInput = true;
                    player.disableInteract = true;

                }
                break;
            }
        }
    }

    public void OnSetPlayerId(ulong playerId)
    {
        lastPlayerIdToKill = playerId;
        targetPlayer = StartOfRound.Instance.allPlayerScripts.ToList().Find(p => p.actualClientId == playerId);
    }

    private IEnumerator DropItemsAndTeleportPlayer()
    {
        if(targetPlayer.playerClientId == GameNetworkManager.Instance.localPlayerController.playerClientId) playerToKillIsLocal = true;
        if(playerToKillIsLocal) GameNetworkManager.Instance.localPlayerController.DropAllHeldItemsAndSync();
        
        yield return new WaitForSeconds(0.3f);
        
        targetPlayer.transform.position = TheThingPlugin.instance.actualRoomObjectInstantiated.transform.position + new Vector3(1,1,0);
        if (playerToKillIsLocal)
        {
            var player = GameNetworkManager.Instance.localPlayerController;
                        
            player.transform.position = TheThingPlugin.instance.actualRoomObjectInstantiated.transform.position + new Vector3(1,1,0);
                        
        }
    }

    private IEnumerator JumpScareAnimation()
    {
        yield return new WaitForSeconds(2f);
        
        if(playerToKillIsLocal)
        {
            GameNetworkManager.Instance.localPlayerController.KillPlayer(Vector3.zero, causeOfDeath: CauseOfDeath.Crushing, deathAnimation: 0);
            CancelPlayerEffects();
        }
        
        targetPlayer = null;
        playerToKillIsLocal = false;
        playerToKIll = null;
        _teleportTimer = _timeBetweenTeleport;
        
        if (IsOwner)
        {
            var pos = GetRandomNodeObjectPos();
            transform.position = pos;
            SwitchToBehaviourState(0);
        }

        StartCoroutine(DestroyRoom());
    }

    private IEnumerator DestroyRoom()
    {
        yield return new WaitForSeconds(2f);

        TheThingPlugin.instance.DestroyRoom();

    }

    public void MonsterAttackPlayer()
    {
        Debug.Log("MONSTER ATTACK");
        if ( targetPlayer && targetPlayer.isPlayerDead)
        {
            SwitchToBehaviourState(0);
        }
        _shouldTpToPlayer = true;
        SwitchToBehaviourServerRpc(4);
    }

    public void CancelMonsterAttack()
    {
        _sawPlayerCount = 0;
        if (playerToKillIsLocal)
        {
            var player = GameNetworkManager.Instance.localPlayerController;
            player.transform.position = StartOfRound.Instance.insideShipPositions[0].position;
        }
        targetPlayer = null;
        playerToKillIsLocal = false;
        playerToKIll = null;
        _teleportTimer = _timeBetweenTeleport;
        
        if (IsOwner)
        {
            var pos = GetRandomNodeObjectPos();
            transform.position = pos;
            SwitchToBehaviourState(0);
        }

        try
        {
            StartCoroutine(DestroyRoom());
            StopCoroutine(JumpScareAnimation());
        }
        catch
        {
            // ignored
        }
    }
    
    
    private void CancelPlayerEffects()
    {

        if (playerToKillIsLocal)
        {
            var player =  GameNetworkManager.Instance.localPlayerController;
            
            player.disableMoveInput = false;
            player.disableLookInput = false;
            player.disableInteract = false;
        }

    }

    private void ActivateMonster(bool active)
    {
        _isActive = active;
        modelObject.SetActive(active);
    }
    
    private Vector3 GetRandomNodeObjectPos()
    {
        if (allAINodes.Length > 0)
        {
            var index = Random.Range(0, allAINodes.Length);
            while (index == _lastNodeIndex)
            {
                index = Random.Range(0, allAINodes.Length);
            }

            lastBehaviorState = index;
            return allAINodes[index].transform.position;
        }
        return transform.position;
    }

    private bool CheckIfPlayerAreInRange(Vector3 position)
    {
        var result = true;
        
        StartOfRound.Instance.allPlayerScripts.ToList().ForEach(p =>
        {
            if(Vector3.Distance(p.gameplayCamera.transform.position, position) < TheThingPlugin.instance.minDistanceBetweenPlayerToTeleport.Value) result = false;
        });
        
        return result;
    }

    private void ResetLights()
    {

        foreach (var l in lights)
        {
            if(l.Light) l.Light.intensity = l.intensity;
            if (l.flashlightItem) l.Light.color = l.color;
        }
    }

    private void GetLightsClose()
    {
        
        lights.Clear();
        
        var closeLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList().FindAll(light =>
        {
            if (Vector3.Distance(light.transform.position, transform.position) < 30f) return light;
            return false;
        } );
        
        
        
        foreach (var closeObject in closeLights)
        {
            
            if(closeObject.GetComponentInParent<animatedSun>() != null || closeObject.name == "NightVision" || closeObject.name.Contains("Spot Light") || closeObject.name.Contains("RedLight") || closeObject.name.Contains("RadarCamNightVision")) continue;

            FlashlightItem flashlightItem = closeObject.GetComponentInParent<FlashlightItem>();
            
            LightInformation lightInformation = new LightInformation();
            lightInformation.Light = closeObject;
            lightInformation.color = closeObject.color;
            lightInformation.intensity = closeObject.intensity;
            lightInformation.flashlightItem = flashlightItem;
            lights.Add(lightInformation);
        }
    }
    
    public void SpawnShovel()
    {
        if(!IsServer) return;
        GameObject shovel = null;
        NetworkManager.NetworkConfig.Prefabs.NetworkPrefabsLists?.ForEach(
            list => list.PrefabList?.ToList().ForEach(
                prefab =>
                {
                    GrabbableObject grabbableObject = prefab.Prefab.GetComponent<GrabbableObject>();
                    if (grabbableObject != null)
                    {
                        if (grabbableObject.itemProperties.itemName == "Shovel")
                        {
                            shovel = prefab.Prefab;
                        }
                    } 

                }) 
        );
        
        var newShovel = Instantiate(shovel, TheThingPlugin.instance.actualRoomObjectInstantiated.transform);
        newShovel.transform.localPosition = TheThingPlugin.instance.actualRoomObjectManager.shovelPosition;
        newShovel.GetComponent<NetworkObject>().Spawn();
        
    }

    public override void OnCollideWithPlayer(Collider other)
    {
        PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
        if (player != null && player.actualClientId == GameNetworkManager.Instance.localPlayerController.actualClientId)
        {
            targetPlayer = GameNetworkManager.Instance.localPlayerController;
            playerToKillIsLocal = true;
            NetworkThing.SetPlayerIdServerRpc(NetworkObjectId, targetPlayer.actualClientId);
            MonsterAttackPlayer();
        }
    }
}