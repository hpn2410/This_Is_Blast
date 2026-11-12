using UnityEngine;

public class BulletController : MonoBehaviour
{
    private float speed = 100f;

    private GameObject target;

    public void MoveToTarget(GameObject targetToMoveTo)
    {
        target = targetToMoveTo;
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
            Destroy(target);
            Destroy(gameObject);
        }
    }
}
