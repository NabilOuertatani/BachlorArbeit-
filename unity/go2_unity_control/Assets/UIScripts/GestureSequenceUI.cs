using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Make sure to include this for Button

public class GestureSequenceUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject homeScreen;
    public GameObject configScreen;

    public TMP_Dropdown gestureStepDropdown; // Dropdown for selecting gestures
    public Transform sequenceListParent; // Parent where the steps will be displayed
    public GameObject stepItemTemplate; // Template for a step item
    public TMP_Text previewText; // Text for sequence preview
    public Transform savedSequencesPanel; // Panel to hold saved sequence cards
    public GameObject savedSequenceCardTemplate; // Template for saved sequence card

    private List<string> sequenceSteps = new List<string>(); // Store the steps
    private List<string> savedSequences = new List<string>(); // Store saved sequences

    // Show the config screen
    public void ShowConfig()
    {
        homeScreen.SetActive(false);
        configScreen.SetActive(true);
    }

    // Add a new step to the sequence
    public void AddStep()
    {
        string selectedStep = gestureStepDropdown.options[gestureStepDropdown.value].text; // Get selected gesture

        // Add the new step to the list
        sequenceSteps.Add(selectedStep);

        // Instantiate the StepItemTemplate for this new step
        GameObject newStep = Instantiate(stepItemTemplate, sequenceListParent);
        newStep.SetActive(true); // Make the new step visible

        // Update the text inside the step item
        TMP_Text stepText = newStep.GetComponentInChildren<TMP_Text>();
        stepText.text = sequenceSteps.Count + ". " + selectedStep; // Step number and gesture name

        UpdatePreview(); // Update the preview text
    }

    // Update the preview text
    private void UpdatePreview()
    {
        if (sequenceSteps.Count == 0)
        {
            previewText.text = "Sequence preview will appear on the left.";
            return;
        }

        // Join all steps with "→" to show the sequence
        previewText.text = string.Join(" → ", sequenceSteps);
    }

    // Save the sequence
    public void SaveSequence()
    {
        string savedSequence = string.Join(" → ", sequenceSteps); // Join all steps as a single string
        Debug.Log("Saved sequence: " + savedSequence);

        // Add the sequence to the savedSequences list
        savedSequences.Add(savedSequence);

        // Instantiate a card for the saved sequence
        GameObject newSavedSequence = Instantiate(savedSequenceCardTemplate, savedSequencesPanel);
        newSavedSequence.SetActive(true);

        // Set the text of the saved sequence card
        TMP_Text sequenceText = newSavedSequence.GetComponentInChildren<TMP_Text>();
        sequenceText.text = savedSequence; // Set the saved sequence text

        // Set the OnClick() event for this button
        Button button = newSavedSequence.GetComponent<Button>();
        button.onClick.AddListener(() => EditSavedSequence(savedSequences.Count - 1)); // Pass the index of the saved sequence

        ShowHome(); // Show the home screen after saving
        DisplaySavedSequences(); // Display all saved sequences on the home screen
    }

    // Show the home screen and display saved sequences
    public void ShowHome()
    {
        homeScreen.SetActive(true); // Show the Home screen
        configScreen.SetActive(false); // Hide the Config screen
    }

    // Display all saved sequences as cards
    public void DisplaySavedSequences()
{
    // Clear the current saved sequences from the UI (if any)
    foreach (Transform child in savedSequencesPanel.transform)
    {
        if (child.gameObject != savedSequenceCardTemplate) // Don't delete the template
        {
            Destroy(child.gameObject);
        }
    }

    // Loop through the saved sequences and display them as cards
    for (int i = 0; i < savedSequences.Count; i++)
    {
        // Instantiate a new card for each saved sequence
        GameObject newSavedSequence = Instantiate(savedSequenceCardTemplate, savedSequencesPanel.transform);
        TMP_Text sequenceText = newSavedSequence.GetComponentInChildren<TMP_Text>();
        sequenceText.text = savedSequences[i]; // Set the saved sequence text

        // Set the OnClick() event for this button
        Button button = newSavedSequence.GetComponent<Button>();
        button.onClick.AddListener(() => EditSavedSequence(i)); // Pass the index to EditSavedSequence

        newSavedSequence.SetActive(true); // Make the new saved sequence card visible
    }
}

    // Edit a saved sequence when the user clicks on a saved card
    public void EditSavedSequence(int index)
    {
        string selectedSequence = savedSequences[index]; // Get the selected sequence from the list

        // You can load the selected sequence and show it in the config screen for editing
        Debug.Log("Editing sequence: " + selectedSequence);

        // Optionally, you can set the dropdown or other fields based on the sequence to edit it.
        // For example, you can reset the AddStep() button to add the steps of the selected sequence.

        // Switch to Config Screen to edit the sequence
        ShowConfig();
    }

    // Clear the sequence list
    public void ClearSequence()
    {
        sequenceSteps.Clear(); // Clear the sequence list

        // Remove all dynamically added steps from the Content area
        foreach (Transform child in sequenceListParent)
        {
            if (child.gameObject != stepItemTemplate) // Don't delete the template
            {
                Destroy(child.gameObject); // Destroy all other steps
            }
        }

        UpdatePreview(); // Reset the preview text to nothing
    }
}