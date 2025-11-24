// ViperKit.UI - Views\MainWindow.Demo.cs
using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ViperKit.UI.Models;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    // ----------------------------
    // DEMO MODE – Start Demo
    // ----------------------------
    private void DemoStartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = DemoManager.StartDemo();

        if (result.Success)
        {
            UpdateDemoUI();
            UpdateDemoStatus(result.Message);

            // Show the walkthrough panel
            if (DemoWalkthroughPanel != null)
                DemoWalkthroughPanel.IsVisible = true;

            if (DemoStartButton != null)
                DemoStartButton.IsEnabled = false;

            if (DemoEndButton != null)
                DemoEndButton.IsEnabled = true;
        }
        else
        {
            UpdateDemoStatus($"Error: {result.Message}");
        }
    }

    // ----------------------------
    // DEMO MODE – End Demo
    // ----------------------------
    private void DemoEndButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var result = DemoManager.EndDemo();

        UpdateDemoStatus(result.Message);

        // Hide the walkthrough panel
        if (DemoWalkthroughPanel != null)
            DemoWalkthroughPanel.IsVisible = false;

        if (DemoStartButton != null)
            DemoStartButton.IsEnabled = true;

        if (DemoEndButton != null)
            DemoEndButton.IsEnabled = false;

        // Reset UI
        ResetDemoUI();
    }

    // ----------------------------
    // DEMO MODE – Navigation
    // ----------------------------
    private void DemoNextButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DemoManager.CompleteCurrentStep();
        UpdateDemoUI();

        // Navigate to the target tab if not on last step
        var step = DemoManager.GetCurrentStep();
        if (step != null && MainTabs != null)
        {
            MainTabs.SelectedIndex = step.TabIndex;
        }
    }

    private void DemoPrevButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DemoManager.PreviousStep();
        UpdateDemoUI();
    }

    private void DemoGoToTabButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var step = DemoManager.GetCurrentStep();
        if (step != null && MainTabs != null)
        {
            MainTabs.SelectedIndex = step.TabIndex;
        }
    }

    // ----------------------------
    // DEMO MODE – UI Updates
    // ----------------------------
    private void UpdateDemoUI()
    {
        var step = DemoManager.GetCurrentStep();
        if (step == null)
            return;

        // Update step indicator
        if (DemoStepIndicator != null)
            DemoStepIndicator.Text = $"Step {step.StepNumber} of {DemoManager.TotalSteps}";

        // Update step title
        if (DemoStepTitle != null)
            DemoStepTitle.Text = step.Title;

        // Update instructions
        if (DemoInstructions != null)
            DemoInstructions.Text = step.Instructions;

        // Update expected findings
        if (DemoExpectedFindings != null)
            DemoExpectedFindings.Text = step.ExpectedFindings;

        // Update tip
        if (DemoTip != null)
            DemoTip.Text = step.Tip;

        // Update action
        if (DemoAction != null)
            DemoAction.Text = $"Action: {step.ActionToTake}";

        // Update learning point
        if (DemoLearningPoint != null)
            DemoLearningPoint.Text = step.LearningPoint;

        // Update tab target button
        if (DemoGoToTabButton != null)
            DemoGoToTabButton.Content = $"Go to {step.TabTarget} tab";

        // Update navigation buttons
        if (DemoPrevButton != null)
            DemoPrevButton.IsEnabled = DemoManager.CurrentStep > 1;

        if (DemoNextButton != null)
        {
            bool isLastStep = DemoManager.CurrentStep >= DemoManager.TotalSteps;
            DemoNextButton.Content = isLastStep ? "Finish Demo" : "Next Step";
        }

        // Update progress bar
        if (DemoProgressBar != null)
        {
            DemoProgressBar.Maximum = DemoManager.TotalSteps;
            DemoProgressBar.Value = DemoManager.CurrentStep;
        }

        // Update artifacts list
        UpdateDemoArtifactsList();
    }

    private void UpdateDemoArtifactsList()
    {
        if (DemoArtifactsList != null)
        {
            DemoArtifactsList.ItemsSource = null;
            DemoArtifactsList.ItemsSource = DemoManager.Artifacts;
        }
    }

    private void UpdateDemoStatus(string message)
    {
        if (DemoStatusText != null)
            DemoStatusText.Text = $"Status: {message}";
    }

    private void ResetDemoUI()
    {
        if (DemoStepIndicator != null)
            DemoStepIndicator.Text = "Step 0 of 0";

        if (DemoStepTitle != null)
            DemoStepTitle.Text = "";

        if (DemoInstructions != null)
            DemoInstructions.Text = "";

        if (DemoExpectedFindings != null)
            DemoExpectedFindings.Text = "";

        if (DemoTip != null)
            DemoTip.Text = "";

        if (DemoAction != null)
            DemoAction.Text = "";

        if (DemoLearningPoint != null)
            DemoLearningPoint.Text = "";

        if (DemoProgressBar != null)
            DemoProgressBar.Value = 0;

        if (DemoArtifactsList != null)
            DemoArtifactsList.ItemsSource = null;

        UpdateDemoStatus("Ready to start demo");
    }

    // ----------------------------
    // DEMO MODE – Copy Search Term
    // ----------------------------
    private async void DemoCopySearchButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var step = DemoManager.GetCurrentStep();
        if (step != null && !string.IsNullOrEmpty(step.SearchTerm))
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(step.SearchTerm);
                UpdateDemoStatus($"Copied '{step.SearchTerm}' to clipboard");
            }
        }
    }

    // ----------------------------
    // DEMO MODE – View Artifacts
    // ----------------------------
    private void DemoViewArtifactsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DemoArtifactsPanel != null)
        {
            DemoArtifactsPanel.IsVisible = !DemoArtifactsPanel.IsVisible;
        }

        if (DemoViewArtifactsButton != null)
        {
            DemoViewArtifactsButton.Content = DemoArtifactsPanel?.IsVisible == true
                ? "Hide Artifacts"
                : "View Artifacts";
        }
    }

    // ----------------------------
    // DEMO MODE – Initialize on load
    // ----------------------------
    private void InitializeDemoMode()
    {
        DemoManager.Initialize();
        ResetDemoUI();

        if (DemoWalkthroughPanel != null)
            DemoWalkthroughPanel.IsVisible = false;

        if (DemoArtifactsPanel != null)
            DemoArtifactsPanel.IsVisible = false;

        if (DemoEndButton != null)
            DemoEndButton.IsEnabled = false;
    }
}
