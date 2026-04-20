#include "MainComponent.h"

MainComponent::MainComponent()
{
    setSize(settings.getWindowWidth(), settings.getWindowHeight());
    audioEngine.initialise(settings.getAudioDeviceState());
    audioEngine.getDeviceManager().addChangeListener(this);
    addAndMakeVisible(menuBar);
    tabs.setColour(juce::TabbedComponent::backgroundColourId, juce::Colour(0xFF16213E));
    tabs.setTabBarDepth(34);
    tabs.getTabbedButtonBar().setMinimumTabScaleFactor(0.7f);
    addAndMakeVisible(tabs);
    addEmptyTab();
    addEmptyTab();

    auto savedMidiDevice = settings.getMidiDeviceName();
    if (savedMidiDevice.isNotEmpty())
    {
        midiEngine.openDevice(savedMidiDevice);
        statusBar.setText("PolyHost 0.1  |  MIDI: " + midiEngine.getCurrentDeviceName() + "  |  Ready",
                          juce::dontSendNotification);
    }
    else
    {
        statusBar.setText("PolyHost 0.1  |  No MIDI device selected  |  Ready",
                          juce::dontSendNotification);
    }
    statusBar.setColour(juce::Label::backgroundColourId, juce::Colour(0xFF0F3460));
    statusBar.setColour(juce::Label::textColourId, juce::Colours::lightgrey);
    statusBar.setJustificationType(juce::Justification::centredLeft);
    addAndMakeVisible(statusBar);
    markSessionClean();
    updateWindowTitle();
    startTimer(750);
}

MainComponent::~MainComponent()
{
    stopTimer();
    audioEngine.getDeviceManager().removeChangeListener(this);
    settings.setAudioDeviceState(audioEngine.createAudioDeviceStateXml());
    audioEngine.shutdown();
}

juce::String MainComponent::createSessionSignature() const
{
    juce::MemoryOutputStream mo;

    mo.writeInt(tabs.getNumTabs());
    mo.writeInt(tabs.getCurrentTabIndex());

    if (auto* top = findParentComponentOfClass<juce::DocumentWindow>())
    {
        mo.writeInt(top->getWidth());
        mo.writeInt(top->getHeight());
    }
    else
    {
        mo.writeInt(getWidth());
        mo.writeInt(getHeight());
    }

    for (int i = 0; i < tabs.getNumTabs(); ++i)
    {
        if (auto* tc = getTabComponent(i))
        {
            mo.writeInt((int) tc->getType());
            mo.writeString(tc->getPluginFile().getFullPathName());

            auto state = tc->getPluginState();
            mo.writeInt((int) state.getSize());
            mo.write(state.getData(), state.getSize());
        }
    }

    return mo.toUTF8();
}

void MainComponent::timerCallback()
{
    auto currentSignature = createSessionSignature();

    if (lastCleanSessionSignature.isNotEmpty() && currentSignature != lastCleanSessionSignature)
    {
        if (!isSessionDirty)
            markSessionDirty();
    }
}

void MainComponent::changeListenerCallback(juce::ChangeBroadcaster* source)
{
    if (source == &audioEngine.getDeviceManager())
    {
        settings.setAudioDeviceState(audioEngine.createAudioDeviceStateXml());
        return;
    }

    for (int i = 0; i < tabs.getNumTabs(); ++i)
    {
        if (auto* tc = getTabComponent(i))
        {
            if (source == tc)
            {
                refreshTabAppearance(i);
                markSessionDirty();
                return;
            }
        }
    }
}

void MainComponent::updateWindowTitle()
{
    juce::String title = "PolyHost";

    if (currentPresetFile.existsAsFile())
        title += " - " + currentPresetFile.getFileNameWithoutExtension();
    else
        title += " - Untitled";

    if (isSessionDirty)
        title += " *";

    if (auto* top = findParentComponentOfClass<juce::DocumentWindow>())
        top->setName(title);
}

