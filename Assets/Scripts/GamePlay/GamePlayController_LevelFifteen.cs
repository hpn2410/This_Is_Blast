using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GamePlayController_LevelFifteen : MonoBehaviour
{
    [SerializeField] private List<GameObject> shootPositions;
    [SerializeField] private List<Transform> buttonContainers;
    [SerializeField] private List<GameObject> redShootBtns;
    [SerializeField] private List<GameObject> blueShootBtns;
    [SerializeField] private List<GameObject> greenShootBtns;
    [SerializeField] private List<GameObject> yellowShootBtns;
    [SerializeField] private List<TextMeshProUGUI> redShootBtnTexts;
    [SerializeField] private List<TextMeshProUGUI> blueShootBtnTexts;
    [SerializeField] private List<TextMeshProUGUI> greenShootBtnTexts;
    [SerializeField] private List<TextMeshProUGUI> yellowShootBtnTexts;
    [SerializeField] private List<GameObject> redTargets;
    [SerializeField] private List<GameObject> blueTargets;
    [SerializeField] private List<GameObject> greenTargets;
    [SerializeField] private List<GameObject> yellowTargets;
    [SerializeField] private List<GameObject> multiHitTargets;
    [SerializeField] private List<Transform> targetColumns;
    [SerializeField] private BulletController bulletPrefab;
    [SerializeField] private float shootInterval = 0.25f;
    [SerializeField] private float waitForTargetInterval = 0.1f;
    [SerializeField] private int maxShotsPerButton = 20;
    [SerializeField] private float bulletRotationOffset = -90f;
    [SerializeField] private Slider levelBar;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private Button nextLevelBtn;
    [SerializeField] private GameObject blurPanel;
    [SerializeField] private Button gotItBtn;

    private readonly Dictionary<GameObject, UnityAction> buttonListeners = new Dictionary<GameObject, UnityAction>();
    private readonly HashSet<GameObject> shootingButtons = new HashSet<GameObject>();
    private readonly HashSet<GameObject> waitingButtons = new HashSet<GameObject>();
    private readonly HashSet<GameObject> reservedTargets = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, int> remainingShotsPerButton = new Dictionary<GameObject, int>();
    private readonly Dictionary<GameObject, TextMeshProUGUI> buttonTexts = new Dictionary<GameObject, TextMeshProUGUI>();
    private readonly Dictionary<GameObject, TargetColor> buttonColors = new Dictionary<GameObject, TargetColor>();
    private readonly Dictionary<GameObject, Transform> buttonQueueParents = new Dictionary<GameObject, Transform>();
    private GameObject[] buttonsInSlots;
    private GameManager gameManager;
    private int totalTargetCount;
    private bool gameplayFinished;
    private bool gameplayStarted;

    private enum TargetColor
    {
        Red,
        Blue,
        Green,
        Yellow
    }

    private void Start()
    {
        gameManager = GetComponentInParent<GameManager>();
        buttonsInSlots = new GameObject[shootPositions == null ? 0 : shootPositions.Count];
        InitializeButtonQueues();
        totalTargetCount = GetTargetCount(redTargets)
            + GetTargetCount(blueTargets)
            + GetTargetCount(greenTargets)
            + GetTargetCount(multiHitTargets)
            + GetTargetCount(yellowTargets);

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }

        if (blurPanel != null)
        {
            blurPanel.SetActive(true);
        }

        UpdateAllButtonTexts();
        UpdateLevelBar();
    }

    private void Update()
    {
        UpdateLevelBar();
        CheckGameplayFinished();
    }

    private void OnEnable()
    {
        AddButtonListeners(redShootBtns, redShootBtnTexts, TargetColor.Red);
        AddButtonListeners(blueShootBtns, blueShootBtnTexts, TargetColor.Blue);
        AddButtonListeners(greenShootBtns, greenShootBtnTexts, TargetColor.Green);
        AddButtonListeners(yellowShootBtns, yellowShootBtnTexts, TargetColor.Yellow);

        if (nextLevelBtn != null)
        {
            nextLevelBtn.onClick.AddListener(OnNextLevelButtonClicked);
        }

        if (gotItBtn != null)
        {
            gotItBtn.onClick.AddListener(OnGotItButtonClicked);
        }

    }

    private void OnDisable()
    {
        foreach (KeyValuePair<GameObject, UnityAction> listener in buttonListeners)
        {
            Button button = listener.Key == null ? null : listener.Key.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.RemoveListener(listener.Value);
            }
        }

        buttonListeners.Clear();

        if (nextLevelBtn != null)
        {
            nextLevelBtn.onClick.RemoveListener(OnNextLevelButtonClicked);
        }

        if (gotItBtn != null)
        {
            gotItBtn.onClick.RemoveListener(OnGotItButtonClicked);
        }

    }

    private void AddButtonListeners(
        List<GameObject> buttons,
        List<TextMeshProUGUI> texts,
        TargetColor color)
    {
        if (buttons == null)
        {
            return;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            GameObject buttonObject = buttons[i];

            if (buttonObject == null || buttonListeners.ContainsKey(buttonObject))
            {
                continue;
            }

            Button button = buttonObject.GetComponent<Button>();

            if (button == null)
            {
                Debug.LogError($"{buttonObject.name} must have a Button component.");
                continue;
            }

            GameObject capturedButton = buttonObject;
            TargetColor capturedColor = color;
            UnityAction listener = () => OnShootButtonClicked(capturedButton, capturedColor);

            buttonColors[buttonObject] = color;
            remainingShotsPerButton[buttonObject] = maxShotsPerButton;

            if (texts != null && i < texts.Count)
            {
                buttonTexts[buttonObject] = texts[i];
            }

            buttonListeners.Add(buttonObject, listener);
            button.onClick.AddListener(listener);
        }
    }

    private void OnShootButtonClicked(GameObject buttonObject, TargetColor color)
    {
        if (blurPanel != null && blurPanel.activeSelf)
        {
            return;
        }

        if (buttonObject == null || shootingButtons.Contains(buttonObject))
        {
            return;
        }

        if (!remainingShotsPerButton.ContainsKey(buttonObject))
        {
            remainingShotsPerButton[buttonObject] = maxShotsPerButton;
        }

        int slotIndex = FindOrAssignShootSlot(buttonObject);

        if (slotIndex < 0)
        {
            Debug.Log("There is no available shoot position.");
            return;
        }

        bool isWaitingForTarget = FindTargetToShoot(color) == null;

        shootingButtons.Add(buttonObject);

        if (isWaitingForTarget)
        {
            waitingButtons.Add(buttonObject);
        }

        StartCoroutine(ShootTargets(buttonObject, color, slotIndex));

        if (isWaitingForTarget)
        {
            TryMergeWaitingButtons(buttonObject, color);
        }
    }

    private IEnumerator ShootTargets(GameObject buttonObject, TargetColor color, int slotIndex)
    {
        GameObject shootPosition = shootPositions[slotIndex];

        if (shootPosition == null || bulletPrefab == null)
        {
            if (bulletPrefab == null)
            {
                Debug.LogError("Bullet prefab has not been assigned to GamePlayController_LevelTwo.");
            }

            FinishButtonShooting(buttonObject);
            yield break;
        }

        while (GetRemainingShots(buttonObject) > 0)
        {
            if (!buttonObject.activeInHierarchy)
            {
                shootingButtons.Remove(buttonObject);
                waitingButtons.Remove(buttonObject);
                yield break;
            }

            GameObject target = FindTargetToShoot(color);

            if (target == null)
            {
                waitingButtons.Add(buttonObject);
                yield return new WaitForSeconds(Mathf.Max(0.01f, waitForTargetInterval));
                continue;
            }

            waitingButtons.Remove(buttonObject);
            reservedTargets.Add(target);
            Shoot(buttonObject, shootPosition, target);

            bool isMultiHitTarget = IsMultiHitTarget(target);

            if (!isMultiHitTarget)
            {
                DecreaseRemainingShots(buttonObject);
            }

            if (isMultiHitTarget)
            {
                yield return new WaitForSeconds(Mathf.Max(0.01f, shootInterval));
            }
            else
            {
                yield return new WaitUntil(() => target == null || !target.activeInHierarchy);

                if (shootInterval > 0f)
                {
                    yield return new WaitForSeconds(shootInterval);
                }
            }

            reservedTargets.Remove(target);
        }

        FinishButtonShooting(buttonObject);
    }

    private void FinishButtonShooting(GameObject buttonObject)
    {
        shootingButtons.Remove(buttonObject);
        waitingButtons.Remove(buttonObject);

        if (GetRemainingShots(buttonObject) <= 0)
        {
            int slotIndex = FindButtonSlot(buttonObject);

            if (slotIndex >= 0)
            {
                buttonsInSlots[slotIndex] = null;
            }

            buttonObject.SetActive(false);
        }
    }

    private void TryMergeWaitingButtons(GameObject mergedButton, TargetColor color)
    {
        List<GameObject> buttonsToMerge = new List<GameObject>();

        foreach (GameObject buttonObject in waitingButtons)
        {
            if (buttonObject != null
                && buttonObject.activeInHierarchy
                && buttonColors.TryGetValue(buttonObject, out TargetColor buttonColor)
                && buttonColor == color)
            {
                buttonsToMerge.Add(buttonObject);
            }
        }

        if (buttonsToMerge.Count < 3 || !buttonsToMerge.Contains(mergedButton))
        {
            return;
        }

        int totalRemainingShots = 0;

        for (int i = 0; i < 3; i++)
        {
            totalRemainingShots += GetRemainingShots(buttonsToMerge[i]);
        }

        for (int i = 0; i < 3; i++)
        {
            GameObject buttonObject = buttonsToMerge[i];

            if (buttonObject == mergedButton)
            {
                continue;
            }

            ReleaseButtonSlot(buttonObject);
            shootingButtons.Remove(buttonObject);
            waitingButtons.Remove(buttonObject);
            buttonObject.SetActive(false);
        }

        remainingShotsPerButton[mergedButton] = totalRemainingShots;
        waitingButtons.Add(mergedButton);
        UpdateButtonText(mergedButton);

    }

    private void ReleaseButtonSlot(GameObject buttonObject)
    {
        int slotIndex = FindButtonSlot(buttonObject);

        if (slotIndex >= 0)
        {
            buttonsInSlots[slotIndex] = null;
        }
    }

    private int GetRemainingShots(GameObject buttonObject)
    {
        return remainingShotsPerButton.TryGetValue(buttonObject, out int remainingShots)
            ? remainingShots
            : maxShotsPerButton;
    }

    private void DecreaseRemainingShots(GameObject buttonObject)
    {
        remainingShotsPerButton[buttonObject] = Mathf.Max(0, GetRemainingShots(buttonObject) - 1);
        UpdateButtonText(buttonObject);
    }

    private void UpdateAllButtonTexts()
    {
        foreach (GameObject buttonObject in buttonTexts.Keys)
        {
            UpdateButtonText(buttonObject);
        }
    }

    private void UpdateButtonText(GameObject buttonObject)
    {
        if (buttonTexts.TryGetValue(buttonObject, out TextMeshProUGUI text) && text != null)
        {
            text.text = GetRemainingShots(buttonObject).ToString();
        }
    }

    private GameObject FindTargetToShoot(TargetColor color)
    {
        if (targetColumns == null)
        {
            return null;
        }

        foreach (Transform targetColumn in targetColumns)
        {
            GameObject target = GetFrontTarget(targetColumn);

            if (target != null
                && (IsMultiHitTarget(target) || !reservedTargets.Contains(target))
                && (IsMultiHitTarget(target) || IsTargetColor(target, color)))
            {
                return target;
            }
        }

        return null;
    }

    private GameObject GetFrontTarget(Transform targetColumn)
    {
        if (targetColumn == null)
        {
            return null;
        }

        for (int i = 0; i < targetColumn.childCount; i++)
        {
            GameObject target = targetColumn.GetChild(i).gameObject;

            if (target.activeInHierarchy)
            {
                return target;
            }
        }

        return null;
    }

    private bool IsTargetColor(GameObject target, TargetColor color)
    {
        switch (color)
        {
            case TargetColor.Red:
                return redTargets != null && redTargets.Contains(target);
            case TargetColor.Blue:
                return blueTargets != null && blueTargets.Contains(target);
            case TargetColor.Green:
                return greenTargets != null && greenTargets.Contains(target);
            case TargetColor.Yellow:
                return yellowTargets != null && yellowTargets.Contains(target);
            default:
                return false;
        }
    }

    private void Shoot(GameObject buttonObject, GameObject shootPosition, GameObject target)
    {
        Vector3 direction = target.transform.position - buttonObject.transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + bulletRotationOffset;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

        buttonObject.transform.rotation = rotation;

        BulletController bullet = Instantiate(
            bulletPrefab,
            shootPosition.transform.position,
            rotation,
            shootPosition.transform);

        bullet.MoveToTarget(target, true);
    }

    private bool IsMultiHitTarget(GameObject target)
    {
        return target != null
            && multiHitTargets != null
            && multiHitTargets.Contains(target)
            && target.GetComponent<MultiHitTargetController>() != null;
    }

    private int FindOrAssignShootSlot(GameObject buttonObject)
    {
        for (int i = 0; i < buttonsInSlots.Length; i++)
        {
            if (buttonsInSlots[i] == buttonObject)
            {
                return i;
            }
        }

        for (int i = 0; i < buttonsInSlots.Length; i++)
        {
            if ((buttonsInSlots[i] == null || !buttonsInSlots[i].activeInHierarchy)
                && shootPositions[i] != null)
            {
                Transform originalParent = buttonObject.transform.parent;
                buttonsInSlots[i] = buttonObject;

                buttonObject.transform.SetParent(shootPositions[i].transform, true);
                buttonObject.transform.position = shootPositions[i].transform.position;

                RectTransform originalParentRect = originalParent as RectTransform;

                if (originalParentRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(originalParentRect);
                }

                RevealNextButton(buttonObject);

                return i;
            }
        }

        return -1;
    }

    private void InitializeButtonQueues()
    {
        if (buttonContainers == null)
        {
            return;
        }

        foreach (Transform buttonContainer in buttonContainers)
        {
            if (buttonContainer == null)
            {
                continue;
            }

            for (int i = 0; i < buttonContainer.childCount; i++)
            {
                GameObject buttonObject = buttonContainer.GetChild(i).gameObject;
                buttonQueueParents[buttonObject] = buttonContainer;
                SetButtonVisibility(buttonObject, i == 0);
            }
        }
    }

    private void RevealNextButton(GameObject selectedButton)
    {
        if (!buttonQueueParents.TryGetValue(selectedButton, out Transform buttonContainer)
            || buttonContainer == null)
        {
            return;
        }

        for (int i = 0; i < buttonContainer.childCount; i++)
        {
            GameObject nextButton = buttonContainer.GetChild(i).gameObject;
            Button button = nextButton.GetComponent<Button>();

            if (button != null && !button.interactable)
            {
                SetButtonVisibility(nextButton, true);

                RectTransform containerRect = buttonContainer as RectTransform;

                if (containerRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
                }

                return;
            }
        }
    }

    private void SetButtonVisibility(GameObject buttonObject, bool showColor)
    {
        if (buttonObject == null)
        {
            return;
        }

        ShootButtonVisual buttonVisual = buttonObject.GetComponent<ShootButtonVisual>();

        if (buttonVisual != null)
        {
            buttonVisual.SetColorVisible(showColor);
        }
        else
        {
            //Debug.LogWarning($"{buttonObject.name} is missing ShootButtonVisual.");
        }
    }

    private int FindButtonSlot(GameObject buttonObject)
    {
        for (int i = 0; i < buttonsInSlots.Length; i++)
        {
            if (buttonsInSlots[i] == buttonObject)
            {
                return i;
            }
        }

        return -1;
    }

    private bool HasActiveTargetsOfColor(TargetColor color)
    {
        List<GameObject> targets = GetTargetList(color);

        if (targets == null)
        {
            return false;
        }

        foreach (GameObject target in targets)
        {
            if (target != null && target.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private List<GameObject> GetTargetList(TargetColor color)
    {
        switch (color)
        {
            case TargetColor.Red:
                return redTargets;
            case TargetColor.Blue:
                return blueTargets;
            case TargetColor.Green:
                return greenTargets;
            case TargetColor.Yellow:
                return yellowTargets;
            default:
                return null;
        }
    }

    private int GetTargetCount(List<GameObject> targets)
    {
        return targets == null ? 0 : targets.Count;
    }

    private void UpdateLevelBar()
    {
        if (totalTargetCount <= 0)
        {
            return;
        }

        int remainingTargets = CountActiveTargets(redTargets)
            + CountActiveTargets(blueTargets)
            + CountActiveTargets(greenTargets)
            + CountActiveTargets(multiHitTargets)
            + CountActiveTargets(yellowTargets);

        float progress = 1f - remainingTargets / (float)totalTargetCount;

        if (levelBar != null)
        {
            levelBar.minValue = 0f;
            levelBar.maxValue = 1f;
            levelBar.value = Mathf.Clamp01(progress);
        }
    }

    private int CountActiveTargets(List<GameObject> targets)
    {
        if (targets == null)
        {
            return 0;
        }

        int count = 0;

        foreach (GameObject target in targets)
        {
            if (target != null && target.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    private void CheckGameplayFinished()
    {
        if (gameplayFinished || totalTargetCount <= 0)
        {
            return;
        }

        if (CountActiveTargets(redTargets)
            + CountActiveTargets(blueTargets)
            + CountActiveTargets(greenTargets)
            + CountActiveTargets(multiHitTargets)
            + CountActiveTargets(yellowTargets) == 0)
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
    }

    private void OnGotItButtonClicked()
    {
        if (blurPanel != null)
        {
            blurPanel.SetActive(false);
        }
    }
}
