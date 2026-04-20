#pragma once
#include <JuceHeader.h>

class MidiEngine : private juce::MidiInputCallback,
                   public juce::ChangeBroadcaster
{
public:
    explicit MidiEngine(juce::AudioDeviceManager& deviceManager);
    ~MidiEngine() override;

    juce::StringArray getAvailableDeviceNames() const;
    void openDevice(const juce::String& deviceIdentifier);
    void closeCurrentDevice();

    juce::String getCurrentDeviceName() const;
    juce::MidiMessage getLastMessage() const;

    juce::Array<juce::MidiMessage> popPendingMessages();

private:
    void handleIncomingMidiMessage(juce::MidiInput* source, const juce::MidiMessage& message) override;

    juce::AudioDeviceManager& deviceManager;
    std::unique_ptr<juce::MidiInput> midiInput;
    juce::MidiMessage lastMessage;
    juce::Array<juce::MidiMessage> pendingMessages;
    mutable juce::CriticalSection lock;

    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(MidiEngine)
};