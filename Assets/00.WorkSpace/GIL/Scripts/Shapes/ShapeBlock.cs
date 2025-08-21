using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ShapeBlock : MonoBehaviour
{
    public int x, y;
    public Block parentBlock;
}