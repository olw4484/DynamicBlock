using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BlockFxFacade : MonoBehaviour, IFx
{
    public static IFx Instance { get; private set; }

    [SerializeField] private ParticleManager particle; // 씬의 ParticleManager drag&drop

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
    public void PlayRowPerimeter(int row, Color color) => particle?.PlayRowPerimeterParticle(row, color);
    public void PlayColPerimeter(int col, Color color) => particle?.PlayColPerimeterParticle(col, color);
    public void PlayAllClear() => particle?.PlayAllClear();
    public void PlayGameOverAt() => particle?.PlayGameOverAt();
    public void PlayNewScoreAt() => particle?.PlayNewScoreAt();
}