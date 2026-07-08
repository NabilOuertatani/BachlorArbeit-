using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent singleton that bridges <c>GestureSequenceUI</c> and
/// <c>MultiGoalManager</c>, since both now live in the same single scene.
///
/// This is a simplified version — the previous additive scene loading /
/// hiding / watch-only-mode logic has been removed, since MainUI and
/// UnityInterface are now merged into one scene and don't need to be
/// loaded/unloaded or hidden from each other anymore.
/// </summary>
public class RobotBridge : MonoBehaviour
{
    // ── Singleton ──────────────────────────────────────────────────────────

    public static RobotBridge Instance { get; private set; }

    // ── Private state ──────────────────────────────────────────────────────

    private MultiGoalManager _mgm;

    // ── Unity lifecycle ────────────────────────────────────────────────────

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
    }

    // ── Registration (called by MultiGoalManager) ──────────────────────────

    /// <summary>
    /// Called by <see cref="MultiGoalManager"/> from its <c>Start()</c> method.
    /// </summary>
    public void RegisterMultiGoalManager(MultiGoalManager mgm)
    {
        _mgm = mgm;
        Debug.Log("[RobotBridge] MultiGoalManager registered.");
    }

    /// <summary>Finds MultiGoalManager in the scene if it hasn't registered itself yet.</summary>
    private MultiGoalManager GetMgm()
    {
        if (_mgm == null)
            _mgm = FindFirstObjectByType<MultiGoalManager>();
        return _mgm;
    }

    // ── Navigation API (called by GestureSequenceUI) ───────────────────────

    /// <summary>
    /// Waits (briefly, if needed) for MultiGoalManager to be available, then loads waypoints.
    /// </summary>
    public IEnumerator LoadWaypointsAsync(List<Vector3> waypoints)
    {
        float timeout = 5f;
        float elapsed = 0f;
        while (GetMgm() == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_mgm != null)
            _mgm.LoadWaypoints(waypoints);
        else
            Debug.LogError("[RobotBridge] LoadWaypointsAsync failed: MultiGoalManager not found.");
    }

    /// <summary>
    /// Waits (briefly, if needed) for MultiGoalManager to be available, then loads waypoints with speeds.
    /// </summary>
    public IEnumerator LoadWaypointsAsync(List<WaypointWithSpeed> waypointsWithSpeeds)
    {
        float timeout = 5f;
        float elapsed = 0f;
        while (GetMgm() == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_mgm != null)
            _mgm.LoadWaypoints(waypointsWithSpeeds);
        else
            Debug.LogError("[RobotBridge] LoadWaypointsAsync failed: MultiGoalManager not found.");
    }

    /// <summary>Synchronously sends waypoints to MultiGoalManager.</summary>
    public void LoadWaypoints(List<Vector3> waypoints)
    {
        if (GetMgm() != null)
            _mgm.LoadWaypoints(waypoints);
        else
            Debug.LogWarning("[RobotBridge] LoadWaypoints: MultiGoalManager not found.");
    }

    /// <summary>Synchronously sends waypoints with speeds to MultiGoalManager.</summary>
    public void LoadWaypoints(List<WaypointWithSpeed> waypointsWithSpeeds)
    {
        if (GetMgm() != null)
            _mgm.LoadWaypoints(waypointsWithSpeeds);
        else
            Debug.LogWarning("[RobotBridge] LoadWaypoints: MultiGoalManager not found.");
    }

    /// <summary>Tells MultiGoalManager to begin executing the loaded waypoint sequence.</summary>
    public void StartNavigation()
    {
        if (GetMgm() != null)
            _mgm.StartNavigation();
        else
            Debug.LogWarning("[RobotBridge] StartNavigation: MultiGoalManager not found.");
    }

    /// <summary>Returns true when the robot has reached all waypoints and navigation is complete.</summary>
    public bool IsNavigationComplete()
    {
        return GetMgm() != null && _mgm.IsNavigationComplete();
    }

    /// <summary>Clears all waypoints from MultiGoalManager and resets its internal state.</summary>
    public void ClearWaypoints()
    {
        if (GetMgm() != null)
            _mgm.ClearWaypoints();
    }

    /// <summary>Re-enables floor click input in MultiGoalManager.</summary>
    public void EnableInput()
    {
        if (GetMgm() != null)
            _mgm.EnableInput();
        else
            Debug.LogWarning("[RobotBridge] EnableInput: MultiGoalManager not found.");
    }

    /// <summary>Disables floor click input in MultiGoalManager.</summary>
    public void DisableInput()
    {
        if (GetMgm() != null)
            _mgm.DisableInput();
    }

    /// <summary>Returns the current list of waypoints held by MultiGoalManager.</summary>
    public List<Vector3> GetCurrentWaypoints()
    {
        return GetMgm() != null ? _mgm.GetCurrentWaypoints() : new List<Vector3>();
    }

    /// <summary>Get current waypoints WITH speeds (for saving sequences).</summary>
    public List<WaypointWithSpeed> GetCurrentWaypointsWithSpeed()
    {
        return GetMgm() != null ? _mgm.GetCurrentWaypointsWithSpeed() : new List<WaypointWithSpeed>();
    }
}