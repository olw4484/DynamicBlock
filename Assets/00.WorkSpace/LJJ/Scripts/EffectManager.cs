using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LJJ;

//public class EffectManager : MonoBehaviour
//{
//    [Header("Particle")]
//    public ParticleManager particleManager;
//    
//
//
//    private void OnEnable()
//    {
//        GameEvents.OnBlockDestroyed += HandleBlockDestroyed;
//    }
//
//    private void OnDisable()
//    {
//        GameEvents.OnBlockDestroyed -= HandleBlockDestroyed;
//    }
//
//    private void HandleBlockDestroyed(int index, Color color, bool isRow)
//    {
//        Debug.Log($"isRow: {isRow}, Index: {index}");
//        if (isRow)
//        {
//            particleManager.PlayRowParticle(index, color);
//        }
//        else
//        {
//            particleManager.PlayColParticle(index, color);
//        }
//    }
//
//    
//    
//}
