#include <JuceHeader.h>
#include "MainComponent.h"

class PolyHostApplication final : public juce::JUCEApplication
{
public:
    PolyHostApplication() = default;
    const juce::String getApplicationName() override    { return "PolyHost"; }
    const juce::String getApplicationVersion() override { return "0.1.0"; }
    bool moreThanOneInstanceAllowed() override { return false; }

    void initialise(const juce::String& commandLine) override
    {
        mainWindow = std::make_unique<MainWindow>(getApplicationName(), commandLine);
    }

    void shutdown() override { mainWindow.reset(); }
    void systemRequestedQuit() override { quit(); }

    void anotherInstanceStarted(const juce::String& commandLine) override
    {
        if (mainWindow) mainWindow->handleCommandLine(commandLine);
    }

    class MainWindow final : public juce::DocumentWindow
    {
    public:
        MainWindow(const juce::String& name, const juce::String& commandLine)
            : DocumentWindow(name, juce::Colour(0xFF1A1A2E), DocumentWindow::allButtons)
        {
            setUsingNativeTitleBar(true);
            mainComponent = new MainComponent();
            setContentOwned(mainComponent, true);
            setResizable(true, true);
            setResizeLimits(900, 550, 3840, 2160);
            auto& s = mainComponent->getSettings();
            centreWithSize(s.getWindowWidth(), s.getWindowHeight());
            setVisible(true);
            if (commandLine.isNotEmpty()) handleCommandLine(commandLine);
        }

        ~MainWindow() override
        {
            if (mainComponent)
                mainComponent->getSettings().setWindowSize(getWidth(), getHeight());
        }

        void handleCommandLine(const juce::String& cmdLine)
        {
            juce::StringArray tokens;
            tokens.addTokens(cmdLine, true);
            for (auto& token : tokens)
            {
                juce::File f(token.unquoted());
                if (f.existsAsFile()) { if (mainComponent) mainComponent->loadPluginFromFile(f); break; }
            }
        }

        void closeButtonPressed() override
        {
            if (mainComponent == nullptr || mainComponent->requestQuit())
                juce::JUCEApplication::getInstance()->systemRequestedQuit();
        }
        
    private:
        MainComponent* mainComponent = nullptr;
        JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(MainWindow)
    };

private:
    std::unique_ptr<MainWindow> mainWindow;
};

START_JUCE_APPLICATION(PolyHostApplication)