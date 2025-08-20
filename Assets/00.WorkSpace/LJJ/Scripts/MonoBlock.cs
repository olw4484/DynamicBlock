using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LJJ
{
    public class MonoBlock : MonoBehaviour
    {
        [SerializeField] Sprite sprite;

        [SerializeField] public Vector2 pos;

        public void Awake()
        {
            sprite = GetComponent<SpriteRenderer>().sprite;
        }

        public void SetSprite()
        {
            // ToDo : 스프라이트 설정
        }

        public void SetPosition(Vector2 newPos)
        {
            pos = newPos;
        }
    }
}

