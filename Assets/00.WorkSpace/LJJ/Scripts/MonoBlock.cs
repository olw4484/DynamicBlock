using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LJJ
{
    public class MonoBlock : MonoBehaviour
    {
        [SerializeField] MeshRenderer meshRenderer;
        [SerializeField] Material material;

        [SerializeField] public Vector2 pos;

        public void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            material = meshRenderer.material;
        }

        public void SetColor(Color color)
        {
            if (meshRenderer == null || material == null)
            {
                Debug.LogError("MeshRenderer or Material is not assigned.");
                return;
            }
            material.color = color;
            meshRenderer.material = material;
        }

        public void SetPosition(Vector2 newPos)
        {
            pos = newPos;
        }
    }
}

