# Agent Zero - Rovix AI Unity SDK

Agent Zero is a powerful Unity package designed to bridge Unity games with the Rovix AI AgentOne system. It provides automated frame-by-frame event reporting, state synchronization, and a robust interface for AI-driven automation using AltTester.

## Key Features

- **Automated Bootstrapping**: Automatically injects and initializes the AI controller into any scene without manual setup.
- **Persistence**: The AI controller persists across scene loads, ensuring continuous communication.
- **Fixed-Step Reporting**: Sends game state events to a Python AI server at configurable intervals.
- **AltTester Integration**: Seamlessly integrates with AltTester for precise game object manipulation and action execution.
- **Testing Framework**: Includes built-in support for testing and validation within the Unity Editor.

## Installation

To add Agent Zero to your Unity project:

1. Open the **Package Manager** (`Window > Package Manager`).
2. Click the `+` icon and select **Add package from git URL...** or use the local path if provided.
3. Enter the repository URL or browse to the local package folder.

## Core Components

### FrameController
The `frameController` is the heart of the SDK. It tracks the game's execution steps and periodically pulses state information to the AI server.

- **URL Configuration**: Default points to `http://localhost:8000`. This can be modified in `frameController.cs`.
- **Event Interval**: Defaults to 600 FixedUpdate steps.
- **Synchronization**: Uses a "wait-for-action" flow to ensure the AI has processed previous events before sending new ones.

### FrameControllerBootstrap
A static initializer that ensures a `FrameController` instance exists as soon as the game starts (`BeforeSceneLoad`). It handles the creation of the persistent `FrameController` GameObject.

### SDKValidator
An Editor-side utility that confirms the SDK is correctly loaded when the Unity Editor starts.

## API & Usage

### Marking Actions as Executed
The Python server communicates back to Unity via AltTester. Once the AI has decided on an action, it should call:
```csharp
frameController.MarkActionsExecuted();
```
This tells the SDK that it's safe to resume step counting and send the next state update.

### Getting Current State
- `GetCurrentStep()`: Returns the number of FixedUpdate steps since the game started.
- `GetCurrentFrame()`: Returns the current Unity frame count.

## Configuration

You can adjust the behavior of the SDK by interacting with the `frameController` instance:

```csharp
var controller = FindObjectOfType<frameController>();
controller.SetEventInterval(300); // Send updates more frequently
```

## Support

For issues or feature requests, contact [Rovix Digital Support](mailto:support@rovix.digital) or visit [rovix.digital](http://www.rovix.digital).
