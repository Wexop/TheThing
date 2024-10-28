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

    public ThingEnemyAI ThingEnemyAI;
    
    private LightInformation _playerNightVision;

    private float _lightAnimationsTimer;
    //private float _scaryAmbientAnimationTimer = 20f;
    private float _scaryAmbientAnimationTimer = 2f;
    private bool _scarySoundPlayed;
    
    public void OnPlayerSpawnIntoRoom()
    {
        lights.Clear();
        GetAllLights();
        ambientSource.PlayOneShot(welcomeSound);
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

        if (_scaryAmbientAnimationTimer < 0 && !_scarySoundPlayed)
        {
            _scarySoundPlayed = true;
            ambientSource.PlayOneShot(scaryAmbientSound);
            StartCoroutine(OnScaryAmbientRunned());
        }
    }

    private IEnumerator OnScaryAmbientRunned()
    {
        yield return new WaitForSeconds(22f);
        
        LightsManagement(false, lights);
        if(_playerNightVision?.Light) _playerNightVision.Light.enabled = false;
        
        yield return new WaitForSeconds(3f);
        LightsManagement(true, lights);
        if(_playerNightVision?.Light) _playerNightVision.Light.enabled = true;
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
            
            if(closeObject.GetComponentInParent<animatedSun>() != null || closeObject.transform.parent.name == "HelmetLights") continue;


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


    
}