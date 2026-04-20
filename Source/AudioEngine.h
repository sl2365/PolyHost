#pragma once
#include <JuceHeader.h>

class AudioEngine : private juce::ChangeListener
{
public:
    AudioEngine();
    ~AudioEngine() override;
    void initialise(const juce::String& savedAudioDeviceState = {});
    juce::String createAudioDeviceStateXml() const;
    void shutdown();
    juce::AudioProcessorGraph::NodeID addPlugin(std::unique_ptr<juce::AudioPluginInstance> instance, bool isSynth);
    void removePlugin(juce::AudioProcessorGraph::NodeID nodeId);
    void setFxOrder(const juce::Array<juce::AudioProcessorGraph::NodeID>& orderedIds);
    juce::AudioDeviceManager& getDeviceManager() { return deviceManager; }
    juce::AudioProcessorGraph& getGraph() { return graph; }
    void sendMidiBuffer(const juce::MidiBuffer& buffer);
private:
    void rebuildConnections();
    void changeListenerCallback(juce::ChangeBroadcaster*) override {}
    juce::AudioDeviceManager  deviceManager;
    juce::AudioProcessorGraph graph;
    juce::AudioProcessorPlayer graphPlayer;
    juce::AudioProcessorGraph::Node::Ptr midiInNode;
    juce::AudioProcessorGraph::Node::Ptr audioOutNode;
    juce::Array<juce::AudioProcessorGraph::Node::Ptr> synthNodes;
    juce::Array<juce::AudioProcessorGraph::Node::Ptr> fxNodes;
    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(AudioEngine)
};
