using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace _00.WorkSpace.GIL.Scripts.Shapes
{
    public class ShapeSquare : MonoBehaviour
    {
        public Image fruitImage;

        private void OnValidate()
        {
            if(fruitImage.gameObject.activeSelf)
                fruitImage.gameObject.SetActive(false);
        }

        public void SetFruitImage(Sprite sprite)
        {
            if (fruitImage == null) return;

            if (sprite == null)
            {
                // 스프라이트 제거 + 렌더만 끄기
                fruitImage.sprite = null;
                fruitImage.gameObject.SetActive(false);
                fruitImage.enabled = false;
                return;
            }

            // 스프라이트 지정, 활성화 
            fruitImage.sprite = sprite;
            fruitImage.gameObject.SetActive(true);
            fruitImage.enabled = true;
        }

        // 과일 여부 확인하기
        public bool HasFruit => fruitImage != null && fruitImage.gameObject.activeInHierarchy &&
                                fruitImage.enabled && fruitImage.sprite != null;
        // 과일 스프라이트 가져오기
        public Sprite FruitSprite => fruitImage ? fruitImage.sprite : null;
    }
}
