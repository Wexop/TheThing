using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TheThing.Scripts;

public class ThingRoomManager: MonoBehaviour
{
    public AudioSource ambientSource;

    public AudioClip welcomeSound;
    public AudioClip scaryAmbientSound;
    
    public List<LightInformation> lights = new List<LightInformation>();
    public List<EscapeRoomObject> EscapeRoomObjects = new List<EscapeRoomObject>();

    public ThingEnemyAI ThingEnemyAI;
    
    private LightInformation _playerNightVision;

    private float _lightAnimationsTimer;
    private float _scaryAmbientAnimationTimer = 30f;
    private bool _scarySoundPlayed;

    private int escapeObjectToHit = 3;
    private int escapeObjectHitCount;
    
    public void OnPlayerSpawnIntoRoom()
    {
        ambientSource.PlayOneShot(welcomeSound);
        GetAllLights();

        DisableEveryEscapeObject();
        EnableRandomEscapeObject(0);
        
        SpawnShovel();

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
    }

    private IEnumerator OnScaryAmbientRunned()
    {
        yield return new WaitForSeconds(22f);

        GetAllLights();
        LightsManagement(false, lights);
        if(_playerNightVision?.Light) _playerNightVision.Light.enabled = false;
        DisableEveryEscapeObject();
        
        yield return new WaitForSeconds(3f);
        if(_playerNightVision?.Light) _playerNightVision.Light.enabled = true;
        LightsManagement(true, lights);
        ThingEnemyAI.MonsterAttackPlayer();
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
            if (Vector3.Distance(light.transform.position, transform.position) < 300f) return light;
            return false;
        } );
        
        foreach (var closeObject in closeLights)
        {
            
            if(closeObject.GetComponentInParent<animatedSun>() != null || closeObject.transform.parent.name == "HelmetLights" || closeObject == ThingEnemyAI.redLight) continue;


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

    private void SpawnShovel()
    {
        if(!NetworkManager.Singleton.IsServer) return;
        GameObject shovel = null;
        RoundManager.Instance.currentLevel.spawnableScrap.ToList().ForEach(
            prefab =>
            {
                GrabbableObject grabbableObject = prefab.spawnableItem.spawnPrefab.GetComponent<GrabbableObject>();
                if (grabbableObject != null)
                {
                    if (grabbableObject.itemProperties.itemName == "Shovel")
                    {
                        shovel = prefab.spawnableItem.spawnPrefab;
                    }
                }

            });
        
        var newShovel = Instantiate(shovel, transform);
        newShovel.transform.localPosition = new Vector3(1, 1, 0);
        newShovel.GetComponent<NetworkObject>().Spawn();
        
    }

    public void EnableRandomEscapeObject(int notId)
    {
        var index = Random.Range(0, EscapeRoomObjects.Count);
        while (index == notId)
        {
            index = Random.Range(0, EscapeRoomObjects.Count);
        }
        
        EscapeRoomObjects[index].gameObject.SetActive(true);
        
    }

    public void OnHitEscapeObject(int id)
    {
        escapeObjectHitCount++;
        if (escapeObjectHitCount == escapeObjectToHit)
        {
            StopCoroutine(OnScaryAmbientRunned());
            ThingEnemyAI.CancelMonsterAttack();
        }
        else
        {
            EscapeRoomObjects[id].gameObject.SetActive(false);
            EnableRandomEscapeObject(id);
            
        }
    }
    
}