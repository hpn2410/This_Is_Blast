using UnityEngine;

public class BulletController : MonoBehaviour
{
    private float speed = 2000f;

    private GameObject target;
    private bool promoteChildrenOnHit;

    public void MoveToTarget(GameObject targetToMoveTo)
    {
        MoveToTarget(targetToMoveTo, false);
    }

    public void MoveToTarget(GameObject targetToMoveTo, bool promoteChildrenOnTargetHit)
    {
        target = targetToMoveTo;
        promoteChildrenOnHit = promoteChildrenOnTargetHit;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.transform.position,
            speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, target.transform.position) <= 0.01f)
        {
            MultiHitTargetController multiHitTarget = target.GetComponent<MultiHitTargetController>();

            if (multiHitTarget != null)
            {
                multiHitTarget.RegisterHit(promoteChildrenOnHit);
            }
            else
            {
                if (promoteChildrenOnHit)
                {
                    PromoteChildren(target);
                }

                target.SetActive(false);
            }

            Destroy(gameObject);
        }
    }

    private void PromoteChildren(GameObject targetObject)
    {
        Transform targetTransform = targetObject.transform;
        Transform parent = targetTransform.parent;

        if (parent == null)
        {
            return;
        }

        int siblingIndex = targetTransform.GetSiblingIndex();

        for (int i = targetTransform.childCount - 1; i >= 0; i--)
        {
            Transform child = targetTransform.GetChild(i);
            child.SetParent(parent, true);
            child.SetSiblingIndex(siblingIndex);
            child.gameObject.SetActive(true);
        }
    }
}
