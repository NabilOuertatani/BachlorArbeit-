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
    public GameObject dogPanel;  // "DogPannel" — robot controls, should only show on the config screen
    public GameObject[] sceneEnvironmentObjects;  // Walls, NavMesh_Ground — 3D scene, should only show on the config screen

    [Header("Config Screen")]
    public TMP_Dropdown gestureStepDropdown;
    public Transform sequenceListParent;
    public GameObject stepItemTemplate;
    public TMP_Text previewText;

    [Header("Home Screen Cards")]
    public Transform savedSequencesPanel;
    public GameObject savedSequenceCardTemplate;

    [Header("Redesign — Home")]
    public GestureCard gestureCardPrefab;
    public TMP_Text homeCountText;          // mono "6 SAVED" top-right
    public TMP_Text filterAllText;          // "All 6" chip label
    public Button filterAllButton;
    public Button filterRecentButton;
    public Button filterIdleButton;
    public Button searchButton;
    public TMP_InputField searchInput;

    [Header("Redesign — Config")]
    public TMP_Text breadcrumbNameText;     // current gesture name in the breadcrumb
    public GameObject unsavedChip;          // amber "unsaved" chip

    private enum HomeFilter { All, Recent, Idle }
    private HomeFilter homeFilter = HomeFilter.All;
    private string searchQuery = "";
    private int runningIndex = -1;          // saved-sequence index currently executing (-1 = none)

    private readonly List<GestureStepData> sequenceSteps = new List<GestureStepData>();
    private readonly List<string> savedSequences = new List<string>();

    private int editingIndex = -1;

    void Start()
    {
        // Establish a known screen state — the scene may be saved with both
        // homeScreen and configScreen active, which lets the (invisible, but
        // still raycastable) home screen panel swallow clicks meant for the
        // 3D floor while the config screen is shown.
        ShowHome();

        // Ensure GestureDataManager exists
        if (GestureDataManager.Instance == null)
        {
            GameObject go = new GameObject("GestureDataManager");
            go.AddComponent<GestureDataManager>();
            Debug.Log("[GestureSequenceUI] Created GestureDataManager on startup");
        }

        // Wire the Home filter chips and search (references set by RedesignBuilder)
        if (filterAllButton != null)    filterAllButton.onClick.AddListener(SetFilterAll);
        if (filterRecentButton != null) filterRecentButton.onClick.AddListener(SetFilterRecent);
        if (filterIdleButton != null)   filterIdleButton.onClick.AddListener(SetFilterIdle);
        if (searchButton != null)       searchButton.onClick.AddListener(ToggleSearch);
        if (searchInput != null)
        {
            searchInput.onValueChanged.AddListener(OnSearchChanged);
            searchInput.gameObject.SetActive(false);
        }
        RefreshFilterChips();

        // Load and display saved sequences (with delay to ensure manager is ready)
        Invoke("DisplaySavedSequences", 0.1f);
    }

    public void ShowHome()
    {
        homeScreen.SetActive(true);
        configScreen.SetActive(false);
        if (dogPanel != null) dogPanel.SetActive(false);
        SetSceneEnvironmentActive(false);
    }

    public void ShowConfig()
    {
        homeScreen.SetActive(false);
        configScreen.SetActive(true);
        if (dogPanel != null) dogPanel.SetActive(true);
        SetSceneEnvironmentActive(true);
    }

    private void SetSceneEnvironmentActive(bool active)
    {
        if (sceneEnvironmentObjects == null) return;
        foreach (GameObject go in sceneEnvironmentObjects)
            if (go != null) go.SetActive(active);
    }

    public void AddStep()
    {
        string selectedStep = gestureStepDropdown.options[gestureStepDropdown.value].text;
        AddStepToUI(selectedStep);
    }

    /// <summary>
    /// Called by the "+ Add Waypoints" button.
    /// Creates a "Move" step (regardless of dropdown selection) AND makes sure
    /// floor-click input is enabled, so the user can immediately start clicking
    /// the floor to place waypoints without any extra setup step.
    /// </summary>
    public void AddWaypointsStep()
    {
        Debug.Log("[GestureSequenceUI] AddWaypointsStep() called — creating Move step and enabling floor clicks");

        // Always create a "Move" step for waypoint collection,
        // regardless of what's currently selected in the gesture dropdown.
        AddStepToUI("Move");

        // Make sure the robot scene is available and floor clicking is enabled
        // so the user can place waypoints right away.
        RobotBridge bridge = RobotBridge.Instance != null
            ? RobotBridge.Instance
            : FindFirstObjectByType<RobotBridge>();

        if (bridge != null)
        {
            bridge.EnableInput();
            Debug.Log("[GestureSequenceUI] Floor click input enabled via RobotBridge");
        }
        else
        {
            Debug.LogWarning("[GestureSequenceUI] RobotBridge not found — cannot enable floor input!");
        }
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
        MarkUnsaved();

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

        // AUTO-SAVE: persist immediately, but stay on the Configure screen so
        // the user can keep editing with the freshly added waypoints visible.
        SaveSequenceInPlace();
    }

    private void AddStepToUI(string stepName)
    {
        GestureStepData stepData = new GestureStepData(stepName);

        sequenceSteps.Add(stepData);

        GameObject newStep = Instantiate(stepItemTemplate, sequenceListParent);
        newStep.SetActive(true);

        TMP_Text stepText = newStep.GetComponentInChildren<TMP_Text>();
        stepText.text = sequenceSteps.Count + ". " + stepName;

        MarkUnsaved();
        UpdatePreview();
    }

    /// <summary>Shows the amber "unsaved" chip in the Configure breadcrumb.</summary>
    private void MarkUnsaved()
    {
        if (unsavedChip != null) unsavedChip.SetActive(true);
    }

    /// <summary>All waypoint positions of the sequence currently being edited (across Move steps).</summary>
    public List<Vector3> GetAllWaypointPositions()
    {
        var positions = new List<Vector3>();
        foreach (GestureStepData step in sequenceSteps)
            foreach (WaypointWithSpeed wp in step.waypoints)
                positions.Add(wp.unityPosition);
        return positions;
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
        if (SaveSequenceCore() < 0) return;

        ClearSequence();
        ShowHome();
        DisplaySavedSequences();
    }

    /// <summary>
    /// Saves like SaveSequence() but keeps the Configure screen open so the
    /// user can continue editing — used by the "Add points" auto-save.
    /// </summary>
    public void SaveSequenceInPlace()
    {
        int savedIndex = SaveSequenceCore();
        if (savedIndex < 0) return;

        // Keep editing the sequence we just saved so the next save updates it
        // instead of creating a duplicate.
        editingIndex = savedIndex;

        if (breadcrumbNameText != null)
            breadcrumbNameText.text = GestureDataManager.Instance.savedSequences[savedIndex].name;

        DisplaySavedSequences();
    }

    /// <summary>Builds and persists the current steps. Returns the index of the
    /// saved sequence in GestureDataManager, or -1 if nothing was saved.</summary>
    private int SaveSequenceCore()
    {
        if (sequenceSteps.Count == 0)
        {
            Debug.LogWarning("Cannot save empty sequence.");
            return -1;
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
        if (editingIndex >= 0 && editingIndex < GestureDataManager.Instance.savedSequences.Count)
        {
            // Keep the name and id of the sequence being edited
            GestureDataManager.SavedSequence existing = GestureDataManager.Instance.savedSequences[editingIndex];
            savedSeq.name = existing.name;
            savedSeq.id = existing.id;
        }
        else
        {
            savedSeq.name = "Sequence " + (GestureDataManager.Instance.savedSequences.Count + 1);
        }
        savedSeq.EnsureId();

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

        // Screenshot-on-save thumbnail of the waypoint path (see ThumbnailCapture)
        if (ThumbnailCapture.Instance != null)
        {
            string thumbPath = ThumbnailCapture.Instance.Capture(savedSeq.id, GetAllWaypointPositions());
            if (!string.IsNullOrEmpty(thumbPath))
                savedSeq.thumbnailPath = thumbPath;
        }

        if (unsavedChip != null) unsavedChip.SetActive(false);

        // Save to persistent storage (with full waypoint data AND speeds!)
        int savedIndex;
        if (editingIndex >= 0)
        {
            GestureDataManager.Instance.UpdateSequence(editingIndex, savedSeq);
            savedIndex = editingIndex;
            editingIndex = -1;
        }
        else
        {
            GestureDataManager.Instance.AddSequence(savedSeq);
            savedIndex = GestureDataManager.Instance.savedSequences.Count - 1;
        }

        Debug.Log("[GestureSequenceUI] Saved sequence with " + sequenceSteps.Count + " steps and waypoints with speeds");
        return savedIndex;
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

            Destroy(child.gameObject);
        }

        // Load from persistent storage
        List<GestureDataManager.SavedSequence> sequences = (GestureDataManager.Instance != null)
            ? GestureDataManager.Instance.savedSequences
            : new List<GestureDataManager.SavedSequence>();

        if (homeCountText != null)
            homeCountText.text = sequences.Count + " SAVED";
        if (filterAllText != null)
            filterAllText.text = "All " + sequences.Count;

        for (int i = 0; i < sequences.Count; i++)
        {
            int index = i;
            GestureDataManager.SavedSequence seq = sequences[i];

            if (!PassesHomeFilter(seq, index, sequences)) continue;

            if (gestureCardPrefab != null)
            {
                GestureCard card = Instantiate(gestureCardPrefab, savedSequencesPanel);
                card.gameObject.SetActive(true);
                card.gameObject.name = "SavedCard_" + index;

                GestureCard.Status status = (index == runningIndex)
                    ? GestureCard.Status.Running
                    : (seq.edited ? GestureCard.Status.Edited : GestureCard.Status.Ready);

                card.Bind(
                    seq.name,
                    BuildMetaLine(seq),
                    ThumbnailCapture.ResolvePath(seq.thumbnailPath, seq.id),
                    status,
                    onRun: () => RunSavedSequence(index),
                    onEdit: () => EditSavedSequence(index),
                    onDelete: () => DeleteSequence(index));
            }
            else if (savedSequenceCardTemplate != null)
            {
                // Legacy fallback if the redesigned prefab isn't wired
                GameObject newCard = Instantiate(savedSequenceCardTemplate, savedSequencesPanel);
                newCard.SetActive(true);
                newCard.name = "SavedCard_" + index;
                TMP_Text cardText = newCard.GetComponentInChildren<TMP_Text>();
                if (cardText != null) cardText.text = seq.name;
                Button button = newCard.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => EditSavedSequence(index));
                }
            }
        }

        Transform addButton = savedSequencesPanel.Find("AddNewConfigButton");
        if (addButton != null)
        {
            addButton.SetAsLastSibling();
        }
    }

    /// <summary>Mono metadata line for a card, e.g. "3 WAYPOINTS · SIT DOWN".</summary>
    private static string BuildMetaLine(GestureDataManager.SavedSequence seq)
    {
        int waypointCount = 0;
        string behavior = null;
        foreach (GestureDataManager.SavedStep step in seq.steps)
        {
            if (step.waypoints != null) waypointCount += step.waypoints.Count;
            if (behavior == null && step.stepName != "Move")
                behavior = step.stepName;
        }
        return waypointCount + " WAYPOINTS · " + (behavior ?? "Move").ToUpperInvariant();
    }

    private bool PassesHomeFilter(GestureDataManager.SavedSequence seq, int index,
                                  List<GestureDataManager.SavedSequence> all)
    {
        if (!string.IsNullOrEmpty(searchQuery) &&
            (seq.name == null || seq.name.ToLowerInvariant().IndexOf(searchQuery.ToLowerInvariant()) < 0))
            return false;

        switch (homeFilter)
        {
            case HomeFilter.Recent:
                // The three most recently saved sequences
                int newer = 0;
                foreach (GestureDataManager.SavedSequence other in all)
                    if (other != seq && other.savedAt > seq.savedAt) newer++;
                return newer < 3;
            case HomeFilter.Idle:
                return index != runningIndex;
            default:
                return true;
        }
    }

    // ── Filter chip / search handlers (wired to the Home chip row) ────────

    public void SetFilterAll()    { homeFilter = HomeFilter.All;    RefreshFilterChips(); DisplaySavedSequences(); }
    public void SetFilterRecent() { homeFilter = HomeFilter.Recent; RefreshFilterChips(); DisplaySavedSequences(); }
    public void SetFilterIdle()   { homeFilter = HomeFilter.Idle;   RefreshFilterChips(); DisplaySavedSequences(); }

    public void ToggleSearch()
    {
        if (searchInput == null) return;
        bool show = !searchInput.gameObject.activeSelf;
        searchInput.gameObject.SetActive(show);
        if (show)
        {
            searchInput.text = "";
            searchInput.Select();
            searchInput.ActivateInputField();
        }
        else
        {
            searchQuery = "";
            DisplaySavedSequences();
        }
    }

    public void OnSearchChanged(string query)
    {
        searchQuery = query != null ? query.Trim() : "";
        DisplaySavedSequences();
    }

    private void RefreshFilterChips()
    {
        StyleChip(filterAllButton,    homeFilter == HomeFilter.All);
        StyleChip(filterRecentButton, homeFilter == HomeFilter.Recent);
        StyleChip(filterIdleButton,   homeFilter == HomeFilter.Idle);
    }

    private static void StyleChip(Button chip, bool active)
    {
        if (chip == null) return;
        Image bg = chip.GetComponent<Image>();
        if (bg != null)
            bg.color = active ? UITheme.WithAlpha(UITheme.Accent, 0.28f) : UITheme.Panel;
        TMP_Text label = chip.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.color = active ? UITheme.AccentSoft : UITheme.TextSecondary;
    }

    /// <summary>Run a saved gesture straight from its Home-screen card.</summary>
    public void RunSavedSequence(int index)
    {
        EditSavedSequence(index);
        runningIndex = index;
        if (unsavedChip != null) unsavedChip.SetActive(false);
        PlaySequence();
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

        if (breadcrumbNameText != null) breadcrumbNameText.text = savedSeq.name;
        if (unsavedChip != null) unsavedChip.SetActive(false);

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

        runningIndex = -1;
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
        // Use direct TCP instead of ROS TCP Connector (which sends binary, not JSON)
        SendGestureViaTCP(apiId);
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
            "sit down"    => 1005,  // StandDown — slow damped ~2 s crouch (1009 Sit is a fixed fast trick)
            "jump"        => 1031,  // Jump
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
            "sit down"    => 2.5f,  // StandDown motion is ~2 s; small margin before the next step
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

        if (breadcrumbNameText != null) breadcrumbNameText.text = "New gesture";
        if (unsavedChip != null) unsavedChip.SetActive(false);
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