void MainComponent::loadPluginFromFile(const juce::File& file)
{
    for (int i = 0; i < tabs.getNumTabs(); ++i)
    {
        if (auto* tc = getTabComponent(i))
        {
            if (!tc->hasPlugin())
            {
                if (tc->loadPlugin(file))
                {
                    tabs.setCurrentTabIndex(i);
                }
                return;
            }
        }
    }

    addEmptyTab();

    if (auto* tc = getTabComponent(tabs.getNumTabs() - 1))
    {
        if (tc->loadPlugin(file))
        {
            tabs.setCurrentTabIndex(tabs.getNumTabs() - 1);
            refreshTabAppearance(tabs.getNumTabs() - 1);
            markSessionDirty();
        }
    }
}

void MainComponent::addEmptyTab()
{
    auto* tc = new PluginTabComponent(audioEngine, tabs.getNumTabs());
    tc->addChangeListener(this);
    tabs.addTab("Empty", PluginTabComponent::colourForType(PluginTabComponent::SlotType::Empty), tc, true);
    markSessionDirty();
}

void MainComponent::refreshTabAppearance(int index)
{
    if (auto* tc = getTabComponent(index))
    {
        juce::String name = "Empty";

        if (tc->hasPlugin())
            name = tc->getPluginName();

        tabs.setTabName(index, name);
        tabs.setTabBackgroundColour(index, PluginTabComponent::colourForType(tc->getType()));
    }
}

int MainComponent::countTabsOfType(PluginTabComponent::SlotType type) const
{
    int count = 0;
    for (int i = 0; i < tabs.getNumTabs(); ++i)
        if (auto* tc = getTabComponent(i)) if (tc->getType() == type) ++count;
    return count;
}

PluginTabComponent* MainComponent::getTabComponent(int index) const
{
    return dynamic_cast<PluginTabComponent*>(tabs.getTabContentComponent(index));
}

void MainComponent::paint(juce::Graphics& g) { g.fillAll(juce::Colour(0xFF1A1A2E)); }

void MainComponent::resized()
{
    auto area = getLocalBounds();
    menuBar.setBounds(area.removeFromTop(juce::LookAndFeel::getDefaultLookAndFeel().getDefaultMenuBarHeight()));
    statusBar.setBounds(area.removeFromBottom(24));
    tabs.setBounds(area);
}

juce::StringArray MainComponent::getMenuBarNames() { return { "File", "Audio", "MIDI", "Recording", "Options", "Help" }; }

juce::PopupMenu MainComponent::getMenuForIndex(int index, const juce::String&)
{
    juce::PopupMenu menu;
    switch (index)
    {
        case 0:
            menu.addItem(100, "New Preset");
            menu.addSeparator();
            menu.addItem(101, "Add Tab");
            menu.addSeparator();
            menu.addItem(103, "Save Preset");
            menu.addItem(104, "Save Preset As...");
            menu.addItem(105, "Load Preset...");
            menu.addItem(106, "Delete Preset...");
            menu.addSeparator();
            menu.addItem(199, "Quit");
            break;
        case 1: menu.addItem(201, "Audio Device Settings"); break;
        case 2: menu.addItem(301, "Select MIDI Input Device"); menu.addItem(302, "MIDI Monitor"); break;
        case 3: menu.addItem(401, "Record Audio...  [TODO]"); menu.addItem(402, "Record MIDI...  [TODO]"); break;
        case 4: menu.addItem(501, "Preferences...  [TODO]"); break;
        case 5: menu.addItem(601, "About PolyHost"); break;
        default: break;
    }
    return menu;
}

