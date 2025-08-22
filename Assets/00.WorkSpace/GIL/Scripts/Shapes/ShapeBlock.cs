using UnityEngine;
using UnityEngine.UI;
using _00.WorkSpace.GIL.Scripts.Blocks;
namespace _00.WorkSpace.GIL.Scripts.Shapes
{
    [RequireComponent(typeof(Image))]
    public class ShapeBlock : MonoBehaviour
    {
        public int x, y;
        public Block parentBlock;
    }
}