using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using _00.WorkSpace.GIL.Scripts.Blocks;

public class BlockFxFacade : MonoBehaviour, IFx
{
    public static IFx Instance { get; private set; }

    [SerializeField] private ParticleManager particle; // 씬의 ParticleManager drag&drop
    [SerializeField] private BlockStorage blockStorage; // 씬의 BlockStorage drag&drop

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 필요시
    }

    public void PlayRow(int row, Color color) => particle?.PlayRowParticle(row, color);
    public void PlayCol(int col, Color color) => particle?.PlayColParticle(col, color);
    public void PlayRowPerimeter(int row, Sprite sprite) => particle?.PlayRowPerimeterParticle(row, SpriteToColor(sprite));
    public void PlayColPerimeter(int col, Sprite sprite) => particle?.PlayColPerimeterParticle(col, SpriteToColor(sprite));
    public void StopAllLoop() => particle?.StopAllLoopCommon();
    public void PlayAllClear() => particle?.PlayAllClear();
    public void PlayGameOverAt() => particle?.PlayGameOverAt();
    public void PlayNewScoreAt() => particle?.PlayNewScoreAt();


    private Color SpriteToColor(Sprite sprite)
    {
        if (sprite == blockStorage.shapeImageSprites[0]) return Color.red;
        if (sprite == blockStorage.shapeImageSprites[1]) return Color.yellow;
        if (sprite == blockStorage.shapeImageSprites[2]) return Color.white;
        if (sprite == blockStorage.shapeImageSprites[3]) return Color.green;
        if (sprite == blockStorage.shapeImageSprites[4]) return Color.blue;
        return Color.white;
    }
}