void MainComponent::menuItemSelected(int itemId, int)
{
    switch (itemId)
    {
        case 100: newPreset(); break;
        case 101: addEmptyTab(); break;
        case 103: savePreset(); break;
        case 104: savePresetAs(); break;
        case 105: loadPreset(); break;
        case 106: deletePreset(); break;
        case 199:
            if (requestQuit())
                juce::JUCEApplication::getInstance()->systemRequestedQuit();
            break;
        case 201:
        {
            auto* selector = new juce::AudioDeviceSelectorComponent(
                audioEngine.getDeviceManager(),
                0, 2,
                0, 2,
                true,
                true,
                true,
                false);

            juce::DialogWindow::LaunchOptions o;
            o.content.setOwned(selector);
            o.dialogTitle                  = "Audio Device Settings";
            o.dialogBackgroundColour       = juce::Colours::darkgrey;
            o.escapeKeyTriggersCloseButton = true;
            o.useNativeTitleBar            = true;
            o.resizable                    = true;

            auto* w = o.launchAsync();
            w->centreWithSize(500, 400);
            break;
        }
        case 301:
        {
            juce::PopupMenu midiMenu;
            auto names = midiEngine.getAvailableDeviceNames();

            if (names.isEmpty())
            {
                juce::AlertWindow::showMessageBoxAsync(
                    juce::AlertWindow::WarningIcon,
                    "MIDI Input",
                    "No MIDI input devices found.");
                break;
            }

            for (int i = 0; i < names.size(); ++i)
                midiMenu.addItem(1000 + i, names[i]);

            midiMenu.showMenuAsync(
                juce::PopupMenu::Options(),
                [this, names](int result)
                {
                    if (result >= 1000 && result < 1000 + names.size())
                    {
                        auto selectedName = names[result - 1000];
                        midiEngine.openDevice(selectedName);
                        settings.setMidiDeviceName(selectedName);
                        statusBar.setText(
                            "PolyHost 0.1  |  MIDI: " + midiEngine.getCurrentDeviceName() + "  |  Ready",
                            juce::dontSendNotification);
                    }
                });

            break;
        }
        case 302:
        {
            if (midiMonitorWindow == nullptr)
                midiMonitorWindow = std::make_unique<MidiMonitorWindow>(midiEngine);

            midiMonitorWindow->setVisible(true);
            midiMonitorWindow->toFront(true);
            break;
        }
        case 601:
            juce::NativeMessageBox::showMessageBoxAsync(juce::AlertWindow::InfoIcon, "About PolyHost",
                "PolyHost v0.1\n\nA lightweight tabbed plugin host for VST3 and CLAP.\nBuilt with JUCE.\n\nVST2 and 32-bit bridging planned.");
            break;
        default: break;
    }
}

void MainComponent::clearAllPlugins()
{
    for (int i = 0; i < tabs.getNumTabs(); ++i)
    if (auto* tc = getTabComponent(i))
        tc->removeChangeListener(this);

    tabs.clearTabs();
    markSessionDirty();
}

void MainComponent::deletePreset()
{
    juce::FileChooser chooser("Delete Preset",
                              AppSettings::getPresetsDirectory(),
                              "*.xml");

    if (!chooser.browseForFileToOpen())
        return;

    auto file = chooser.getResult();

    if (juce::AlertWindow::showOkCancelBox(juce::AlertWindow::WarningIcon,
                                           "Delete Preset",
                                           "Delete preset:\n" + file.getFileName() + "?"))
    {
        if (file.deleteFile() && file == currentPresetFile)
        {
            currentPresetFile = {};
            updateWindowTitle();
        }
    }
}

