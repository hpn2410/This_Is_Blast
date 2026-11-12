using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamePlayController_Level1 : MonoBehaviour
{
    [SerializeField] private GameObject gameTutorial;

    [SerializeField] private Button buttonLeft;
    [SerializeField] private Button buttonRight;
    [SerializeField] private GameObject leftShoot;
    [SerializeField] private GameObject rightShoot;
    [SerializeField] private BulletController bulletPrefab;
    [SerializeField] private float shootInterval = 0.75f;
    [SerializeField] private List<GameObject> shootTargetRight;
    [SerializeField] private List<GameObject> shootTargetLeft;

    private bool isShootingLeft;
    private bool isShootingRight;

    private void OnEnable()
    {
        if (buttonLeft != null)
        {
            buttonLeft.onClick.AddListener(OnLeftButtonClicked);
        }

        if (buttonRight != null)
        {
            buttonRight.onClick.AddListener(OnRightButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (buttonLeft != null)
        {
            buttonLeft.onClick.RemoveListener(OnLeftButtonClicked);
        }

        if (buttonRight != null)
        {
            buttonRight.onClick.RemoveListener(OnRightButtonClicked);
        }
    }

    private void OnLeftButtonClicked()
    {
        MoveButtonToShootPosition(buttonLeft, leftShoot);

        if (!isShootingLeft)
        {
            StartCoroutine(ShootTargets(leftShoot, shootTargetLeft, true));
        }

        HideTutorial();
    }

    private void OnRightButtonClicked()
    {
        MoveButtonToShootPosition(buttonRight, rightShoot);

        if (!isShootingRight)
        {
            StartCoroutine(ShootTargets(rightShoot, shootTargetRight, false));
        }

        HideTutorial();
    }

    private void MoveButtonToShootPosition(Button button, GameObject shootPosition)
    {
        if (button == null || shootPosition == null)
        {
            return;
        }

        button.transform.position = shootPosition.transform.position;
    }

    private IEnumerator ShootTargets(GameObject shootPosition, List<GameObject> targets, bool isLeft)
    {
        if (isLeft)
        {
            isShootingLeft = true;
        }
        else
        {
            isShootingRight = true;
        }

        if (shootPosition == null || targets == null || targets.Count == 0)
        {
            SetShootingState(isLeft, false);
            yield break;
        }

        for (int i = targets.Count - 1; i >= 0; i--)
        {
            GameObject target = targets[i];

            if (target == null || !target.activeInHierarchy)
            {
                continue;
            }

            Shoot(shootPosition, target);

            if (i > 0)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, shootInterval));
            }
        }

        SetShootingState(isLeft, false);
    }

    private void Shoot(GameObject shootPosition, GameObject target)
    {
        if (shootPosition == null || target == null)
        {
            return;
        }

        if (bulletPrefab == null)
        {
            Debug.LogError("Bullet prefab has not been assigned to GamePlayController_Level1.");
            return;
        }

        BulletController bullet = Instantiate(
            bulletPrefab,
            shootPosition.transform.position,
            CalculateBulletRotation(shootPosition, target),
            shootPosition.transform);

        bullet.MoveToTarget(target);
    }

    private Quaternion CalculateBulletRotation(GameObject shootPosition, GameObject target)
    {
        Vector3 direction = target.transform.position - shootPosition.transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle);
    }

    private void SetShootingState(bool isLeft, bool isShooting)
    {
        if (isLeft)
        {
            isShootingLeft = isShooting;
        }
        else
        {
            isShootingRight = isShooting;
        }
    }

    private void HideTutorial()
    {
        if (gameTutorial != null)
        {
            gameTutorial.SetActive(false);
        }
    }
}