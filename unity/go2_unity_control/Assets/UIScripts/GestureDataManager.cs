using System.Collections.Generic;
using UnityEngine;
using System.IO;

/// <summary>
/// Persistent storage for gesture sequences WITH waypoints.
/// Saves complete step data including waypoints for each step.
/// </summary>
public class GestureDataManager : MonoBehaviour
{
    public static GestureDataManager Instance { get; private set; }

    private string savePath;
    public List<SavedSequence> savedSequences = new List<SavedSequence>();

    // Serializable wrapper classes
    [System.Serializable]
    public class SavedSequence
    {
        public string name;
        public List<SavedStep> steps = new List<SavedStep>();
    }

    [System.Serializable]
    public class SavedStep
    {
        public string stepName;
        public List<SerializableVector3> waypoints;  // Will be null for non-Move steps
    }

    [System.Serializable]
    public class SerializableVector3
    {
        public float x, y, z;

        public SerializableVector3() { }
        public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
        public Vector3 ToVector3() { return new Vector3(x, y, z); }
    }

    [System.Serializable]
    public class GestureDataFile
    {
        public List<SavedSequence> sequences = new List<SavedSequence>();
    }

    void Awake()
    {
        // Singleton pattern - survive scene switches
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Save in project folder (Git-friendly)
        string dataFolder = Application.dataPath + "/Data";
        if (!Directory.Exists(dataFolder))
            Directory.CreateDirectory(dataFolder);
        
        savePath = dataFolder + "/gestures.json";
        LoadSequences();
        Debug.Log("[GestureDataManager] Initialized at: " + savePath);
    }

    /// <summary>Load all sequences from file</summary>
    public void LoadSequences()
    {
        if (!File.Exists(savePath))
        {
            Debug.Log("[GestureDataManager] No save file found. Starting fresh.");
            savedSequences.Clear();
            return;
        }

        try
        {
            string json = File.ReadAllText(savePath);
            GestureDataFile data = JsonUtility.FromJson<GestureDataFile>(json);
            
            if (data != null && data.sequences != null)
            {
                savedSequences = new List<SavedSequence>(data.sequences);
                Debug.Log("[GestureDataManager] Loaded " + savedSequences.Count + " gesture sequences from disk");
            }
            else
            {
                savedSequences.Clear();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GestureDataManager] Failed to load: " + e.Message);
            savedSequences.Clear();
        }
    }

    /// <summary>Save all sequences to file</summary>
    public void SaveSequences(List<SavedSequence> sequences)
    {
        savedSequences = new List<SavedSequence>(sequences);
        
        GestureDataFile data = new GestureDataFile();
        data.sequences = new List<SavedSequence>(sequences);
        
        try
        {
            string json = JsonUtility.ToJson(data, true);
            
            // Remove empty waypoints arrays for non-Move steps
            // Replace: ,\n                    "waypoints": [] or "waypoints": [],
            json = System.Text.RegularExpressions.Regex.Replace(json, @",\s*""waypoints""\s*:\s*\[\]", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, @"""waypoints""\s*:\s*\[\],\s*", "");
            
            File.WriteAllText(savePath, json);
            Debug.Log("[GestureDataManager] Saved " + sequences.Count + " gesture sequences with waypoints");
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GestureDataManager] Failed to save: " + e.Message);
        }
    }

    /// <summary>Add a single sequence (with steps and waypoints)</summary>
    public void AddSequence(SavedSequence sequence)
    {
        savedSequences.Add(sequence);
        SaveSequences(savedSequences);
    }

    /// <summary>Update sequence at index</summary>
    public void UpdateSequence(int index, SavedSequence sequence)
    {
        if (index >= 0 && index < savedSequences.Count)
        {
            savedSequences[index] = sequence;
            SaveSequences(savedSequences);
        }
    }

    /// <summary>Delete sequence at index</summary>
    public void DeleteSequence(int index)
    {
        if (index >= 0 && index < savedSequences.Count)
        {
            savedSequences.RemoveAt(index);
            SaveSequences(savedSequences);
        }
    }

    /// <summary>Clear all sequences</summary>
    public void ClearAll()
    {
        savedSequences.Clear();
        SaveSequences(savedSequences);
        Debug.Log("[GestureDataManager] All gestures cleared");
    }
}