void MainComponent::loadPreset()
{
    if (!maybeSaveChanges())
        return;

    juce::FileChooser chooser("Load Preset",
                              AppSettings::getPresetsDirectory(),
                              "*.xml");

    if (!chooser.browseForFileToOpen())
        return;

    auto file = chooser.getResult();
    auto xml = juce::XmlDocument::parse(file);

    if (xml == nullptr || !xml->hasTagName("PolyHostPreset"))
        return;

    juce::StringArray loadErrors;

    rebuildTabsFromPresetXml(*xml);

    int tabIndex = 0;
    for (auto* tabXml : xml->getChildIterator())
    {
        if (!tabXml->hasTagName("Tab"))
            continue;

        if (!juce::isPositiveAndBelow(tabIndex, tabs.getNumTabs()))
            break;

        if (auto* tc = getTabComponent(tabIndex))
        {
            auto pluginName               = tabXml->getStringAttribute("pluginName", "Unknown Plugin");
            auto pluginPath               = tabXml->getStringAttribute("pluginPath");
            auto pluginPathRelative       = tabXml->getStringAttribute("pluginPathRelative");
            auto pluginPathDriveFlexible  = tabXml->getStringAttribute("pluginPathDriveFlexible");
            auto stateBase64              = tabXml->getStringAttribute("pluginState");

            if (pluginPath.isNotEmpty() || pluginPathRelative.isNotEmpty() || pluginPathDriveFlexible.isNotEmpty())
            {
                auto pluginFile = AppSettings::resolvePluginPath(pluginPath,
                                                                 pluginPathRelative,
                                                                 pluginPathDriveFlexible);

                if (pluginFile == juce::File())
                {
                    juce::String displayPath = pluginPathRelative.isNotEmpty() ? pluginPathRelative : pluginPath;
                    if (displayPath.isEmpty())
                        displayPath = pluginPathDriveFlexible;

                    loadErrors.add("Missing plugin: " + pluginName + "\n  Path: " + displayPath);
                }
                else if (!tc->loadPlugin(pluginFile))
                {
                    loadErrors.add("Failed to load plugin: " + pluginName + "\n  Path: " + pluginFile.getFullPathName());
                }
                else
                {
                    juce::MemoryBlock state;
                    if (state.fromBase64Encoding(stateBase64))
                        tc->restorePluginState(state);

                    refreshTabAppearance(tabIndex);
                }
            }
        }

        ++tabIndex;
    }

    for (int i = 0; i < tabs.getNumTabs(); ++i)
        refreshTabAppearance(i);

    auto selectedTab = xml->getIntAttribute("selectedTab", 0);
    if (juce::isPositiveAndBelow(selectedTab, tabs.getNumTabs()))
        tabs.setCurrentTabIndex(selectedTab);

    if (auto* top = findParentComponentOfClass<juce::DocumentWindow>())
    {
        auto w = xml->getIntAttribute("windowWidth", top->getWidth());
        auto h = xml->getIntAttribute("windowHeight", top->getHeight());
        top->setSize(juce::jmax(900, w), juce::jmax(550, h));
    }

    currentPresetFile = file;
    markSessionClean();
    updateWindowTitle();
    showPresetLoadErrors(loadErrors);
}

void MainComponent::showPresetLoadErrors(const juce::StringArray& errors)
{
    if (errors.isEmpty())
        return;

    juce::String message = "Some plugins could not be restored:\n\n";

    for (int i = 0; i < errors.size(); ++i)
    {
        message += errors[i];

        if (i < errors.size() - 1)
            message += "\n\n";
    }

    juce::AlertWindow::showMessageBoxAsync(
        juce::AlertWindow::WarningIcon,
        "Preset Load Warnings",
        message);
}

void MainComponent::markSessionDirty()
{
    isSessionDirty = true;
    updateWindowTitle();
}

void MainComponent::markSessionClean()
{
    isSessionDirty = false;
    lastCleanSessionSignature = createSessionSignature();
    updateWindowTitle();
}

void MainComponent::newPreset()
{
    if (!maybeSaveChanges())
        return;

    for (int i = 0; i < tabs.getNumTabs(); ++i)
        if (auto* tc = getTabComponent(i))
            tc->removeChangeListener(this);

    tabs.clearTabs();

    addEmptyTab();
    addEmptyTab();

    currentPresetFile = {};
    markSessionClean();
    updateWindowTitle();
}

bool MainComponent::requestQuit()
{
    return maybeSaveChanges();
}

bool MainComponent::maybeSaveChanges()
{
    if (!isSessionDirty)
        return true;

    auto result = juce::NativeMessageBox::showYesNoCancelBox(
        juce::AlertWindow::WarningIcon,
        "Unsaved Changes",
        "Save changes to the current preset before continuing?");

    if (result == 0) // cancel
        return false;

    if (result == 1) // yes
    {
        if (currentPresetFile == juce::File())
            savePresetAs();
        else
            savePreset();

        return !isSessionDirty;
    }

    if (result == 2) // no
        return true;

    return true;
}

