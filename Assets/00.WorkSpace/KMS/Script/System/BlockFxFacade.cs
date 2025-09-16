using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using _00.WorkSpace.GIL.Scripts.Blocks;

public class BlockFxFacade : MonoBehaviour, IFx
{
    public static IFx Instance { get; private set; }

    [SerializeField] private ParticleManager particles; // 씬의 ParticleManager drag&drop
    [SerializeField] private BlockStorage blockStorage; // 씬의 BlockStorage drag&drop
    [SerializeField] private List<Sprite> comboSprites; // 콤보 이펙트용 스프라이트들 

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!particles) particles = FindAnyObjectByType<ParticleManager>();
        Debug.Log($"[FX] Facade.Awake particles={(particles ? particles.name : "null")}");
    }

    public void PlayRow(int row, Color color) => particles?.PlayRowParticle(row, color);
    public void PlayCol(int col, Color color) => particles?.PlayColParticle(col, color);
    public void PlayComboRow(int row, Sprite sprite) => particles?.PlayComboRowParticle(row, sprite); //
    public void PlayComboCol(int col, Sprite sprite) => particles?.PlayComboColParticle(col, sprite); //
    public void PlayRowPerimeter(int row, Sprite sprite) => particles?.PlayRowPerimeterParticle(row, SpriteToColor(sprite));
    public void PlayColPerimeter(int col, Sprite sprite) => particles?.PlayColPerimeterParticle(col, SpriteToColor(sprite));
    public void StopAllLoop() => particles?.StopAllLoopCommon();
    public void PlayAllClear()
    {
        Debug.Log("[FX] Facade.PlayAllClear");
        particles?.PlayAllClear();
    }
    public void PlayAllClearAtWorld(Vector3 world)
    {
        Debug.Log($"[FX] Facade.PlayAllClearAtWorld {world}");
        particles?.PlayAllClearAtWorld(world);
    }
    public void PlayGameOverAt() => particles?.PlayGameOverAt();
    public void PlayNewScoreAt() => particles?.PlayNewScoreAt();

    public void PlayRowPerimeter(int row, Color color)
    => particles?.PlayRowPerimeterParticle(row, color);

    public void PlayColPerimeter(int col, Color color)
    => particles?.PlayColPerimeterParticle(col, color);


    private Color SpriteToColor(Sprite sprite)
    {
        if (sprite == blockStorage.shapeImageSprites[0]) return Color.red;
        if (sprite == blockStorage.shapeImageSprites[1]) return Color.yellow;
        if (sprite == blockStorage.shapeImageSprites[2]) return Color.white;
        if (sprite == blockStorage.shapeImageSprites[3]) return Color.green;
        if (sprite == blockStorage.shapeImageSprites[4]) return Color.blue;
        return Color.white;
    }

    private Sprite ComboSprite(Sprite sprite)
    {
        if (sprite == blockStorage.shapeImageSprites[0]) return comboSprites[0];
        if (sprite == blockStorage.shapeImageSprites[1]) return comboSprites[1];
        if (sprite == blockStorage.shapeImageSprites[2]) return comboSprites[2];
        if (sprite == blockStorage.shapeImageSprites[3]) return comboSprites[3];
        if (sprite == blockStorage.shapeImageSprites[4]) return comboSprites[4];
        return null;

    }
}