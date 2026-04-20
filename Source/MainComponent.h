#pragma once
#include <JuceHeader.h>
#include "AudioEngine.h"
#include "MidiEngine.h"
#include "PluginTabComponent.h"
#include "AppSettings.h"
#include "MidiMonitorWindow.h"

class MainComponent final : public juce::Component,
                             public juce::MenuBarModel,
                             private juce::ChangeListener,
                             private juce::Timer
{
public:
    static constexpr int kMaxSynthTabs = 2;
    static constexpr int kMaxFxTabs    = 8;

    MainComponent();
    ~MainComponent() override;

    void loadPluginFromFile(const juce::File& file);
    void resized() override;
    void paint(juce::Graphics& g) override;
    bool requestQuit();

    juce::StringArray getMenuBarNames() override;
    juce::PopupMenu   getMenuForIndex(int index, const juce::String& name) override;
    void              menuItemSelected(int itemId, int menuIndex) override;

    AppSettings& getSettings() { return settings; }

private:
    void addEmptyTab();
    void refreshTabAppearance(int tabIndex);
    int  countTabsOfType(PluginTabComponent::SlotType type) const;
    PluginTabComponent* getTabComponent(int tabIndex) const;
    void changeListenerCallback(juce::ChangeBroadcaster* source) override;

    void newPreset();
    void savePreset();
    void savePresetAs();
    void loadPreset();
    void deletePreset();
    void clearAllPlugins();
    bool writePresetToFile(const juce::File& file);
    void updateWindowTitle();
    void rebuildTabsFromPresetXml(const juce::XmlElement& presetXml);
    bool maybeSaveChanges();
    void markSessionDirty();
    void markSessionClean();
    void showPresetLoadErrors(const juce::StringArray& errors);
    void timerCallback() override;
    juce::String createSessionSignature() const;

    AppSettings   settings;
    AudioEngine   audioEngine;
    MidiEngine    midiEngine { audioEngine.getDeviceManager() };

    juce::MenuBarComponent menuBar { this };
    juce::TabbedComponent  tabs    { juce::TabbedButtonBar::TabsAtTop };
    juce::Label            statusBar;
    std::unique_ptr<MidiMonitorWindow> midiMonitorWindow;
    juce::File currentPresetFile;
    bool isSessionDirty = false;
    juce::String lastCleanSessionSignature;

    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(MainComponent)
};
