using UnityEngine;

public class MultiHitTargetController : MonoBehaviour
{
    [SerializeField] private int hitsToDestroy = 30;

    private int currentHits;

    public void RegisterHit(bool promoteChildrenOnDestroy)
    {
        currentHits++;

        if (currentHits < hitsToDestroy)
        {
            return;
        }

        // A multi-hit target and its children are one target. Destroying the
        // root GameObject also removes its child hierarchy.
        Destroy(gameObject);
    }
}