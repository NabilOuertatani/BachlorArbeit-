using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gesture Sequence UI Controller - Main UI for creating, editing, and executing gesture sequences.
/// 
/// Features:
/// - Create custom gesture sequences (Move + any gesture type)
/// - Add waypoints to Move steps via ground clicking
/// - Save/load/edit sequences persistently
/// - Execute complete sequences with proper timing
/// 
/// Data Flow:
/// 1. User creates sequence (Add Gesture)
/// 2. For Move: user clicks ground to add waypoints
/// 3. Save sequence → JSON file
/// 4. Play sequence → ExecuteSequenceCoroutine()
///    - Move steps: Navigate via MultiGoalManager
///    - Gesture steps: Send API command via ROS
/// 
/// <remarks>
/// Interaction with other systems:
/// - GestureDataManager: Persistent storage (JSON)
/// - MultiGoalManager: Waypoint navigation
/// - ROS Bridge: Gesture command execution
/// </remarks>
/// </summary>
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
        if (sequenceSteps.Count == 0)
        {
            Debug.LogWarning("No gestures to play!");
            return;
        }

        Debug.Log("▶ Playing RoboDog sequence: " + sequenceSteps.Count + " steps");
        StartCoroutine(ExecuteSequenceCoroutine());
    }

    private System.Collections.IEnumerator ExecuteSequenceCoroutine()
    {
        for (int i = 0; i < sequenceSteps.Count; i++)
        {
            GestureStepData step = sequenceSteps[i];
            Debug.Log($"[{i + 1}/{sequenceSteps.Count}] Executing: {step.stepName}");

            if (step.stepName == "Move")
            {
                yield return StartCoroutine(ExecuteMoveStep(step));
            }
            else
            {
                yield return StartCoroutine(ExecuteGestureStep(step.stepName));
            }

            // Small delay between steps
            yield return new WaitForSeconds(0.5f);
        }

        Debug.Log("✓ Sequence complete!");
    }

    private System.Collections.IEnumerator ExecuteMoveStep(GestureStepData moveStep)
    {
        if (moveStep.waypoints.Count == 0)
        {
            Debug.LogWarning("Move step has no waypoints!");
            yield break;
        }

        Debug.Log($"  → Moving through {moveStep.waypoints.Count} waypoints...");

        MultiGoalManager mgm = FindObjectOfType<MultiGoalManager>();
        if (mgm == null)
        {
            Debug.LogError("MultiGoalManager not found!");
            yield break;
        }

        // Load waypoints and start navigation
        mgm.LoadWaypoints(moveStep.waypoints);
        mgm.StartNavigation();

        // Wait for navigation to complete (with timeout)
        float timeout = 120f;  // 2 minutes for all waypoints
        float elapsed = 0f;
        
        while (!mgm.IsNavigationComplete() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
        {
            Debug.LogWarning("Move step timed out!");
        }
        else
        {
            Debug.Log($"  ✓ Move step complete!");
        }

        mgm.ClearWaypoints();
    }

    private System.Collections.IEnumerator ExecuteGestureStep(string gestureName)
    {
        Debug.Log($"  → Executing gesture: {gestureName}");

        // Map gesture names to Sport API IDs and durations
        int apiId = GetGestureApiId(gestureName);
        float duration = GetGestureDuration(gestureName);

        if (apiId > 0)
        {
            Debug.Log($"  → Sending API command: id={apiId}, gesture='{gestureName}'");
            Debug.Log($"  → Gesture will take ~{duration:F1}s to complete");
            
            // Send actual ROS command
            SendGestureCommand(apiId);
        }
        else
        {
            Debug.LogWarning($"  ⚠ Unknown gesture: {gestureName}");
        }

        // Wait for gesture to complete
        yield return new WaitForSeconds(duration);
        Debug.Log($"  ✓ Gesture '{gestureName}' complete!");
    }

    private void SendGestureCommand(int apiId)
    {
        try
        {
            // Create JSON request matching unitree_api.msg.Request format
            // Header with API ID
            string jsonRequest = $@"{{
                ""header"": {{
                    ""identity"": {{
                        ""api_id"": {apiId}
                    }}
                }},
                ""parameter"": {{}}
            }}";

            // Find ROS-TCP bridge component
            ROS_TCPBridge bridge = FindObjectOfType<ROS_TCPBridge>();
            if (bridge != null)
            {
                // Send via ROS bridge to /api/sport/request topic
                bridge.Send("/api/sport/request", jsonRequest);
                Debug.Log($"[GestureSequenceUI] Sent gesture command via ROS-TCP: API {apiId}");
            }
            else
            {
                // Fallback: try to send via socket directly
                Debug.LogWarning("[GestureSequenceUI] ROS_TCPBridge not found. Attempting direct socket send...");
                SendGestureViaTCP(apiId);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GestureSequenceUI] Failed to send gesture command: {e.Message}");
        }
    }

    private void SendGestureViaTCP(int apiId)
    {
        try
        {
            // Fallback direct TCP connection to ROS bridge (localhost:10000)
            string json = $"{{\"header\":{{\"identity\":{{\"api_id\":{apiId}}}}},\"parameter\":{{}}}}";
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
            
            // Send with length prefix (4 bytes, big-endian)
            int len = data.Length;
            byte[] prefix = new byte[4]
            {
                (byte)(len >> 24), (byte)(len >> 16),
                (byte)(len >>  8), (byte)(len)
            };
            
            byte[] packet = new byte[4 + data.Length];
            System.Buffer.BlockCopy(prefix, 0, packet, 0, 4);
            System.Buffer.BlockCopy(data, 0, packet, 4, data.Length);
            
            System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient("127.0.0.1", 10000);
            client.GetStream().Write(packet, 0, packet.Length);
            client.Close();
            
            Debug.Log($"[GestureSequenceUI] Sent gesture via direct TCP: API {apiId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GestureSequenceUI] Direct TCP send failed: {e.Message}");
        }
    }

    private int GetGestureApiId(string gestureName)
    {
        return gestureName switch
        {
            "Raise Hand" => 1016,  // Hello (wave FR leg)
            "Stand Up" => 1002,     // StandUp
            "Sit Down" => 1003,     // StandDown
            "Jump" => 1022,         // Dance1 (has jump-like motion)
            "Stretch" => 1017,      // Stretch
            "Dance" => 1022,        // Dance1
            _ => -1  // Unknown gesture
        };
    }

    private float GetGestureDuration(string gestureName)
    {
        return gestureName switch
        {
            "Raise Hand" => 3.0f,   // Hello gesture takes ~3s
            "Stand Up" => 2.0f,     // Stand up takes ~2s
            "Sit Down" => 2.0f,     // Sit down takes ~2s
            "Jump" => 2.5f,         // Dance/jump takes ~2.5s
            "Stretch" => 2.0f,      // Stretch takes ~2s
            "Dance" => 3.0f,        // Dance takes ~3s
            _ => 1.0f
        };
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