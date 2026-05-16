using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class GestureSequenceUI : MonoBehaviour
{
    [Header("Screens")]
    public GameObject homeScreen;
    public GameObject configScreen;
    

    [Header("Config Screen")]
    public TMP_Dropdown gestureStepDropdown;
    public Transform sequenceListParent;
    public GameObject stepItemTemplate;
    public TMP_Text previewText;

    [Header("Home Screen Cards")]
    public Transform savedSequencesPanel;
    public GameObject savedSequenceCardTemplate;

    private readonly List<GestureStepData> sequenceSteps = new List<GestureStepData>();
    private readonly List<string> savedSequences = new List<string>();

    private int editingIndex = -1;

    void Start()
    {
        // Ensure GestureDataManager exists
        if (GestureDataManager.Instance == null)
        {
            GameObject go = new GameObject("GestureDataManager");
            go.AddComponent<GestureDataManager>();
            Debug.Log("[GestureSequenceUI] Created GestureDataManager on startup");
        }

        // Load and display saved sequences (with delay to ensure manager is ready)
        Invoke("DisplaySavedSequences", 0.1f);
    }

    public void ShowHome()
    {
        homeScreen.SetActive(true);
        configScreen.SetActive(false);
    }

    public void ShowConfig()
    {
        homeScreen.SetActive(false);
        configScreen.SetActive(true);
    }

    public void AddStep()
    {
        string selectedStep = gestureStepDropdown.options[gestureStepDropdown.value].text;
        AddStepToUI(selectedStep);
    }

    public void AddWaypoints()
    {
        Debug.Log("[GestureSequenceUI] AddWaypoints() called!");
        
        if (sequenceSteps.Count == 0)
        {
            Debug.LogWarning("[GestureSequenceUI] Add a step first before adding waypoints!");
            return;
        }

        // Get the last added step
        GestureStepData lastStep = sequenceSteps[sequenceSteps.Count - 1];
        Debug.Log("[GestureSequenceUI] Working on step: " + lastStep.stepName);

        // Only "Move" actions can have waypoints
        if (lastStep.stepName != "Move")
        {
            Debug.LogWarning("[GestureSequenceUI] Only 'Move' actions can have waypoints! Current: " + lastStep.stepName);
            return;
        }

        // Get MultiGoalManager and collect waypoints
        MultiGoalManager mgm = FindObjectOfType<MultiGoalManager>();
        if (mgm == null)
        {
            Debug.LogError("[GestureSequenceUI] MultiGoalManager not found in ANY scene!");
            return;
        }

        Debug.Log("[GestureSequenceUI] Found MultiGoalManager");

        List<Vector3> waypointsFromMGM = mgm.GetCurrentWaypoints();
        Debug.Log("[GestureSequenceUI] Got " + waypointsFromMGM.Count + " waypoints from MultiGoalManager");
        
        // Add waypoints to the current Move step
        lastStep.waypoints.Clear();
        lastStep.waypoints.AddRange(waypointsFromMGM);

        Debug.Log("[GestureSequenceUI] Added " + waypointsFromMGM.Count + " waypoints to step: " + lastStep.stepName);

        // Update UI to show waypoint count
        TMP_Text[] allStepTexts = sequenceListParent.GetComponentsInChildren<TMP_Text>();
        Debug.Log("[GestureSequenceUI] Found " + allStepTexts.Length + " text elements");
        
        if (allStepTexts.Length > 0)
        {
            TMP_Text lastStepText = allStepTexts[allStepTexts.Length - 1];
            lastStepText.text = sequenceSteps.Count + ". " + lastStep.stepName + " (" + lastStep.waypoints.Count + " wp)";
            Debug.Log("[GestureSequenceUI] Updated UI: " + lastStepText.text);
        }

        // Clear waypoints from MultiGoalManager
        mgm.ClearWaypoints();
        Debug.Log("[GestureSequenceUI] Cleared MultiGoalManager waypoints");
    }

    private void AddStepToUI(string stepName)
{
    GestureStepData stepData = new GestureStepData(stepName);

    sequenceSteps.Add(stepData);

    GameObject newStep = Instantiate(stepItemTemplate, sequenceListParent);
    newStep.SetActive(true);

    TMP_Text stepText = newStep.GetComponentInChildren<TMP_Text>();
    stepText.text = sequenceSteps.Count + ". " + stepName;

    UpdatePreview();
}

    private void UpdatePreview()
    {
        if (previewText == null) return;

        if (sequenceSteps.Count == 0)
        {
            previewText.text = "Sequence preview will appear here.";
            return;
        }

        List<string> previewSteps = new List<string>();

foreach (GestureStepData step in sequenceSteps)
{
    previewSteps.Add(step.stepName);
}

previewText.text = string.Join(" → ", previewSteps);
    }

    public void SaveSequence()
    {
        if (sequenceSteps.Count == 0)
        {
            Debug.LogWarning("Cannot save empty sequence.");
            return;
        }

        // Auto-create GestureDataManager if missing
        if (GestureDataManager.Instance == null)
        {
            GameObject go = new GameObject("GestureDataManager");
            go.AddComponent<GestureDataManager>();
            Debug.Log("[GestureSequenceUI] Created GestureDataManager");
        }

        // Create saved sequence with full step data (including waypoints)
        GestureDataManager.SavedSequence savedSeq = new GestureDataManager.SavedSequence();
        savedSeq.name = "Sequence " + (GestureDataManager.Instance.savedSequences.Count + 1);

        foreach (GestureStepData step in sequenceSteps)
        {
            GestureDataManager.SavedStep savedStep = new GestureDataManager.SavedStep();
            savedStep.stepName = step.stepName;
            
            // Only save waypoints for "Move" actions
            if (step.stepName == "Move")
            {
                savedStep.waypoints = new List<GestureDataManager.SerializableVector3>();
                foreach (Vector3 wp in step.waypoints)
                {
                    savedStep.waypoints.Add(new GestureDataManager.SerializableVector3(wp));
                }
            }
            
            savedSeq.steps.Add(savedStep);
        }

        // Save to persistent storage (with full waypoint data!)
        if (editingIndex >= 0)
        {
            if (GestureDataManager.Instance != null)
                GestureDataManager.Instance.UpdateSequence(editingIndex, savedSeq);
            editingIndex = -1;
        }
        else
        {
            if (GestureDataManager.Instance != null)
                GestureDataManager.Instance.AddSequence(savedSeq);
        }

        Debug.Log("[GestureSequenceUI] Saved sequence with " + sequenceSteps.Count + " steps and waypoints");
        ClearSequence();
        ShowHome();
        DisplaySavedSequences();
    }

    public void DisplaySavedSequences()
    {
        // Force reload from disk first
        if (GestureDataManager.Instance != null)
        {
            GestureDataManager.Instance.LoadSequences();
        }

        foreach (Transform child in savedSequencesPanel)
        {
            if (child.gameObject == savedSequenceCardTemplate) continue;
            if (child.gameObject.name == "AddNewConfigButton") continue;
            if (child.gameObject.name == "ExistingConfigurationsLabel") continue;
            if (child.gameObject.name == "RobotstatusPanel") continue;

            Destroy(child.gameObject);
        }

        // Load from persistent storage
        List<GestureDataManager.SavedSequence> sequences = (GestureDataManager.Instance != null) 
            ? GestureDataManager.Instance.savedSequences 
            : new List<GestureDataManager.SavedSequence>();
        
        for (int i = 0; i < sequences.Count; i++)
        {
            int index = i;

            GameObject newCard = Instantiate(savedSequenceCardTemplate, savedSequencesPanel);
            newCard.SetActive(true);
            newCard.name = "SavedCard_" + index;

            // Create display string from sequence
            string displayText = sequences[i].name + ": ";
            List<string> stepNames = new List<string>();
            foreach (GestureDataManager.SavedStep step in sequences[i].steps)
            {
                // Only show waypoint count for "Move" actions with waypoints
                if (step.stepName == "Move" && step.waypoints != null)
                {
                    stepNames.Add(step.stepName + "(" + step.waypoints.Count + "wp)");
                }
                else
                {
                    stepNames.Add(step.stepName);
                }
            }
            displayText += string.Join(" → ", stepNames);

            TMP_Text cardText = newCard.GetComponentInChildren<TMP_Text>();
            cardText.text = displayText;

            // Edit button (main card)
            Button button = newCard.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => EditSavedSequence(index));
            }

            // Add delete button as child
            GameObject deleteButtonObj = new GameObject("DeleteButton");
            deleteButtonObj.transform.SetParent(newCard.transform);
            deleteButtonObj.transform.SetAsLastSibling();
            
            RectTransform deleteRect = deleteButtonObj.AddComponent<RectTransform>();
            deleteRect.anchorMin = new Vector2(1, 1);  // Top-right
            deleteRect.anchorMax = new Vector2(1, 1);
            deleteRect.offsetMin = new Vector2(-12, -12);
            deleteRect.offsetMax = new Vector2(0, -1);

            Image deleteImg = deleteButtonObj.AddComponent<Image>();
            deleteImg.color = new Color(0, 0, 0);  // Red

            Button deleteBtn = deleteButtonObj.AddComponent<Button>();
            deleteBtn.targetGraphic = deleteImg;

            // Delete button text
            GameObject deleteTextObj = new GameObject("Text");
            deleteTextObj.transform.SetParent(deleteButtonObj.transform);
            RectTransform deleteTextRect = deleteTextObj.AddComponent<RectTransform>();
            deleteTextRect.anchorMin = Vector2.zero;
            deleteTextRect.anchorMax = Vector2.one;
            deleteTextRect.offsetMin = Vector2.zero;
            deleteTextRect.offsetMax = Vector2.zero;

            TMP_Text deleteText = deleteTextObj.AddComponent<TextMeshProUGUI>();
            deleteText.text = "X";
            deleteText.fontSize = 8;
            deleteText.alignment = TextAlignmentOptions.Center;
            deleteText.color = Color.white;

            // Delete button click handler
            int indexCopy = index;
            deleteBtn.onClick.AddListener(() => DeleteSequence(indexCopy));
        }

        Transform addButton = savedSequencesPanel.Find("AddNewConfigButton");
        if (addButton != null)
        {
            addButton.SetAsLastSibling();
        }
    }

    public void EditSavedSequence(int index)
    {
        if (GestureDataManager.Instance == null)
        {
            Debug.LogError("[GestureSequenceUI] GestureDataManager not found!");
            return;
        }

        editingIndex = index;
        ClearSequence();

        GestureDataManager.SavedSequence savedSeq = GestureDataManager.Instance.savedSequences[index];

        // Reconstruct sequence with waypoints
        foreach (GestureDataManager.SavedStep savedStep in savedSeq.steps)
        {
            GestureStepData stepData = new GestureStepData(savedStep.stepName);
            
            // Restore waypoints only if they exist (Move steps)
            if (savedStep.waypoints != null)
            {
                foreach (GestureDataManager.SerializableVector3 wp in savedStep.waypoints)
                {
                    stepData.waypoints.Add(wp.ToVector3());
                }
            }
            
            sequenceSteps.Add(stepData);

            // Create UI
            GameObject newStep = Instantiate(stepItemTemplate, sequenceListParent);
            newStep.SetActive(true);

            TMP_Text stepText = newStep.GetComponentInChildren<TMP_Text>();
            // Only show waypoint count for "Move" actions
            if (stepData.stepName == "Move")
            {
                stepText.text = sequenceSteps.Count + ". " + stepData.stepName + " (" + stepData.waypoints.Count + " wp)";
            }
            else
            {
                stepText.text = sequenceSteps.Count + ". " + stepData.stepName;
            }
        }

        UpdatePreview();
        ShowConfig();
        Debug.Log("[GestureSequenceUI] Loaded sequence: " + savedSeq.name + " with " + savedSeq.steps.Count + " steps");
    }

    public void PlaySequence()
    {
        Debug.Log("Playing RoboDog sequence:");

        foreach (GestureStepData step in sequenceSteps)
{
    Debug.Log(step.stepName);

    if (step.stepName == "Move")
    {
        Debug.Log("Move has " + step.waypoints.Count + " waypoints");
    }
}
    }

    public void ClearSequence()
    {
        sequenceSteps.Clear();

        foreach (Transform child in sequenceListParent)
        {
            if (child.gameObject != stepItemTemplate)
            {
                Destroy(child.gameObject);
            }
        }

        UpdatePreview();
    }

    public void StartNewSequence()
    {
        editingIndex = -1;
        ClearSequence();
        ShowConfig();
    }

    public void DeleteSequence(int index)
    {
        if (GestureDataManager.Instance == null)
        {
            Debug.LogError("[GestureSequenceUI] GestureDataManager not found!");
            return;
        }

        GestureDataManager.Instance.DeleteSequence(index);
        Debug.Log("[GestureSequenceUI] Deleted sequence at index " + index);

        // Reload and display
        DisplaySavedSequences();
        ShowHome();
    }
    

}