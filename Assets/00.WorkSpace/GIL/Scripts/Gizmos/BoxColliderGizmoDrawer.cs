using UnityEngine;
namespace _00.WorkSpace.GIL.Scripts.Gizmos
{
#if UNITY_EDITOR
    [RequireComponent(typeof(BoxCollider2D))]
    public class BoxColliderGizmoDrawer : MonoBehaviour
    {

        [SerializeField] private Color gizmoColor = Color.green;

        private void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider2D>();
            if (col == null) return;

            UnityEngine.Gizmos.color = gizmoColor;
            UnityEngine.Gizmos.matrix = transform.localToWorldMatrix;
            UnityEngine.Gizmos.DrawWireCube(col.offset, col.size);
        }
    }
#endif
}
