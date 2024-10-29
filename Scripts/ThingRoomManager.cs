using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using GameNetcodeStuff;
using Unity.Netcode;

namespace TheThing.Scripts;

public class ThingRoomManager: MonoBehaviour
{
    public AudioSource ambientSource;

    public AudioClip welcomeSound;
    public AudioClip scaryAmbientSound;

    public Vector3 shovelPosition;
    
    public List<LightInformation> lights = new List<LightInformation>();
    public List<EscapeRoomObject> EscapeRoomObjects = new List<EscapeRoomObject>();
    public List<int> escapeRoomNumbersHit = new List<int>();

    public ThingEnemyAI ThingEnemyAI;
    
    private LightInformation _playerNightVision;

    private float _lightAnimationsTimer;
    private float _scaryAmbientAnimationTimer = 90f;
    private bool _scarySoundPlayed;
    private bool _hasEscaped;

    private int escapeObjectToHit = 2;
    private int escapeObjectHitCount;

    public void Start()
    {
        _scaryAmbientAnimationTimer = TheThingPlugin.instance.TimeToEscapeRoom.Value - 22f;
        escapeObjectToHit = TheThingPlugin.instance.monsterToHitToEscapeRoom.Value;
    }

    public void OnPlayerSpawnIntoRoom()
    {
        ambientSource.PlayOneShot(welcomeSound);
        GetAllLights();

        DisableEveryEscapeObject();
        EnableRandomEscapeObject(0);

    }

    public void DisableEveryEscapeObject()
    {
        var i = 0;
        EscapeRoomObjects.ForEach(o =>
        {
            o.id = i;
            i++;
            o.gameObject.SetActive(false);
        });
    }

    public void Update()
    {
        _lightAnimationsTimer-= Time.deltaTime;
        _scaryAmbientAnimationTimer-= Time.deltaTime;
        if(_lightAnimationsTimer < 0 && _scaryAmbientAnimationTimer >= 0 )
        {
            _lightAnimationsTimer = Random.Range(4, 7);
            StartCoroutine(LightAnimation());
        }

        if (_scaryAmbientAnimationTimer < 0 && !_scarySoundPlayed && escapeObjectHitCount < escapeObjectToHit)
        {
            _scarySoundPlayed = true;
            ambientSource.PlayOneShot(scaryAmbientSound);
            StartCoroutine(OnScaryAmbientRunned());
        }
        
        if(ThingEnemyAI && ThingEnemyAI.playerToKillIsLocal && GameNetworkManager.Instance.localPlayerController.isPlayerDead && !_hasEscaped)
        {
            _hasEscaped = true;
            NetworkThing.EscapeRoomServerRpc();
        }
        
    }

    private IEnumerator OnScaryAmbientRunned()
    {
        yield return new WaitForSeconds(22f);

        GetAllLights();
        LightsManagement(false, lights);
        //if(_playerNightVision?.Light) _playerNightVision.Light.enabled = false;
        DisableEveryEscapeObject();
        
        ambientSource.PlayOneShot(welcomeSound);
        if (ThingEnemyAI && ThingEnemyAI.playerToKillIsLocal)
        {
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.8f);
        }
        
        yield return new WaitForSeconds(4f);
        //if(_playerNightVision?.Light) _playerNightVision.Light.enabled = true;
        if(ThingEnemyAI) ThingEnemyAI.MonsterAttackPlayer();
        lights.Clear();
        StopCoroutine(LightAnimation());
        
    }
    private IEnumerator LightAnimation()
    {
        if (!(lights.Count > 0 && lights?[0].Light)) yield return null;
        var lightsAnimated = new List<LightInformation>();
        
        lights.ForEach(l =>
        {
            if (Random.Range(0, 3) == 0)
            {
                lightsAnimated.Add(l);
            }
        });
        
        LightsManagement(false, lightsAnimated);
        
        yield return new WaitForSeconds(0.5f);
        
        LightsManagement(true, lightsAnimated);
        
        yield return new WaitForSeconds(0.5f);
        
        LightsManagement(false, lightsAnimated);
        
        yield return new WaitForSeconds(0.8f);
        
        LightsManagement(true, lightsAnimated);
        
        yield return new WaitForSeconds(0.2f);
        
        LightsManagement(false, lightsAnimated);
        
        yield return new WaitForSeconds(1f);
        
        LightsManagement(true, lightsAnimated);
        
    }
    
    public Color GetRandomRedColor(){
        return new Color(Random.Range(0.3f,0.75f), 0, 0, 1);
    }

    public void LightsManagement(bool active, List<LightInformation> lightsInfos)
    {
        lightsInfos.ForEach(l =>
        {
            if(l.Light != null) l.Light.color = active ? GetRandomRedColor() : Color.black;
        });
    }

    public void GetAllLights()
    {
        lights.Clear();
        
        var closeLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList().FindAll(light =>
        {
            if (Vector3.Distance(light.transform.position, transform.position) < 150f && Vector3.Distance(new Vector3(0,light.transform.position.y, 0), new Vector3(0,transform.position.y,0)) < 25f) return light;
            return false;
        } );
        
        foreach (var closeObject in closeLights)
        {
            
            if(closeObject.GetComponentInParent<animatedSun>() != null || closeObject.transform.parent.name == "HelmetLights" || closeObject == ThingEnemyAI.redLight || closeObject.transform.parent.name.Contains("EscapeObject")) continue;
            if(closeObject.name.Contains("Spot Light") || closeObject.name.Contains("RedLight") || closeObject.name.Contains("RadarCamNightVision") || closeObject.name.Contains("Near") || closeObject.name.Contains("VisorCamera") || closeObject.name.Contains("MainCamera")) continue;

            FlashlightItem flashlightItem = closeObject.GetComponentInParent<FlashlightItem>();
            
            LightInformation lightInformation = new LightInformation();
            lightInformation.Light = closeObject;
            lightInformation.color = closeObject.color;
            lightInformation.intensity = closeObject.intensity;
            lightInformation.flashlightItem = flashlightItem;
            
            if (closeObject.name == "NightVision")
            {
                _playerNightVision = lightInformation;
                continue;
            }
            
            lights.Add(lightInformation);
        }
    }

    public void EnableRandomEscapeObject(int notId)
    {
        if(!ThingEnemyAI.playerToKillIsLocal) return;
        var index = Random.Range(0, EscapeRoomObjects.Count);
        while (index == notId || escapeRoomNumbersHit.Contains(index))
        {
            index = Random.Range(0, EscapeRoomObjects.Count);
        }

        NetworkThing.EnableEscapeObjectServerRpc(index);
        escapeRoomNumbersHit.Add(index);
        
    }

    public void EnableEscapeObject(int id)
    {
        EscapeRoomObjects[id].gameObject.SetActive(true);
    }

    public IEnumerator OnEscaped()
    {
        _hasEscaped = true;
        StopCoroutine(LightAnimation());
        StopCoroutine(OnScaryAmbientRunned());
        LightsManagement(false, lights);
        ambientSource.PlayOneShot(welcomeSound);
        yield return new WaitForSeconds(4f);
        if(ThingEnemyAI) ThingEnemyAI.CancelMonsterAttack();
    }

    public void OnHitEscapeObject(int id)
    {
        escapeObjectHitCount++;
        if (escapeObjectHitCount == escapeObjectToHit)
        {
            NetworkThing.EscapeRoomServerRpc();
        }
        else
        {
            EnableRandomEscapeObject(id);
        }
    }
    
}