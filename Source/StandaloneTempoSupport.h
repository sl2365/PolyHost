#pragma once
#include <JuceHeader.h>
#include "AudioEngine.h"

struct StandaloneTempoState
{
    double hostTempoBpm = 120.0;
    double defaultTempoBpm = 120.0;
    bool hostSyncEnabled = false;
    double externalHostTempoBpm = 120.0;
    bool externalHostTempoAvailable = false;
};

class StandaloneTempoSupport
{
public:
    class HostPlayHead final : public juce::AudioPlayHead
    {
    public:
        void prepareToPlay(double newSampleRate)
        {
            const juce::ScopedLock sl(lock);
            sampleRate = (newSampleRate > 0.0 ? newSampleRate : 44100.0);
            samplePosition = 0.0;
        }

        void setTempoBpm(double newTempoBpm)
        {
            const juce::ScopedLock sl(lock);
            tempoBpm = juce::jlimit(20.0, 300.0, newTempoBpm);
        }

        void advance(int numSamples)
        {
            const juce::ScopedLock sl(lock);
            samplePosition += juce::jmax(0, numSamples);
        }

        Optional<PositionInfo> getPosition() const override
        {
            const juce::ScopedLock sl(lock);

            PositionInfo info;
            info.setIsPlaying(true);
            info.setIsRecording(false);
            info.setBpm(tempoBpm);
            info.setTimeSignature(juce::AudioPlayHead::TimeSignature{ 4, 4 });
            info.setTimeInSamples((juce::int64) samplePosition);
            info.setTimeInSeconds(samplePosition / sampleRate);

            const auto ppq = (samplePosition / sampleRate) * (tempoBpm / 60.0);
            info.setPpqPosition(ppq);
            info.setPpqPositionOfLastBarStart(std::floor(ppq / 4.0) * 4.0);

            return info;
        }

    private:
        mutable juce::CriticalSection lock;
        double sampleRate = 44100.0;
        double samplePosition = 0.0;
        double tempoBpm = 120.0;
    };

    class PlaybackTracker final : public juce::AudioIODeviceCallback
    {
    public:
        explicit PlaybackTracker(HostPlayHead& playHeadIn) : playHead(playHeadIn) {}

        void setWrappedCallback(juce::AudioIODeviceCallback* callbackToWrap)
        {
            wrappedCallback = callbackToWrap;
        }

        void audioDeviceIOCallbackWithContext(const float* const* inputChannelData,
                                              int numInputChannels,
                                              float* const* outputChannelData,
                                              int numOutputChannels,
                                              int numSamples,
                                              const juce::AudioIODeviceCallbackContext& context) override
        {
            if (wrappedCallback != nullptr)
            {
                wrappedCallback->audioDeviceIOCallbackWithContext(inputChannelData,
                                                                  numInputChannels,
                                                                  outputChannelData,
                                                                  numOutputChannels,
                                                                  numSamples,
                                                                  context);
            }

            playHead.advance(numSamples);
        }

        void audioDeviceAboutToStart(juce::AudioIODevice* device) override
        {
            if (wrappedCallback != nullptr)
                wrappedCallback->audioDeviceAboutToStart(device);

            if (device != nullptr)
                playHead.prepareToPlay(device->getCurrentSampleRate());
        }

        void audioDeviceStopped() override
        {
            if (wrappedCallback != nullptr)
                wrappedCallback->audioDeviceStopped();
        }

    private:
        HostPlayHead& playHead;
        juce::AudioIODeviceCallback* wrappedCallback = nullptr;
    };

    class TempoStripComponent final : public juce::Component,
                                      private juce::TextEditor::Listener,
                                      private juce::Timer
    {
    public:
        explicit TempoStripComponent(StandaloneTempoSupport& ownerIn);

        void resized() override;

    private:
        class TempoTextEditor final : public juce::TextEditor
        {
        public:
            explicit TempoTextEditor(TempoStripComponent& ownerIn) : owner(ownerIn) {}

            void mouseWheelMove(const juce::MouseEvent& event,
                                const juce::MouseWheelDetails& wheel) override;

            void mouseDoubleClick(const juce::MouseEvent& event) override;
            bool keyPressed(const juce::KeyPress& key) override;
            void focusLost(FocusChangeType cause) override;

        private:
            TempoStripComponent& owner;
        };

        class BeatIndicatorComponent final : public juce::Component,
                                             private juce::Timer
        {
        public:
            explicit BeatIndicatorComponent(StandaloneTempoSupport& ownerIn);

            void paint(juce::Graphics& g) override;

        private:
            void timerCallback() override;

            StandaloneTempoSupport& owner;
        };

        void timerCallback() override;
        void textEditorReturnKeyPressed(juce::TextEditor& editor) override;
        void textEditorEscapeKeyPressed(juce::TextEditor& editor) override;
        void textEditorFocusLost(juce::TextEditor& editor) override;

        void setTempoBpm(double bpm);
        void registerTapTempo();
        void commitTempoFromEditor();
        void refreshUi();
        void updateTooltip();
        void updateControlState();

        StandaloneTempoSupport& owner;
        BeatIndicatorComponent beatIndicator;
        TempoTextEditor tempoEditor;
        juce::TextButton tapTempoButton { "Tap" };
    };

    explicit StandaloneTempoSupport(AudioEngine& audioEngineIn);

    const StandaloneTempoState& getState() const;

    void initialise(juce::AudioDeviceManager& deviceManager);
    void shutdown(juce::AudioDeviceManager& deviceManager);
    void setWrappedAudioCallback(juce::AudioIODeviceCallback* callback);
    std::unique_ptr<juce::Component> createTempoStripComponent();

    void setHostTempoBpm(double bpm);
    void setHostSyncEnabled(bool enabled);
    void registerTapTempo();
    void setDefaultTempoBpm(double bpm);

    double getHostTempoBpm() const;
    double getDefaultTempoBpm() const;
    bool isHostSyncEnabled() const;

    double getDisplayedTempoBpm() const;
    bool isTempoEditorInteractive() const;
    bool isExternalHostTempoAvailable() const;

    void setExternalHostTempoBpm(double bpm);
    void clearExternalHostTempo();

    bool refreshFromEngineIfNeeded();

    juce::AudioPlayHead* getPlayHead() { return &hostPlayHead; }

private:
    void pushEffectiveTempoToEngine();

    AudioEngine& audioEngine;
    StandaloneTempoState state;
    HostPlayHead hostPlayHead;
    PlaybackTracker playbackTracker { hostPlayHead };
    juce::Array<double> tapTimesMs;
    double lastDisplayedSyncedTempoBpm = -1.0;
    bool lastDisplayedHostTempoAvailable = false;
    bool trackerInitialised = false;
};