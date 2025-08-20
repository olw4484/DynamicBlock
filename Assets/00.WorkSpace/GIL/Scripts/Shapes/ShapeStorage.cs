using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShapeStorage : MonoBehaviour
{
    public List<ShapeData> shapeDatas;
    public List<Shape> shapeList;
    
    private void Start()
    {
        foreach (var shape in shapeList)
        {
            var shapeIndex = UnityEngine.Random.Range(0, shapeDatas.Count);
            shape.CreateShape(shapeDatas[shapeIndex]);
        }
    }

    public Shape GetCurrentSelectedShape()
    {
        foreach (var shape in shapeList)
        {
            if (shape.IsOnStartPosition() == false && shape.IsAnyOfShapeSquareActive())
                return shape;
            
        }
        
        Debug.LogError("No shape selected");
        return null;
    }
}
