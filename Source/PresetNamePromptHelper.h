#pragma once
#include <JuceHeader.h>

class PresetNamePromptHelper
{
public:
    juce::String promptForPresetName(const juce::String& initialName) const
    {
        juce::AlertWindow alert("Save Preset As",
                                "Enter a preset name:",
                                juce::AlertWindow::NoIcon);

        alert.addTextEditor("name", initialName, "Preset Name:");
        alert.addButton("Save", 1, juce::KeyPress(juce::KeyPress::returnKey));
        alert.addButton("Cancel", 0, juce::KeyPress(juce::KeyPress::escapeKey));

        if (alert.runModalLoop() != 1)
            return {};

        return alert.getTextEditorContents("name").trim();
    }
};