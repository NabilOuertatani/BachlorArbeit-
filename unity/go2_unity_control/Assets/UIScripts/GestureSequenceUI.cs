using System.Collections.Generic;
using TMPro;
using Unity.Robotics.ROSTCPConnector;
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

        // Get waypoints via RobotBridge (handles cross-scene communication)
        if (RobotBridge.Instance == null)
        {
            Debug.LogError("[GestureSequenceUI] RobotBridge not found! Add it to an empty GameObject in MainUI.unity.");
            return;
        }

        Debug.Log("[GestureSequenceUI] Retrieving waypoints via RobotBridge");

        List<WaypointWithSpeed> waypointsFromBridge = RobotBridge.Instance.GetCurrentWaypointsWithSpeed();
        Debug.Log("[GestureSequenceUI] Got " + waypointsFromBridge.Count + " waypoints with speeds");
        
        // Add waypoints to the current Move step
        lastStep.waypoints.Clear();
        lastStep.waypoints.AddRange(waypointsFromBridge);

        Debug.Log("[GestureSequenceUI] Added " + waypointsFromBridge.Count + " waypoints to step: " + lastStep.stepName);

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
        RobotBridge.Instance.ClearWaypoints();
        Debug.Log("[GestureSequenceUI] Cleared MultiGoalManager waypoints");

        // AUTO-SAVE: Save the sequence to JSON immediately after adding waypoints
        SaveSequence();
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

        // Create saved sequence with full step data (including waypoints and speeds)
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
                foreach (WaypointWithSpeed wp in step.waypoints)
                {
                    // Save waypoint position AND speed
                    savedStep.waypoints.Add(new GestureDataManager.SerializableVector3(wp.unityPosition, wp.speed));
                }
            }
            
            savedSeq.steps.Add(savedStep);
        }

        // Save to persistent storage (with full waypoint data AND speeds!)
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

        Debug.Log("[GestureSequenceUI] Saved sequence with " + sequenceSteps.Count + " steps and waypoints with speeds");
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

        // Reconstruct sequence with waypoints and speeds
        foreach (GestureDataManager.SavedStep savedStep in savedSeq.steps)
        {
            GestureStepData stepData = new GestureStepData(savedStep.stepName);
            
            // Restore waypoints only if they exist (Move steps)
            if (savedStep.waypoints != null)
            {
                foreach (GestureDataManager.SerializableVector3 wp in savedStep.waypoints)
                {
                    Vector3 unityPos = wp.ToVector3();
                    
                    // Recalculate ROS position from Unity position
                    // Standard transformation: ros_x = unity_z / 2, ros_y = -unity_x / 2
                    float rosX = unityPos.z / 2f;
                    float rosY = -unityPos.x / 2f;
                    Vector3 rosPos = new Vector3(rosX, rosY, 0);
                    
                    // Create WaypointWithSpeed with both position and speed
                    WaypointWithSpeed waypoint = new WaypointWithSpeed(unityPos, rosPos, wp.speed);
                    stepData.waypoints.Add(waypoint);
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

        Debug.Log("▶Playing RoboDog sequence: " + sequenceSteps.Count + " steps");
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
            Debug.LogWarning("[GestureSequenceUI] Move step has no waypoints — skipping.");
            yield break;
        }

        if (RobotBridge.Instance == null)
        {
            Debug.LogError("[GestureSequenceUI] RobotBridge not found!");
            yield break;
        }

        Debug.Log($"  → Moving through {moveStep.waypoints.Count} waypoints with per-waypoint speeds...");

        // Load waypoints with speeds into MultiGoalManager (via Bridge)
        // This preserves individual speeds for each waypoint segment
        // MultiGoalManager will use these speeds when sending waypoints via TCP
        yield return RobotBridge.Instance.LoadWaypointsAsync(moveStep.waypoints);

        // Start navigation — MultiGoalManager sends first waypoint via TCP
        // goal_navigation_node.py receives it and drives the real robot
        // When robot arrives, UDP signal comes back → MultiGoalManager._goalReached = true
        // → AdvanceToNextGoal() is called automatically for each waypoint
        RobotBridge.Instance.StartNavigation();

        // Wait for ALL waypoints to be completed
        // IsNavigationComplete() returns true when _isWalking = false in MultiGoalManager
        // which happens after the last AdvanceToNextGoal() call
        float timeout = 300f;  // 5 minutes max for a full move step
        float elapsed = 0f;

        while (!RobotBridge.Instance.IsNavigationComplete() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (elapsed >= timeout)
            Debug.LogWarning("[GestureSequenceUI] Move step timed out after 5 minutes.");
        else
            Debug.Log("  ✓ Move step complete — all waypoints reached!");

        RobotBridge.Instance.ClearWaypoints();
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
            ROSConnection ros = ROSConnection.GetOrCreateInstance();
            
            // Register publisher if not already registered
            // Topic matches what the Unitree Go2 expects
            ros.RegisterPublisher<SportRequestMsg>("/api/sport/request");

            SportRequestMsg msg = new SportRequestMsg();
            msg.header.identity.api_id = apiId;
            msg.parameter = "{}";

            ros.Publish("/api/sport/request", msg);
            Debug.Log($"[GestureSequenceUI] Published to /api/sport/request: api_id={apiId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GestureSequenceUI] Failed to publish gesture: {e.Message}");
            // Fallback to direct TCP
            SendGestureViaTCP(apiId);
        }
    }

    private void SendGestureViaTCP(int apiId)
    {
        try
        {
            // Fallback: direct TCP connection to ROS bridge (localhost:10000)
            // Only used if ROS publisher fails
            Debug.Log("[GestureSequenceUI] Using TCP fallback...");
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
            
            Debug.Log($"[GestureSequenceUI] Sent gesture via TCP fallback: API {apiId}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GestureSequenceUI] TCP fallback failed: {e.Message}");
        }
    }

    private int GetGestureApiId(string gestureName)
    {
        // Normalize to lowercase to avoid case mismatches from saved JSON
        return gestureName.ToLower().Trim() switch
        {
            "raise hand"  => 1016,  // Hello (wave FR leg)
            "stand up"    => 1002,  // StandUp
            "sit down"    => 1003,  // StandDown
            "jump"        => 1022,  // Dance1 (has jump-like motion)
            "stretch"     => 1017,  // Stretch
            "dance"       => 1022,  // Dance1
            _             => -1     // Unknown gesture
        };
    }

    private float GetGestureDuration(string gestureName)
    {
        // Normalize to lowercase to avoid case mismatches from saved JSON
        return gestureName.ToLower().Trim() switch
        {
            "raise hand"  => 3.0f,
            "stand up"    => 2.0f,
            "sit down"    => 2.0f,
            "jump"        => 2.5f,
            "stretch"     => 2.0f,
            "dance"       => 3.0f,
            _             => 1.0f
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