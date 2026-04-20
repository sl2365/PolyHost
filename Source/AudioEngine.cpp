#include "AudioEngine.h"

AudioEngine::AudioEngine() : graph() {}
AudioEngine::~AudioEngine() { shutdown(); }

void AudioEngine::initialise(const juce::String& savedAudioDeviceState)
{
    juce::String err;

    if (savedAudioDeviceState.isNotEmpty())
    {
        std::unique_ptr<juce::XmlElement> xml(juce::parseXML(savedAudioDeviceState));
        if (xml)
            err = deviceManager.initialise(0, 2, xml.get(), true);
        else
            err = deviceManager.initialiseWithDefaultDevices(0, 2);
    }
    else
    {
        err = deviceManager.initialiseWithDefaultDevices(0, 2);
    }

    if (err.isNotEmpty())
    {
        DBG("AudioDeviceManager init error: " << err);
    }

    graph.enableAllBuses();

    midiInNode = graph.addNode(std::make_unique<juce::AudioProcessorGraph::AudioGraphIOProcessor>(
        juce::AudioProcessorGraph::AudioGraphIOProcessor::midiInputNode));

    audioOutNode = graph.addNode(std::make_unique<juce::AudioProcessorGraph::AudioGraphIOProcessor>(
        juce::AudioProcessorGraph::AudioGraphIOProcessor::audioOutputNode));

    graphPlayer.setProcessor(&graph);
    deviceManager.addAudioCallback(&graphPlayer);
    deviceManager.addMidiInputDeviceCallback({}, &graphPlayer);
}

juce::String AudioEngine::createAudioDeviceStateXml() const
{
    if (auto xml = deviceManager.createStateXml())
        return xml->toString();

    return {};
}

void AudioEngine::shutdown()
{
    deviceManager.removeMidiInputDeviceCallback({}, &graphPlayer);
    deviceManager.removeAudioCallback(&graphPlayer);
    graphPlayer.setProcessor(nullptr);
    graph.clear();
}

juce::AudioProcessorGraph::NodeID AudioEngine::addPlugin(std::unique_ptr<juce::AudioPluginInstance> instance, bool isSynth)
{
    if (instance == nullptr)
        return {};

    instance->enableAllBuses();
    auto node = graph.addNode(std::move(instance));

    if (node == nullptr)
        return {};

    if (isSynth)
        synthNodes.add(node);
    else
        fxNodes.add(node);

    rebuildConnections();
    return node->nodeID;
}

void AudioEngine::removePlugin(juce::AudioProcessorGraph::NodeID nodeId)
{
    for (int i = synthNodes.size(); --i >= 0;)
        if (synthNodes[i]->nodeID == nodeId)
            synthNodes.remove(i);

    for (int i = fxNodes.size(); --i >= 0;)
        if (fxNodes[i]->nodeID == nodeId)
            fxNodes.remove(i);

    graph.removeNode(nodeId);
    rebuildConnections();
}

void AudioEngine::setFxOrder(const juce::Array<juce::AudioProcessorGraph::NodeID>& orderedIds)
{
    juce::Array<juce::AudioProcessorGraph::Node::Ptr> reordered;

    for (auto id : orderedIds)
        if (auto* n = graph.getNodeForId(id))
            reordered.add(n);

    fxNodes = reordered;
    rebuildConnections();
}

void AudioEngine::sendMidiBuffer(const juce::MidiBuffer&) {}

void AudioEngine::rebuildConnections()
{
    graph.disconnectNode(midiInNode->nodeID);
    graph.disconnectNode(audioOutNode->nodeID);

    for (auto& n : synthNodes)
        graph.disconnectNode(n->nodeID);

    for (auto& n : fxNodes)
        graph.disconnectNode(n->nodeID);

    if (synthNodes.isEmpty())
        return;

    const int midiCh = juce::AudioProcessorGraph::midiChannelIndex;

    for (auto& sn : synthNodes)
        graph.addConnection({ { midiInNode->nodeID, midiCh }, { sn->nodeID, midiCh } });

    juce::Array<juce::AudioProcessorGraph::Node::Ptr> chain;

    for (auto& n : synthNodes)
        chain.add(n);

    for (auto& n : fxNodes)
        chain.add(n);

    chain.add(audioOutNode);

    for (int i = 0; i < chain.size() - 1; ++i)
    {
        graph.addConnection({ { chain[i]->nodeID, 0 }, { chain[i + 1]->nodeID, 0 } });
        graph.addConnection({ { chain[i]->nodeID, 1 }, { chain[i + 1]->nodeID, 1 } });
    }
}