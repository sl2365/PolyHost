#pragma once
#include <JuceHeader.h>

class UnsavedChangesHelper
{
public:
    enum class Decision
    {
        cancel,
        discard,
        save
    };

    Decision promptToSaveChanges() const
    {
        juce::AlertWindow alert("Unsaved Changes",
                                "Save changes to the current preset before continuing?",
                                juce::AlertWindow::WarningIcon);

        alert.addButton("Save", 1, juce::KeyPress(juce::KeyPress::returnKey));
        alert.addButton("Discard", 2);
        alert.addButton("Cancel", 0, juce::KeyPress(juce::KeyPress::escapeKey));

        auto result = alert.runModalLoop();

        if (result == 1)
            return Decision::save;

        if (result == 2)
            return Decision::discard;

        return Decision::cancel;
    }
};