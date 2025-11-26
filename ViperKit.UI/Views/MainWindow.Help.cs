// ViperKit.UI - Views\MainWindow.Help.cs
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ViperKit.UI.Views;

public partial class MainWindow
{
    /// <summary>
    /// Handle search box text changes - filter help content.
    /// </summary>
    private void HelpSearchBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        try
        {
            if (HelpSearchBox == null || HelpContentPanel == null)
                return;

            string searchText = HelpSearchBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;

            // If search is empty, show all sections
            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var child in HelpContentPanel.Children)
                {
                    if (child is Border border)
                        border.IsVisible = true;
                }
                return;
            }

            // Filter sections based on search text
            foreach (var child in HelpContentPanel.Children)
            {
                if (child is Border border)
                {
                    // Check if this section contains the search text
                    bool matches = ContainsSearchText(border, searchText);
                    border.IsVisible = matches;

                    // If it's an Expander, expand it when it matches
                    if (matches && border.Child is Expander expander)
                    {
                        expander.IsExpanded = true;
                    }
                }
            }
        }
        catch
        {
            // Don't crash on search errors
        }
    }

    /// <summary>
    /// Recursively search for text in a control tree.
    /// </summary>
    private bool ContainsSearchText(Control control, string searchText)
    {
        // Check TextBlock content
        if (control is TextBlock textBlock)
        {
            string text = textBlock.Text?.ToLowerInvariant() ?? string.Empty;
            if (text.Contains(searchText))
                return true;
        }

        // Check Expander header
        if (control is Expander expander)
        {
            string header = expander.Header?.ToString()?.ToLowerInvariant() ?? string.Empty;
            if (header.Contains(searchText))
                return true;
        }

        // Recursively check children
        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl && ContainsSearchText(childControl, searchText))
                    return true;
            }
        }

        // Check Expander content
        if (control is Expander exp && exp.Content is Control expContent)
        {
            return ContainsSearchText(expContent, searchText);
        }

        // Check Border child
        if (control is Border border && border.Child is Control borderChild)
        {
            return ContainsSearchText(borderChild, searchText);
        }

        return false;
    }
}
