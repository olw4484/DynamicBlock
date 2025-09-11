using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace _00.WorkSpace.GIL.Scripts.Grids
{
    public enum GridState {Normal, Hover, Active}

    public class GridSquare : MonoBehaviour
    {
        [HideInInspector] public int RowIndex;
        [HideInInspector] public int ColIndex;
        
        [Header("Image Objects")] 
        [SerializeField] private Image normalImage;
        [SerializeField] private Image hoverImage;
        [SerializeField] private Image activeImage;
        [SerializeField] private Image lineClearImage;
        [SerializeField] private Image fruitImage;
        
        [HideInInspector] public bool IsOccupied;
        public GridState state;
        
        [SerializeField, Tooltip("0=empty, 1~5=block, 6~10=block+fruit")]
        private int _blockSpriteIndex;
        public int BlockSpriteIndex => _blockSpriteIndex;
        
        // 정규식 해설 : ^s 앞쪽 공백 무시 , (\d+) 연속된 숫자 캡쳐, (?=_) 바로 뒤에 '_'가 와야함
        private static readonly Regex s_CodeRegex =
            new(@"^\s*(\d+)(?=_)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        
        public void SetState(GridState newState)
        {
            state = newState;
            // 가시성은 이거로만 제어하기
            normalImage.gameObject.SetActive(state == GridState.Normal);
            hoverImage.gameObject.SetActive(state == GridState.Hover);
            activeImage.gameObject.SetActive(state == GridState.Active);
        }
        // 모든 이미지 변경 -> active 이미지만 바꾸기
        public void SetImage(Sprite sprite, bool changeIndex = true)
        {
            activeImage.sprite = sprite;
            if(changeIndex) _blockSpriteIndex = ParseCodeFromName(sprite != null ? sprite.name : null);
        }
        
        public void SetPreviewImage(Sprite sprite)
        {
            hoverImage.sprite = sprite;
            // SetState(Hover)에서 가시성이 결정되므로 여기서는 enabled만 맞춰둠
            hoverImage.enabled = (sprite != null);
        }
        
        public void SetLineClearImage(bool isActive, Sprite lineClearSprite = null)
        {
            lineClearImage.gameObject.SetActive(isActive);
            lineClearImage.sprite = lineClearSprite;
        }
        
        public void SetFruitImage(bool isActive, Sprite fruitSprite = null, bool changeIndex = true)
        {
            fruitImage.gameObject.SetActive(isActive);
            fruitImage.sprite = fruitSprite;
            
            if (isActive && changeIndex && fruitSprite != null)
                _blockSpriteIndex = ParseCodeFromName(fruitSprite.name);
        }
        public void SetOccupied(bool occupied)
        {
            IsOccupied = occupied;
            state = occupied ? GridState.Active : GridState.Normal;
            SetState(state);
        }
        
        private static int ParseCodeFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            var m = s_CodeRegex.Match(name);
            return (m.Success && int.TryParse(m.Groups[1].Value, out var code)) ? code : 0;
        }
        public void SetPreviewFruitImage(Sprite fruitSprite) => SetFruitImage(true, fruitSprite, changeIndex: false);
    }
}

