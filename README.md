# Loggy

Loggy is a Blazor web application for uploading, exploring, and AI-analyzing structured log files. Upload a newline-delimited JSON log file, group events by any field, and run a Gemini-powered analysis to surface patterns, anomalies, and actionable recommendations.

---

## Features

- **Log upload** — upload newline-delimited JSON (NDJSON) log files up to 10 MB
- **Tree-based parsing** — each log event is parsed into a `TreeNode<string>` tree, preserving nested JSON structure
- **Group by any field** — select any top-level key to group and browse log events in an accordion view
- **AI analysis** — send logs to Google Gemini 2.5 Flash for a structured analysis including a summary, error counts, detected patterns, severity badges, and per-pattern recommendations
- **Pattern highlighting** — log entries linked to a detected pattern are highlighted with the appropriate severity color

---

## Architecture

Loggy is a .NET Aspire solution with four projects:

```
Loggy.AppHost         — Aspire orchestration host
Loggy.ApiService      — ASP.NET Core Web API (log processing + Gemini integration)
Loggy.Web             — Blazor Server frontend
Loggy.Models          — Shared models (TreeNode, LogEvent, Gemini DTOs)
Loggy.ServiceDefaults — Shared Aspire service defaults (telemetry, health checks)
```

### Request flow

```
Browser
  └── Blazor (Loggy.Web)
        ├── LogUploadApiClient  ──► POST /api/LogEventProcessing/ProcessEvents
        ├── LogUploadApiClient  ──► POST /api/LogEventProcessing/GetSortKeys
        ├── LogUploadApiClient  ──► POST /api/LogEventProcessing/GroupBy
        └── AnalysisApiClient   ──► POST /api/GeminiAPI/Query ──► Google Gemini API
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
- A Google Gemini API key

---

## Getting Started

**1. Clone the repository**

```bash
git clone https://github.com/your-org/loggy.git
cd loggy
```

**2. Add your Gemini API key**

In `Loggy.ApiService/appsettings.Development.json`:

```json
{
  "ApiKey": "YOUR_GEMINI_API_KEY"
}
```

**3. Run the application**

```bash
cd Loggy.AppHost
dotnet run
```

The Aspire dashboard will open automatically. The web frontend and API service start together and the frontend waits for the API to be healthy before accepting traffic.

---

## Log File Format

Loggy expects **newline-delimited JSON** (NDJSON) — one JSON object per line:

```jsonl
{"timestamp":"2024-01-15T10:00:00Z","level":"Info","message":"Application started","service":"WebAPI"}
{"timestamp":"2024-01-15T10:01:22Z","level":"Error","message":"Database timeout","service":"WebAPI","duration":5012}
{"timestamp":"2024-01-15T10:01:45Z","level":"Warning","message":"Retry attempt 1","service":"WebAPI"}
```

Any JSON shape is supported. Nested objects are preserved as child nodes in the tree and rendered in the UI.

---

## Project Structure

```
Loggy/
├── Loggy.AppHost/
│   └── AppHost.cs                          # Aspire host configuration
├── Loggy.ApiService/
│   ├── Controllers/
│   │   └── Classes/
│   │       ├── LogEventProcessingController.cs   # Upload, sort key, and group-by endpoints
│   │       └── GeminiAPIController.cs            # Gemini AI analysis endpoint
│   ├── Services/
│   │   └── Classes/
│   │       └── LogEventProcessingService.cs      # NDJSON parsing, sort keys, grouping
│   └── Options.cs                          # Configuration binding for API keys
├── Loggy.Web/
│   ├── ApiClients/
│   │   ├── LogUploadApiClient.cs           # HTTP client for log processing endpoints
│   │   └── AnalysisApiClient.cs            # HTTP client for Gemini analysis endpoint
│   └── Components/Pages/Upload/
│       ├── Upload.razor                    # Main page
│       └── Components/
│           ├── KeyGroup.razor              # Sort key selector
│           ├── LogGroup.razor              # Grouped log accordion
│           ├── AnalysisResult.razor        # AI analysis summary and pattern cards
│           └── TreeNode.razor              # Recursive nested value renderer
├── Loggy.Models/
│   ├── Logs/Classes/
│   │   ├── LogEvent.cs                     # Log event with Id and TreeNode<string> Log
│   │   └── TreeNode.cs                     # Generic tree node with parent/child tracking
│   └── Gemini/
│       └── GeminiModels.cs                 # Request/response DTOs for the Gemini API
└── Loggy.Tests/
    ├── LogEventProcessingServiceTests.cs   # Unit tests for parsing, grouping, sort keys
    └── LogEventProcessingControllerTests.cs # Unit tests for controller guard clauses and routing
```

---

## API Endpoints

### Log Processing — `LogEventProcessingController`

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/LogEventProcessing/ProcessEvents` | Upload an NDJSON file, returns `List<LogEvent>` as JSON |
| `POST` | `/api/LogEventProcessing/GetSortKeys` | Returns top-level key names from the first event |
| `POST` | `/api/LogEventProcessing/GroupBy?sortKey={key}` | Groups events by the value of the given key |

### AI Analysis — `GeminiAPIController`

| Method | Route | Description |
|--------|-------|-------------|
| `POST` | `/api/GeminiAPI/Query` | Sends log events to Gemini, returns structured `LogAnalysis` JSON |

---

## Running Tests

```bash
cd Loggy.Tests
dotnet test
```

Tests use **xUnit** and **Moq** and cover the service layer (parsing, grouping, sort keys), the controller layer (guard clauses, response shapes), and the `TreeNode<T>` model directly.

---

## Configuration

| Key | Location | Description |
|-----|----------|-------------|
| `ApiKey` | `Loggy.ApiService/appsettings.json` | Google Gemini API key |

---

## Tech Stack

- **Frontend** — Blazor Server, Bootstrap 5
- **Backend** — ASP.NET Core 10, .NET Aspire
- **AI** — Google Gemini 2.5 Flash (`gemini-2.5-flash`)
- **Testing** — xUnit, Moq
- **Serialization** — `System.Text.Json`
