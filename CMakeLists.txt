cmake_minimum_required(VERSION 3.22)
project(PolyHost VERSION 0.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ── Fetch JUCE ───────────────────────────────────────────────────────────────
include(FetchContent)

FetchContent_Declare(
    JUCE
    GIT_REPOSITORY https://github.com/juce-framework/JUCE.git
    GIT_TAG        7.0.12
)
FetchContent_MakeAvailable(JUCE)

# ── Fetch clap-juce-extensions (CLAP hosting support) ────────────────────────
FetchContent_Declare(
    clap_juce_extensions
    GIT_REPOSITORY https://github.com/free-audio/clap-juce-extensions.git
    GIT_TAG        main
)
FetchContent_MakeAvailable(clap_juce_extensions)

# ── Application target ────────────────────────────────────────────────────────
juce_add_gui_app(PolyHost
    PRODUCT_NAME        "PolyHost"
    VERSION             "0.1.0"
    COMPANY_NAME        "PolyHost Project"
    BUNDLE_ID           "com.polyhost.app"
)

juce_generate_juce_header(PolyHost)

target_sources(PolyHost PRIVATE
    Source/Main.cpp
    Source/MainComponent.cpp
    Source/PluginTabComponent.cpp
    Source/AudioEngine.cpp
    Source/MidiEngine.cpp
    Source/AppSettings.cpp
)

target_compile_definitions(PolyHost PRIVATE
    JUCE_WEB_BROWSER=0
    JUCE_USE_CURL=0
    JUCE_DISPLAY_SPLASH_SCREEN=0
    JUCE_MODAL_LOOPS_PERMITTED=1

    # VST3 x64 - ready out of the box
    JUCE_PLUGINHOST_VST3=1

    # VST2: Uncomment these two lines AND place the Steinberg VST2 SDK at the
    # path below (vstsdk2.4 folder containing "pluginterfaces/vst2.x/aeffect.h")
    # JUCE_PLUGINHOST_VST=1
    # VST2_SDK_ROOT="C:/SDKs/vstsdk2.4"

    # CLAP - via clap-juce-extensions
    JUCE_PLUGINHOST_CLAP=1
)

target_link_libraries(PolyHost
    PRIVATE
        juce::juce_audio_basics
        juce::juce_audio_devices
        juce::juce_audio_formats
        juce::juce_audio_processors
        juce::juce_audio_utils
        juce::juce_core
        juce::juce_data_structures
        juce::juce_events
        juce::juce_graphics
        juce::juce_gui_basics
        juce::juce_gui_extra
        clap_juce_extensions
    PUBLIC
        juce::juce_recommended_config_flags
        juce::juce_recommended_warning_flags
)
