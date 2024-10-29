using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace TheThing.Scripts;

public class EscapeRoomObject : MonoBehaviour, IHittable
{
    private static readonly int Disapear = Animator.StringToHash("disappear");

    public Animator animator;
    public AudioSource AudioSource;
    public AudioClip onHitSound;

    public int id;
    
    public ThingRoomManager thingRoomManager;
    
    private bool alreadyHit = false;
    
    public bool Hit(int force, Vector3 hitDirection, PlayerControllerB playerWhoHit = null, bool playHitSFX = false,
        int hitID = -1)
    {
        
        if(alreadyHit) return true;

        
        animator.SetTrigger(Disapear);
        thingRoomManager.OnHitEscapeObject(id);
        alreadyHit = true;
                
        AudioSource.PlayOneShot(onHitSound);
        return true;
    }
}