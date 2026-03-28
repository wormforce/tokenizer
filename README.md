# Tokenizer

Tokenizer is a Windows desktop app for measuring **human token production**.

In the AI era, we spend a lot of time counting how many tokens models consume. Tokenizer flips the question:

> How many tokens can a human actually produce in a day?

Not model tokens. Not benchmark tokens. Not synthetic output.
Actual human typing activity, captured locally on your machine, aggregated over time, and visualized as a personal production dashboard.

## Why

Large language models can generate and consume millions of tokens with almost no friction.

Humans cannot.

Our attention, typing speed, work context, and time are all hard limits. Tokenizer is built around that contrast. It helps you inspect your real daily throughput:

- How much text did you produce today?
- When were you actually active?
- Which app got most of your writing time?
- What does your production curve look like over a day?

Tokenizer is not trying to replace exact model tokenization for every prompt format. Its purpose is to give you a practical, local, human-centered view of output volume and writing behavior.

## What Tokenizer Measures

Tokenizer currently tracks **countable visible typing activity** on Windows and turns it into local, time-bucketed statistics.

It focuses on:

- Total typed character volume per day
- Real-time typing speed
- Per-minute activity buckets
- App attribution
- Daily summaries
- Historical trends

The app stores **aggregated statistics**, not raw text.

That means:

- It does **not** save what you typed
- It does **not** save window titles as content history
- It does **not** keep raw keystroke logs

Instead, it stores local statistical buckets such as:

- bucket start time
- app id
- character count
- average cps
- peak cps
- active seconds

## Core Features

- **Today dashboard**
  - total chars
  - current minute
  - peak minute
  - top app
  - time-bucketed activity chart
  - app ranking

- **History view**
  - recent 14-day trend
  - selected day activity chart
  - app ranking for the selected date

- **Chart controls**
  - time bucket options: `1 min`, `15 min`, `30 min`, `1 hour`
  - time range options:
    - `Data only`
    - `0-24 h`

- **Floating ball**
  - always-on-top overlay
  - real-time typing indicator
  - draggable edge docking
  - pause/resume actions

- **System integration**
  - tray support
  - autostart option
  - minimized startup
  - local-only SQLite storage

## How It Works

Tokenizer uses a Windows low-level keyboard hook to observe countable key activity, then pushes the events through a local aggregation pipeline.

High level flow:

1. Capture keyboard activity
2. Filter out non-countable keys
3. Detect foreground app
4. Aggregate into minute buckets
5. Persist into SQLite
6. Build charts, rankings, and daily summaries from those aggregates

The project intentionally separates those concerns into layers.

## Project Structure

```text
Tokenizer.sln
├─ Tokenizer.App
│  ├─ Views
│  ├─ ViewModels
│  ├─ Services
│  └─ Diagnostics
├─ Tokenizer.Core
│  ├─ Interfaces
│  ├─ Models
│  └─ Statistics
├─ Tokenizer.Infrastructure
│  ├─ Storage
│  ├─ InputHook
│  ├─ Tray
│  ├─ Windows
│  └─ Autostart
└─ Tokenizer.Tests
```

### `Tokenizer.App`

WinUI 3 UI layer.

Contains:

- pages
- shell window
- floating ball window
- view models
- app services

### `Tokenizer.Core`

Framework-agnostic business logic.

Contains:

- interfaces
- data models
- typing statistics logic
- bucket aggregation
- visible key classification

### `Tokenizer.Infrastructure`

Platform and persistence layer.

Contains:

- SQLite repositories
- keyboard hook implementation
- foreground app detection
- tray integration
- Windows interop
- autostart integration

### `Tokenizer.Tests`

Unit and repository tests.

## Tech Stack

- **.NET 8**
- **WinUI 3**
- **Windows App SDK**
- **CommunityToolkit.Mvvm**
- **Microsoft.Extensions.Hosting / DependencyInjection**
- **SQLite** via `Microsoft.Data.Sqlite`
- **LiveChartsCore + SkiaSharpView.WinUI**
- **Win32 interop / PInvoke**
- **xUnit**

## Running Locally

Requirements:

- Windows
- .NET 8 SDK
- Windows App SDK prerequisites

Build and run:

```powershell
.\run.ps1 -Launch
```

Build only:

```powershell
.\run.ps1
```

Optional flags:

```powershell
.\run.ps1 -SkipTests
.\run.ps1 -SkipRestore
.\run.ps1 -Configuration Release
```

## Data and Privacy

Tokenizer is designed to be **local-first**.

Current storage:

- SQLite database in `%LOCALAPPDATA%\Tokenizer`
- local diagnostic logs in `%LOCALAPPDATA%\Tokenizer\logs`

What is stored:

- aggregated typing stats
- app attribution
- daily summaries
- app settings

What is not stored:

- raw typed text
- full key-by-key text history

## Human Tokens vs Model Tokens

Tokenizer uses the word **token** conceptually.

Today, the app tracks human production through **typed character volume and typing activity** rather than exact LLM tokenizer output for every provider or model family.

That is intentional:

- it is local
- it is fast
- it is stable
- it maps to real human writing effort

You can think of Tokenizer as a **human throughput instrument**:

- models measure token consumption
- Tokenizer measures human production

Future versions may add model-specific token estimators, but the current focus is deliberately simpler and more grounded in actual typing behavior.

## Screenshot

Floating indicator example:

![Floating ball](floating_capture.png)

## Current Status

Tokenizer is an active local desktop project and already includes:

- real-time tracking
- local persistence
- charting
- tray integration
- floating overlay
- chart bucketing controls

It is still evolving, especially around:

- chart interactions
- token estimation models
- documentation polish
- packaging and distribution

## Roadmap Ideas

- exact tokenizer estimation modes for specific LLMs
- export daily summaries
- richer history filtering
- weekly / monthly views
- writing session detection
- per-app productivity comparisons
- configurable counting rules

## Philosophy

AI systems can burn through tokens at industrial scale.

Humans still create under biological, cognitive, and temporal limits.

Tokenizer exists to make those limits visible.

If model usage dashboards answer:

> how much compute did we consume?

Tokenizer tries to answer:

> how much did a person actually produce?

