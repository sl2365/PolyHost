#pragma once
#include <JuceHeader.h>

class PointerControl
{
public:
    struct JumpPoint
    {
        float x = 0.0f;
        float y = 0.0f;
    };

    void setTargetScreenBounds(juce::Rectangle<int> bounds);
    void clearTarget();

    bool hasTarget() const;

    void setSnapWeights(float newXWeight, float newYWeight);
    float getXSnapWeight() const;
    float getYSnapWeight() const;

    void setLaneTolerance(float newTolerance);
    float getLaneTolerance() const;

    void setJumpPoints(const juce::Array<JumpPoint>& newJumpPoints,
                       juce::Rectangle<int> sourceBounds);

    void panX(int midiValue);
    void panY(int midiValue);

    void wheelAdjust(int delta);
    void dragAdjust(int delta);

    juce::Point<int> getCurrentScreenPosition() const;
    juce::Rectangle<int> getTargetScreenBounds() const;

private:
    void moveCursorToCurrentPosition();
    void syncToPhysicalCursorPosition();
    void updateJumpSelection();
    int findNearestJumpPointInCurrentLane() const;
    bool hasJumpPoints() const;

    enum class LastMoveAxis
    {
        none,
        x,
        y
    };

    juce::Rectangle<int> targetScreenBounds;
    juce::Array<JumpPoint> jumpPoints;
    float virtualX = 0.0f;
    float virtualY = 0.0f;
    int selectedJumpPoint = -1;
    int currentX = 0;
    int currentY = 0;
    float xSnapWeight = 1.0f;
    float ySnapWeight = 0.20f;
    float laneLockStrength = 4.0f;
    float lockedLaneX = 0.0f;
    float lockedLaneY = 0.0f;
    bool hasLockedLaneX = false;
    bool hasLockedLaneY = false;
    float laneTolerance = 10.0f;
    LastMoveAxis lastMoveAxis = LastMoveAxis::none;
};
