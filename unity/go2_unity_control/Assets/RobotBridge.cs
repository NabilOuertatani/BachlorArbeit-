using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent singleton that bridges <c>GestureSequenceUI</c> (MainUI scene)
/// and <c>MultiGoalManager</c> (UnityInterface scene) across scene boundaries.
///
/// <para>
/// Survives all scene loads via <c>DontDestroyOnLoad</c>. When a Move step
/// is played, this bridge loads <c>UnityInterface</c> additively, disables
/// the entire Canvas so the user can watch in read-only mode, then restores
/// everything when the simulation ends.
/// </para>
///
/// <para><b>Lifecycle</b></para>
/// <list type="number">
///   <item>MainUI calls <see cref="LoadWaypointsAsync"/> or <see cref="LoadWaypoints"/>.</item>
///   <item>Bridge loads UnityInterface additively if not already loaded.</item>
///   <item>Canvas is disabled → watch-only mode.</item>
///   <item>Bridge waits for <see cref="MultiGoalManager"/> to register itself.</item>
///   <item>Navigation runs; UI is blocked throughout.</item>
///   <item>When UnityInterface unloads, Canvas is restored automatically.</item>
/// </list>
/// </summary>
public class RobotBridge : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────

    /// <summary>Gets the single shared instance of <see cref="RobotBridge"/>.</summary>
    public static RobotBridge Instance { get; private set; }

    // ── Private state ──────────────────────────────────────────────────────

    /// <summary>Reference to the <see cref="MultiGoalManager"/> in UnityInterface.</summary>
    private MultiGoalManager _mgm;

    /// <summary>
    /// <c>true</c> after UnityInterface has been requested to load.
    /// Reset to <c>false</c> when the scene unloads.
    /// </summary>
    private bool _robotSceneLoaded = false;

    /// <summary>
    /// The Canvas found in UnityInterface that is disabled during simulation.
    /// Stored so it can be re-enabled when the scene unloads.
    /// </summary>
    private Canvas _canvasBlocker;

    // ── Unity lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Enforces the singleton pattern and marks this object as persistent
    /// across scene loads.
    /// </summary>
    void Awake()
{
    if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
    Instance = this;
    DontDestroyOnLoad(gameObject);
    Debug.Log("[RobotBridge] Initialized.");
    // DO NOT call EnsureRobotSceneLoaded here
}

    /// <summary>Subscribes to the scene-unloaded event.</summary>
    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    /// <summary>Unsubscribes from the scene-unloaded event.</summary>
    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    // ── Registration (called by MultiGoalManager) ──────────────────────────

    /// <summary>
    /// Called by <see cref="MultiGoalManager"/> from its <c>Start()</c> method
    /// once the UnityInterface scene has finished loading.
    /// </summary>
    /// <param name="mgm">The <see cref="MultiGoalManager"/> instance to control.</param>
    public void RegisterMultiGoalManager(MultiGoalManager mgm)
    {
        _mgm = mgm;
        _robotSceneLoaded = true;
        Debug.Log("[RobotBridge] MultiGoalManager registered.");
    }

    // ── Navigation API (called by GestureSequenceUI) ───────────────────────

    /// <summary>
    /// Loads the UnityInterface scene if needed, waits for
    /// <see cref="MultiGoalManager"/> to register, then sends the waypoints.
    /// Use this overload when the scene may not yet be loaded.
    /// </summary>
    /// <param name="waypoints">
    /// World-space waypoints in Unity coordinates to send to
    /// <see cref="MultiGoalManager"/>.
    /// </param>
    /// <returns>Coroutine enumerator — yield this from a MonoBehaviour.</returns>
    public IEnumerator LoadWaypointsAsync(List<Vector3> waypoints)
    {
        EnsureRobotSceneLoaded();

        // Wait up to 5 seconds for MultiGoalManager to register
        float timeout = 5f;
        float elapsed = 0f;
        while (_mgm == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_mgm != null)
        {
            _mgm.LoadWaypoints(waypoints);
        }
        else
        {
            Debug.LogError(
                "[RobotBridge] LoadWaypointsAsync failed: " +
                "MultiGoalManager did not register within 5 seconds.");
        }
    }

    /// <summary>
    /// Asynchronously loads waypoints with speeds into <see cref="MultiGoalManager"/>.
    /// Waits for UnityInterface scene to load and MultiGoalManager to register.
    /// </summary>
    /// <param name="waypointsWithSpeeds">
    /// Waypoints with individual speeds (Unity positions + speeds).
    /// </param>
    public IEnumerator LoadWaypointsAsync(List<WaypointWithSpeed> waypointsWithSpeeds)
    {
        EnsureRobotSceneLoaded();

        // Wait up to 5 seconds for MultiGoalManager to register
        float timeout = 5f;
        float elapsed = 0f;
        while (_mgm == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_mgm != null)
        {
            _mgm.LoadWaypoints(waypointsWithSpeeds);
        }
        else
        {
            Debug.LogError(
                "[RobotBridge] LoadWaypointsAsync failed: " +
                "MultiGoalManager did not register within 5 seconds.");
        }
    }

    /// <summary>
    /// Synchronously sends waypoints to <see cref="MultiGoalManager"/>.
    /// Only safe to call after the UnityInterface scene is already loaded
    /// and <see cref="MultiGoalManager"/> has registered.
    /// </summary>
    /// <param name="waypoints">
    /// World-space waypoints in Unity coordinates.
    /// </param>
    public void LoadWaypoints(List<Vector3> waypoints)
    {
        EnsureRobotSceneLoaded();
        if (_mgm != null)
            _mgm.LoadWaypoints(waypoints);
        else
            Debug.LogWarning(
                "[RobotBridge] LoadWaypoints: MultiGoalManager not registered yet.");
    }

    /// <summary>
    /// Synchronously sends waypoints with speeds to <see cref="MultiGoalManager"/>.
    /// Only safe to call after the UnityInterface scene is already loaded
    /// and <see cref="MultiGoalManager"/> has registered.
    /// </summary>
    /// <param name="waypointsWithSpeeds">
    /// Waypoints with individual speeds (Unity positions + speeds).
    /// </param>
    public void LoadWaypoints(List<WaypointWithSpeed> waypointsWithSpeeds)
    {
        EnsureRobotSceneLoaded();
        if (_mgm != null)
            _mgm.LoadWaypoints(waypointsWithSpeeds);
        else
            Debug.LogWarning(
                "[RobotBridge] LoadWaypoints: MultiGoalManager not registered yet.");
    }

    /// <summary>
    /// Tells <see cref="MultiGoalManager"/> to begin executing the loaded
    /// waypoint sequence.
    /// </summary>
    public void StartNavigation()
    {
        if (_mgm != null)
            _mgm.StartNavigation();
        else
            Debug.LogWarning(
                "[RobotBridge] StartNavigation: MultiGoalManager not registered.");
    }

    /// <summary>
    /// Returns <c>true</c> when the robot has reached all waypoints and
    /// navigation is complete.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <see cref="MultiGoalManager"/> reports completion;
    /// <c>false</c> if still navigating or not registered.
    /// </returns>
    public bool IsNavigationComplete()
    {
        return _mgm != null && _mgm.IsNavigationComplete();
    }

    /// <summary>
    /// Clears all waypoints from <see cref="MultiGoalManager"/> and resets
    /// its internal state.
    /// </summary>
    public void ClearWaypoints()
    {
        if (_mgm != null)
            _mgm.ClearWaypoints();
    }

    /// <summary>
    /// Returns the current list of waypoints held by <see cref="MultiGoalManager"/>.
    /// </summary>
    /// <returns>
    /// List of Unity world-space positions, or an empty list if
    /// <see cref="MultiGoalManager"/> is not registered.
    /// </returns>
    public List<Vector3> GetCurrentWaypoints()
    {
        return _mgm != null ? _mgm.GetCurrentWaypoints() : new List<Vector3>();
    }

    /// <summary>Get current waypoints WITH speeds (for saving sequences)</summary>
    public List<WaypointWithSpeed> GetCurrentWaypointsWithSpeed()
    {
        return _mgm != null ? _mgm.GetCurrentWaypointsWithSpeed() : new List<WaypointWithSpeed>();
    }

    // ── Scene management ───────────────────────────────────────────────────

    /// <summary>
    /// Loads UnityInterface additively if it has not been loaded yet,
    /// then hides all other scenes and blocks all UI interaction.
    /// </summary>
    private void EnsureRobotSceneLoaded()
    {
        if (_robotSceneLoaded || _mgm != null)
            return;

        SceneManager.LoadScene("UnityInterface", LoadSceneMode.Additive);
        _robotSceneLoaded = true;
        Debug.Log("[RobotBridge] Loaded 'UnityInterface' additively.");

        // Block input after a small delay to ensure scene is ready
        StartCoroutine(BlockInputAfterDelay());
    }

    /// <summary>
    /// Waits one frame then hides all scenes except UnityInterface and blocks input.
    /// </summary>
    private IEnumerator BlockInputAfterDelay()
    {
        yield return null; // Wait one frame for scene to initialize
        HideAllScenesExcept("UnityInterface");
    }

    /// <summary>
    /// Disables all root GameObjects in every scene except
    /// <paramref name="visibleSceneName"/>, then blocks all UI interaction
    /// inside the visible scene so the user can only watch.
    /// </summary>
    /// <param name="visibleSceneName">
    /// The name of the scene that should remain visible.
    /// </param>
    private void HideAllScenesExcept(string visibleSceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.name == visibleSceneName)
                continue;

            foreach (GameObject obj in scene.GetRootGameObjects())
                obj.SetActive(false);

            Debug.Log($"[RobotBridge] Hidden scene: {scene.name}");
        }

        // Enter watch-only mode in the visible scene
        BlockAllInput("UnityInterface");
        DisableAllInteractablesInScene(visibleSceneName);
    }

    /// <summary>
    /// Finds all <see cref="Canvas"/> components in <paramref name="sceneName"/>
    /// and disables them, hiding all UI elements and blocking all pointer events.
    /// Also disables all <see cref="GraphicRaycaster"/> to prevent clicks from reaching the floor.
    /// Additionally disables input in MultiGoalManager to prevent waypoint collection.
    /// </summary>
    /// <param name="sceneName">
    /// Name of the scene whose Canvases should be disabled.
    /// </param>
    private void BlockAllInput(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid())
        {
            Debug.LogWarning($"[RobotBridge] BlockAllInput: scene '{sceneName}' not valid.");
            return;
        }

        int canvasCount = 0;
        int raycasterCount = 0;

        foreach (GameObject obj in scene.GetRootGameObjects())
        {
            // Disable ALL Canvas components recursively
            foreach (Canvas canvas in obj.GetComponentsInChildren<Canvas>())
            {
                if (canvas.enabled)
                {
                    canvas.enabled = false;
                    canvasCount++;
                    Debug.Log($"[RobotBridge] Disabled Canvas: '{canvas.name}'");
                }
            }

            // Disable ALL GraphicRaycasters recursively to block floor clicks
            foreach (GraphicRaycaster raycaster in obj.GetComponentsInChildren<GraphicRaycaster>())
            {
                if (raycaster.enabled)
                {
                    raycaster.enabled = false;
                    raycasterCount++;
                }
            }

            // Store first canvas for later restoration
            Canvas firstCanvas = obj.GetComponent<Canvas>();
            if (firstCanvas == null)
                firstCanvas = obj.GetComponentInChildren<Canvas>();
            if (firstCanvas != null && _canvasBlocker == null)
                _canvasBlocker = firstCanvas;
        }

        // Also disable input in MultiGoalManager to prevent waypoint collection
        if (_mgm != null)
            _mgm.DisableInput();

        if (canvasCount > 0)
            Debug.Log($"[RobotBridge] Watch-only mode: disabled {canvasCount} Canvas(es), {raycasterCount} Raycaster(s)");
        else
            Debug.LogWarning($"[RobotBridge] BlockAllInput: no Canvas found in scene '{sceneName}'.");
    }

    /// <summary>
    /// Re-enables all Canvases and GraphicRaycasters that were disabled by 
    /// <see cref="BlockAllInput"/>, restoring all UI buttons and pointer events.
    /// Also re-enables input in MultiGoalManager so waypoint collection works again.
    /// </summary>
    private void UnblockAllInput()
    {
        Scene unityInterfaceScene = SceneManager.GetSceneByName("UnityInterface");
        if (!unityInterfaceScene.IsValid())
        {
            Debug.LogWarning("[RobotBridge] UnblockAllInput: UnityInterface scene not found.");
            return;
        }

        int canvasCount = 0;
        int raycasterCount = 0;

        foreach (GameObject obj in unityInterfaceScene.GetRootGameObjects())
        {
            // Re-enable all Canvases
            foreach (Canvas canvas in obj.GetComponentsInChildren<Canvas>())
            {
                if (!canvas.enabled)
                {
                    canvas.enabled = true;
                    canvasCount++;
                }
            }

            // Re-enable all GraphicRaycasters
            foreach (GraphicRaycaster raycaster in obj.GetComponentsInChildren<GraphicRaycaster>())
            {
                if (!raycaster.enabled)
                {
                    raycaster.enabled = true;
                    raycasterCount++;
                }
            }
        }

        // Re-enable input in MultiGoalManager
        if (_mgm != null)
            _mgm.EnableInput();

        if (canvasCount > 0)
            Debug.Log($"[RobotBridge] Restored {canvasCount} Canvas(es), {raycasterCount} Raycaster(s)");

        _canvasBlocker = null;
    }

    /// <summary>
    /// Re-enables all root GameObjects in every loaded scene and restores
    /// UI interaction. Called when the simulation ends.
    /// </summary>
    private void ShowAllScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            foreach (GameObject obj in scene.GetRootGameObjects())
                obj.SetActive(true);

            Debug.Log($"[RobotBridge] Shown scene: {scene.name}");
        }

        EnableAllInteractablesInScene("MainUI");
        UnblockAllInput();
    }

    /// <summary>
    /// Disables all <see cref="Button"/> and <see cref="GraphicRaycaster"/>
    /// components under every root GameObject in <paramref name="sceneName"/>.
    /// </summary>
    /// <param name="sceneName">Target scene name.</param>
    private void DisableAllInteractablesInScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) return;

        foreach (GameObject obj in scene.GetRootGameObjects())
            SetInteractables(obj, false);

        Debug.Log($"[RobotBridge] Interactables disabled in scene: {sceneName}");
    }

    /// <summary>
    /// Re-enables all <see cref="Button"/> and <see cref="GraphicRaycaster"/>
    /// components under every root GameObject in <paramref name="sceneName"/>.
    /// </summary>
    /// <param name="sceneName">Target scene name.</param>
    private void EnableAllInteractablesInScene(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        if (!scene.IsValid()) return;

        foreach (GameObject obj in scene.GetRootGameObjects())
            SetInteractables(obj, true);

        Debug.Log($"[RobotBridge] Interactables restored in scene: {sceneName}");
    }

    /// <summary>
    /// Recursively sets the <c>interactable</c> flag on all
    /// <see cref="Button"/> components and the <c>enabled</c> flag on all
    /// <see cref="GraphicRaycaster"/> components found under
    /// <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Root GameObject to search.</param>
    /// <param name="enabled">
    /// <c>true</c> to enable interaction; <c>false</c> to disable.
    /// </param>
    private static void SetInteractables(GameObject root, bool enabled)
    {
        foreach (Button btn in root.GetComponentsInChildren<Button>())
            btn.interactable = enabled;

        foreach (GraphicRaycaster ray in root.GetComponentsInChildren<GraphicRaycaster>())
            ray.enabled = enabled;
    }

    // ── Scene event handler ────────────────────────────────────────────────

    /// <summary>
    /// Invoked automatically by <see cref="SceneManager"/> when any scene
    /// finishes unloading.
    ///
    /// <para>
    /// When <c>UnityInterface</c> unloads, this method resets the bridge
    /// state, re-shows all other scenes, and restores the Canvas so buttons
    /// are available again.
    /// </para>
    /// </summary>
    /// <param name="scene">The scene that was unloaded.</param>
    private void OnSceneUnloaded(Scene scene)
    {
        if (scene.name != "UnityInterface")
            return;

        _robotSceneLoaded = false;
        _mgm              = null;
        Debug.Log("[RobotBridge] 'UnityInterface' unloaded — bridge reset.");

        ShowAllScenes();
    }
}