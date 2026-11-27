using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GamePlayController_LevelSevenTeen : MonoBehaviour
{
    [System.Serializable]
    private class PairedButtonGroup
    {
        public GameObject group;
        public GameObject firstButton;
        public GameObject secondButton;
        public TextMeshProUGUI firstText;
        public TextMeshProUGUI secondText;
        public TargetColor firstColor;
        public TargetColor secondColor;
    }

    [SerializeField] private List<GameObject> shootPositions;
    [SerializeField] private List<Transform> buttonContainers;
    [SerializeField] private List<GameObject> redShootBtns;
    [SerializeField] private List<GameObject> blueShootBtns;
    [SerializeField] private List<GameObject> greenShootBtns;
    [SerializeField] private List<GameObject> yellowShootBtns;
    [SerializeField] private List<PairedButtonGroup> pairedButtons;
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

    private readonly Dictionary<GameObject, UnityAction> buttonListeners = new Dictionary<GameObject, UnityAction>();
    private readonly Dictionary<GameObject, UnityAction> pairedButtonListeners = new Dictionary<GameObject, UnityAction>();
    private readonly HashSet<GameObject> shootingButtons = new HashSet<GameObject>();
    private readonly HashSet<GameObject> waitingButtons = new HashSet<GameObject>();
    private readonly HashSet<GameObject> reservedTargets = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, int> remainingShotsPerButton = new Dictionary<GameObject, int>();
    private readonly Dictionary<GameObject, TextMeshProUGUI> buttonTexts = new Dictionary<GameObject, TextMeshProUGUI>();
    private readonly Dictionary<GameObject, TargetColor> buttonColors = new Dictionary<GameObject, TargetColor>();
    private readonly Dictionary<GameObject, Transform> buttonQueueParents = new Dictionary<GameObject, Transform>();
    private readonly Dictionary<GameObject, PairedButtonGroup> pairedButtonLookup = new Dictionary<GameObject, PairedButtonGroup>();
    private readonly Dictionary<GameObject, Transform> pairedQueueParents = new Dictionary<GameObject, Transform>();
    private readonly HashSet<GameObject> activePairedGroups = new HashSet<GameObject>();
    private readonly Dictionary<GameObject, int[]> pairedGroupSlots = new Dictionary<GameObject, int[]>();
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
            SetBlurRaycastBlocking(false);
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
        AddPairedButtonListeners();

        if (nextLevelBtn != null)
        {
            nextLevelBtn.onClick.AddListener(OnNextLevelButtonClicked);
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

        foreach (KeyValuePair<GameObject, UnityAction> listener in pairedButtonListeners)
        {
            Button button = listener.Key == null ? null : listener.Key.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.RemoveListener(listener.Value);
            }
        }

        pairedButtonListeners.Clear();

        if (nextLevelBtn != null)
        {
            nextLevelBtn.onClick.RemoveListener(OnNextLevelButtonClicked);
        }

    }

    private void AddPairedButtonListeners()
    {
        if (pairedButtons == null)
        {
            return;
        }

        foreach (PairedButtonGroup pair in pairedButtons)
        {
            if (pair == null || pair.group == null || pair.firstButton == null || pair.secondButton == null)
            {
                Debug.LogWarning("A paired button entry is incomplete.");
                continue;
            }

            pairedButtonLookup[pair.firstButton] = pair;
            pairedButtonLookup[pair.secondButton] = pair;
            pairedQueueParents[pair.group] = pair.group.transform.parent;

            RegisterPairedMember(pair, pair.firstButton, pair.firstText, pair.firstColor);
            RegisterPairedMember(pair, pair.secondButton, pair.secondText, pair.secondColor);
        }
    }

    private void RegisterPairedMember(
        PairedButtonGroup pair,
        GameObject buttonObject,
        TextMeshProUGUI text,
        TargetColor color)
    {
        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError($"{buttonObject.name} must have a Button component.");
            return;
        }

        buttonColors[buttonObject] = color;
        remainingShotsPerButton[buttonObject] = maxShotsPerButton;
        buttonTexts[buttonObject] = text;

        GameObject capturedButton = buttonObject;
        UnityAction listener = () => OnPairedButtonClicked(capturedButton);
        pairedButtonListeners[buttonObject] = listener;
        button.onClick.AddListener(listener);
    }

    private void OnPairedButtonClicked(GameObject clickedButton)
    {
        Debug.Log($"Level 16 paired button clicked: {(clickedButton == null ? "null" : clickedButton.name)}");

        if (clickedButton == null
            || !pairedButtonLookup.TryGetValue(clickedButton, out PairedButtonGroup pair)
            || activePairedGroups.Contains(pair.group))
        {
            return;
        }

        StartGameplayOnFirstShoot();

        int[] slotIndices = FindOrAssignPairedGroupSlots(pair);

        if (slotIndices == null)
        {
            Debug.Log("There are not two available shoot positions for the paired buttons.");
            return;
        }

        activePairedGroups.Add(pair.group);
        pairedGroupSlots[pair.group] = slotIndices;
        StartCoroutine(ShootPairedMember(pair, pair.firstButton, pair.firstColor, slotIndices[0]));
        StartCoroutine(ShootPairedMember(pair, pair.secondButton, pair.secondColor, slotIndices[1]));
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

            if (buttonObject == null || pairedButtonLookup.ContainsKey(buttonObject)
                || buttonListeners.ContainsKey(buttonObject))
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
        Debug.Log($"Level 16 shoot button clicked: {(buttonObject == null ? "null" : buttonObject.name)}");

        if (buttonObject == null || shootingButtons.Contains(buttonObject))
        {
            return;
        }

        StartGameplayOnFirstShoot();

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

    private IEnumerator ShootPairedMember(
        PairedButtonGroup pair,
        GameObject buttonObject,
        TargetColor color,
        int slotIndex)
    {
        if (buttonObject == null || bulletPrefab == null || slotIndex < 0 || slotIndex >= shootPositions.Count)
        {
            FinishPairedMember(pair, buttonObject);
            yield break;
        }

        shootingButtons.Add(buttonObject);

        while (GetRemainingShots(buttonObject) > 0)
        {
            if (!buttonObject.activeInHierarchy || pair.group == null || !pair.group.activeInHierarchy)
            {
                break;
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
            Shoot(buttonObject, shootPositions[slotIndex], target);

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

        FinishPairedMember(pair, buttonObject);
    }

    private void FinishPairedMember(PairedButtonGroup pair, GameObject buttonObject)
    {
        shootingButtons.Remove(buttonObject);
        waitingButtons.Remove(buttonObject);

        if (GetRemainingShots(buttonObject) <= 0 && buttonObject != null)
        {
            buttonObject.SetActive(false);
        }

        if (pair == null || pair.firstButton == null || pair.secondButton == null
            || shootingButtons.Contains(pair.firstButton)
            || shootingButtons.Contains(pair.secondButton))
        {
            return;
        }

        activePairedGroups.Remove(pair.group);

        if (pair.group != null)
        {
            ReleasePairedGroupSlot(pair.group);
            pair.group.SetActive(false);
        }
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

    private void Shoot(GameObject buttonObject, Transform bulletOrigin, GameObject target)
    {
        Vector3 direction = target.transform.position - buttonObject.transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + bulletRotationOffset;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        buttonObject.transform.rotation = rotation;

        BulletController bullet = Instantiate(
            bulletPrefab,
            bulletOrigin.position,
            rotation,
            bulletOrigin);

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

                if (pairedButtonLookup.ContainsKey(buttonObject)
                    || pairedButtons.Exists(pair => pair != null && pair.group == buttonObject))
                {
                    continue;
                }

                if (buttonObject.GetComponent<Button>() == null)
                {
                    continue;
                }

                buttonQueueParents[buttonObject] = buttonContainer;
                SetButtonVisibility(buttonObject, i == 0);
            }

            InitializePairedGroupsInQueue(buttonContainer);
        }
    }

    private void InitializePairedGroupsInQueue(Transform buttonContainer)
    {
        if (pairedButtons == null)
        {
            return;
        }

        foreach (PairedButtonGroup pair in pairedButtons)
        {
            if (pair == null || pair.group == null || pair.group.transform.parent == null
                || !IsDescendantOf(pair.group.transform, buttonContainer))
            {
                continue;
            }

            Transform queueParent = pair.group.transform.parent;
            pairedQueueParents[pair.group] = queueParent;
            SetPairedButtonVisibility(pair, IsFirstQueueItem(pair.group, queueParent));
        }
    }

    private bool IsDescendantOf(Transform child, Transform ancestor)
    {
        if (child == null || ancestor == null)
        {
            return false;
        }

        Transform current = child;

        while (current != null)
        {
            if (current == ancestor)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsFirstQueueItem(GameObject group, Transform buttonContainer)
    {
        for (int i = 0; i < buttonContainer.childCount; i++)
        {
            GameObject child = buttonContainer.GetChild(i).gameObject;

            if (child == group || !pairedButtonLookup.ContainsKey(child))
            {
                return child == group;
            }
        }

        return false;
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

            PairedButtonGroup pair = GetPairedButtonGroup(nextButton);

            if (pair != null)
            {
                SetPairedButtonVisibility(pair, true);
                ForceRebuildLayout(buttonContainer);
                return;
            }

            Button button = nextButton.GetComponent<Button>();

            if (button != null && !button.interactable)
            {
                SetButtonVisibility(nextButton, true);

                ForceRebuildLayout(buttonContainer);

                return;
            }
        }
    }

    private PairedButtonGroup GetPairedButtonGroup(GameObject groupObject)
    {
        if (groupObject == null || pairedButtons == null)
        {
            return null;
        }

        return pairedButtons.Find(pair => pair != null && pair.group == groupObject);
    }

    private void SetPairedButtonVisibility(PairedButtonGroup pair, bool visible)
    {
        if (pair == null)
        {
            return;
        }

        SetButtonVisibility(pair.firstButton, visible);
        SetButtonVisibility(pair.secondButton, visible);
    }

    private void SetPairedQueueDecorationVisibility(PairedButtonGroup pair, bool visible)
    {
        if (pair == null || pair.group == null)
        {
            return;
        }

        for (int i = 0; i < pair.group.transform.childCount; i++)
        {
            GameObject child = pair.group.transform.GetChild(i).gameObject;

            if (child != pair.firstButton && child != pair.secondButton)
            {
                child.SetActive(visible);
            }
        }
    }

    private void ForceRebuildLayout(Transform container)
    {
        RectTransform containerRect = container as RectTransform;

        if (containerRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private int[] FindOrAssignPairedGroupSlots(PairedButtonGroup pair)
    {
        if (pair == null || pair.group == null || shootPositions == null)
        {
            return null;
        }

        if (pairedGroupSlots.TryGetValue(pair.group, out int[] existingSlots))
        {
            return existingSlots;
        }

        List<int> availableSlots = new List<int>();

        for (int i = 0; i < buttonsInSlots.Length && availableSlots.Count < 2; i++)
        {
            if ((buttonsInSlots[i] == null || !buttonsInSlots[i].activeInHierarchy)
                && shootPositions[i] != null)
            {
                availableSlots.Add(i);
            }
        }

        if (availableSlots.Count < 2)
        {
            return null;
        }

        Transform originalParent = pair.group.transform.parent;
        MovePairedMemberToSlot(pair.firstButton, availableSlots[0]);
        MovePairedMemberToSlot(pair.secondButton, availableSlots[1]);
        buttonsInSlots[availableSlots[0]] = pair.firstButton;
        buttonsInSlots[availableSlots[1]] = pair.secondButton;
        SetPairedQueueDecorationVisibility(pair, false);
        SetPairedButtonVisibility(pair, true);
        RevealNextPairedButton(pair.group);
        ForceRebuildLayout(originalParent);

        return availableSlots.ToArray();
    }

    private void MovePairedMemberToSlot(GameObject buttonObject, int slotIndex)
    {
        if (buttonObject == null || slotIndex < 0 || slotIndex >= shootPositions.Count
            || shootPositions[slotIndex] == null)
        {
            return;
        }

        buttonObject.transform.SetParent(shootPositions[slotIndex].transform, true);
        buttonObject.transform.position = shootPositions[slotIndex].transform.position;
    }

    private void RevealNextPairedButton(GameObject selectedGroup)
    {
        if (!pairedQueueParents.TryGetValue(selectedGroup, out Transform container)
            || container == null)
        {
            return;
        }

        for (int i = 0; i < container.childCount; i++)
        {
            GameObject next = container.GetChild(i).gameObject;

            PairedButtonGroup pair = GetPairedButtonGroup(next);

            if (pair != null)
            {
                SetPairedButtonVisibility(pair, true);
                ForceRebuildLayout(container);
                return;
            }

            Button button = next.GetComponent<Button>();

            if (button != null && !button.interactable)
            {
                SetButtonVisibility(next, true);
                ForceRebuildLayout(container);
                return;
            }
        }
    }

    private void ReleasePairedGroupSlot(GameObject group)
    {
        if (!pairedGroupSlots.TryGetValue(group, out int[] slotIndices))
        {
            return;
        }

        foreach (int slotIndex in slotIndices)
        {
            if (slotIndex >= 0 && slotIndex < buttonsInSlots.Length)
            {
                buttonsInSlots[slotIndex] = null;
            }
        }

        pairedGroupSlots.Remove(group);
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

    private void StartGameplayOnFirstShoot()
    {
        if (gameplayStarted)
        {
            return;
        }

        gameplayStarted = true;

        if (blurPanel != null)
        {
            blurPanel.SetActive(false);
        }
    }

    private void SetBlurRaycastBlocking(bool blocksRaycasts)
    {
        if (blurPanel == null)
        {
            return;
        }

        CanvasGroup[] canvasGroups = blurPanel.GetComponentsInChildren<CanvasGroup>(true);

        foreach (CanvasGroup canvasGroup in canvasGroups)
        {
            canvasGroup.blocksRaycasts = blocksRaycasts;
            canvasGroup.interactable = blocksRaycasts;
        }

        Graphic[] graphics = blurPanel.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.raycastTarget = blocksRaycasts;
        }
    }
}