void MainComponent::savePreset()
{
    if (currentPresetFile == juce::File())
    {
        savePresetAs();
        return;
    }

    if (writePresetToFile(currentPresetFile))
    {
        markSessionClean();
        updateWindowTitle();
    }
}

void MainComponent::savePresetAs()
{
    juce::AlertWindow w("Save Preset As", "Enter a preset name:", juce::AlertWindow::NoIcon);
    w.addTextEditor("name",
                    currentPresetFile.existsAsFile() ? currentPresetFile.getFileNameWithoutExtension() : "",
                    "Preset Name:");
    w.addButton("Save", 1);
    w.addButton("Cancel", 0);

    if (w.runModalLoop() != 1)
        return;

    auto presetName = w.getTextEditorContents("name").trim();
    if (presetName.isEmpty())
        return;

    auto file = AppSettings::getPresetsDirectory().getChildFile(presetName + ".xml");

    if (writePresetToFile(file))
    {
        currentPresetFile = file;
        markSessionClean();
        updateWindowTitle();
    }
}

bool MainComponent::writePresetToFile(const juce::File& file)
{
    juce::XmlElement presetXml("PolyHostPreset");
    presetXml.setAttribute("name", file.getFileNameWithoutExtension());
    presetXml.setAttribute("selectedTab", tabs.getCurrentTabIndex());

    if (auto* top = findParentComponentOfClass<juce::DocumentWindow>())
    {
        presetXml.setAttribute("windowWidth", top->getWidth());
        presetXml.setAttribute("windowHeight", top->getHeight());
    }
    else
    {
        presetXml.setAttribute("windowWidth", getWidth());
        presetXml.setAttribute("windowHeight", getHeight());
    }

    for (int i = 0; i < tabs.getNumTabs(); ++i)
    {
        if (auto* tc = getTabComponent(i))
        {
            auto tabXml = std::make_unique<juce::XmlElement>("Tab");
            tabXml->setAttribute("index", i);
            tabXml->setAttribute("type", tc->getType() == PluginTabComponent::SlotType::Synth ? "Synth" : "FX");
            tabXml->setAttribute("tabName", tabs.getTabNames()[i]);

            if (tc->hasPlugin())
            {
                auto pluginFile = tc->getPluginFile();

                tabXml->setAttribute("pluginName", tc->getPluginName());
                tabXml->setAttribute("pluginPath", pluginFile.getFullPathName());

                auto relativePath = AppSettings::makePathPortable(pluginFile);
                if (relativePath.isNotEmpty())
                    tabXml->setAttribute("pluginPathRelative", relativePath);

                auto driveFlexiblePath = AppSettings::getDriveFlexiblePath(pluginFile);
                if (driveFlexiblePath.isNotEmpty())
                    tabXml->setAttribute("pluginPathDriveFlexible", driveFlexiblePath);

                auto state = tc->getPluginState();
                tabXml->setAttribute("pluginState", state.toBase64Encoding());
            }

            presetXml.addChildElement(tabXml.release());
        }
    }

    return presetXml.writeTo(file, {});
}

void MainComponent::rebuildTabsFromPresetXml(const juce::XmlElement& presetXml)
{
    for (int i = 0; i < tabs.getNumTabs(); ++i)
    if (auto* tc = getTabComponent(i))
        tc->removeChangeListener(this);

    tabs.clearTabs();

    for (auto* tabXml : presetXml.getChildIterator())
    {
        if (!tabXml->hasTagName("Tab"))
            continue;

        auto* tc = new PluginTabComponent(audioEngine, tabs.getNumTabs());
        tc->addChangeListener(this);
        tabs.addTab("Empty",
                    PluginTabComponent::colourForType(PluginTabComponent::SlotType::Empty),
                    tc,
                    true);
    }

    if (tabs.getNumTabs() == 0)
        addEmptyTab();
}