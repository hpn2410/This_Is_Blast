using System.Collections;
using System.Collections.Generic;
using TMPro;
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
    [SerializeField] private float shootInterval = 0.25f;
    [SerializeField] private float bulletRotationOffset = -90f;
    [SerializeField] private List<GameObject> shootTargetRight;
    [SerializeField] private List<GameObject> shootTargetLeft;
    [SerializeField] private Slider levelBar;
    [SerializeField] private TextMeshProUGUI buttonLeftText;
    [SerializeField] private TextMeshProUGUI buttonRightText;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Button nextLevelBtn;

    private bool isShootingLeft;
    private bool isShootingRight;
    private bool gameplayFinished;
    private int remainingLeftShots;
    private int remainingRightShots;
    private int totalTargetCount;
    private GameManager gameManager;

    private void Start()
    {
        gameManager = GetComponentInParent<GameManager>();
        remainingLeftShots = GetTargetCount(shootTargetLeft);
        remainingRightShots = GetTargetCount(shootTargetRight);
        totalTargetCount = remainingLeftShots + remainingRightShots;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        UpdateButtonTexts();
        UpdateLevelBar();
    }

    private void Update()
    {
        UpdateLevelBar();
        CheckGameplayFinished();
    }

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

        if (nextLevelBtn != null)
        {
            nextLevelBtn.onClick.AddListener(OnNextLevelButtonClicked);
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

        if (nextLevelBtn != null)
        {
            nextLevelBtn.onClick.RemoveListener(OnNextLevelButtonClicked);
        }
    }

    private void OnLeftButtonClicked()
    {
        MoveButtonToShootPosition(buttonLeft, leftShoot);

        if (!isShootingLeft)
        {
            StartCoroutine(ShootTargets(buttonLeft, leftShoot, shootTargetLeft, true));
        }

        HideTutorial();
    }

    private void OnRightButtonClicked()
    {
        MoveButtonToShootPosition(buttonRight, rightShoot);

        if (!isShootingRight)
        {
            StartCoroutine(ShootTargets(buttonRight, rightShoot, shootTargetRight, false));
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

    private IEnumerator ShootTargets(Button shooter, GameObject shootPosition, List<GameObject> targets, bool isLeft)
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

            if (Shoot(shooter, shootPosition, target))
            {
                DecreaseShotCount(isLeft);
            }

            if (i > 0)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, shootInterval));
            }
        }

        SetShootingState(isLeft, false);

        if (shooter != null)
        {
            shooter.gameObject.SetActive(false);
        }
    }

    private bool Shoot(Button shooter, GameObject shootPosition, GameObject target)
    {
        if (shooter == null || shootPosition == null || target == null)
        {
            return false;
        }

        if (bulletPrefab == null)
        {
            Debug.LogError("Bullet prefab has not been assigned to GamePlayController_Level1.");
            return false;
        }

        Quaternion rotation = CalculateBulletRotation(shooter.transform.position, target);
        shooter.transform.rotation = rotation;

        BulletController bullet = Instantiate(
            bulletPrefab,
            shootPosition.transform.position,
            rotation,
            shootPosition.transform);

        bullet.MoveToTarget(target);
        return true;
    }

    private int GetTargetCount(List<GameObject> targets)
    {
        return targets == null ? 0 : targets.Count;
    }

    private void DecreaseShotCount(bool isLeft)
    {
        if (isLeft)
        {
            remainingLeftShots = Mathf.Max(0, remainingLeftShots - 1);
        }
        else
        {
            remainingRightShots = Mathf.Max(0, remainingRightShots - 1);
        }

        UpdateButtonTexts();
    }

    private void UpdateButtonTexts()
    {
        if (buttonLeftText != null)
        {
            buttonLeftText.text = remainingLeftShots.ToString();
        }

        if (buttonRightText != null)
        {
            buttonRightText.text = remainingRightShots.ToString();
        }
    }

    private void UpdateLevelBar()
    {
        if (levelBar == null || totalTargetCount <= 0)
        {
            return;
        }

        int remainingTargets = CountActiveTargets(shootTargetLeft) + CountActiveTargets(shootTargetRight);
        float progress = 1f - remainingTargets / (float)totalTargetCount;

        levelBar.minValue = 0f;
        levelBar.maxValue = 1f;
        levelBar.value = Mathf.Clamp01(progress);
    }

    private void CheckGameplayFinished()
    {
        if (gameplayFinished || totalTargetCount <= 0)
        {
            return;
        }

        int remainingTargets = CountActiveTargets(shootTargetLeft) + CountActiveTargets(shootTargetRight);

        if (remainingTargets == 0)
        {
            gameplayFinished = true;

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
            }
        }
    }

    private void OnNextLevelButtonClicked()
    {
        if (gameManager == null)
        {
            gameManager = GetComponentInParent<GameManager>();
        }

        if (gameManager != null)
        {
            gameManager.CompleteCurrentLevel();
        }
        else
        {
            Debug.LogError("GameManager could not be found in the parent hierarchy.");
        }
    }

    private int CountActiveTargets(List<GameObject> targets)
    {
        if (targets == null)
        {
            return 0;
        }

        int activeTargetCount = 0;

        foreach (GameObject target in targets)
        {
            if (target != null && target.activeInHierarchy)
            {
                activeTargetCount++;
            }
        }

        return activeTargetCount;
    }

    private Quaternion CalculateBulletRotation(Vector3 shooterPosition, GameObject target)
    {
        Vector2 direction = target.transform.position - shooterPosition;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        angle += bulletRotationOffset;
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