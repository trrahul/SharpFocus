# SharpFocus

[![VS Code Marketplace](https://img.shields.io/badge/VS%20Code-Marketplace-blue)](https://marketplace.visualstudio.com/items?itemName=RahulTR.sharpfocus)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)

**Information Flow Analysis for C# — Understand What Your Code Really Does**

SharpFocus is a Visual Studio Code extension that brings information-flow analysis to C#. Inspired by [Flowistry](https://github.com/willcrichton/flowistry) for Rust, it uses program slicing to help developers understand data dependencies and code relationships at a glance.

![SharpFocus Demo](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/advanced-mode-2.png)

---

## What is SharpFocus?

SharpFocus implements **program slicing** — a static analysis technique that answers two critical questions about any variable in your code:

1. **Backward Slice:** What code could have influenced this variable?
2. **Forward Slice:** What code could be influenced by this variable?

By combining both directions, SharpFocus creates a **focus mode** that highlights only the relevant code paths, fading everything else away. Perfect for debugging, refactoring, and code review.

---

## Features

### Focus Mode
Click any variable, parameter, or field and see the complete dataflow instantly. Everything else fades away.

**Normal Mode**

![Normal Mode](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/normal-mode.png)

**Advanced Mode** - Detailed flow indicators with colored relations and gutter icons:

![Advanced Mode](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/advanced-mode.png)

### Navigation
Navigate through your code's dataflow with keyboard shortcuts and visual aids:
- Tree view with hierarchical flow visualization
- CodeLens annotations at each step
- Quick pick menu for flow details
- Keyboard shortcuts: `Ctrl+Alt+N` (next) / `Ctrl+Alt+P` (previous)

**Tree View** shows all flow locations organized by type:

![Tree View](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/tree.png)

**Complete View** with both code highlighting and tree navigation:

![Advanced Mode with Tree](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/advanced-mode-with-tree.png)

**Normal Mode with Tree** for a cleaner look:

![Normal Mode with Tree](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/normal-mode-with-tree.png)

### Settings

![Settings](https://raw.githubusercontent.com/trrahul/SharpFocus/main/vscode-extension/images/settings.png)

- **Analysis Mode:** Focus on click, or manual trigger
- **Display Mode:** Normal (minimalist) or Advanced (detailed)
- **Server Path:** Custom language server location
- **Trace Level:** Debugging and diagnostics
- **UI Options:** Disable word highlighting to avoid conflicts

---

## Installation

### From VS Code Marketplace
```
ext install RahulTR.sharpfocus
```

### From VSIX
Download the latest release from [Releases](https://github.com/trrahul/SharpFocus/releases) and install via:
```bash
code --install-extension sharpfocus-0.1.0.vsix
```

---

## Quick Start

1. Open a C# project in VS Code
2. Click on any variable or parameter
3. Watch SharpFocus highlight the complete dataflow
4. Use `Ctrl+Alt+N` / `Cmd+Alt+N` to navigate through flow locations

That's it! No configuration needed for basic usage.

---

## Architecture

SharpFocus consists of three main components:

### 1. **SharpFocus.Core** (`src/SharpFocus.Core/`)
Core abstractions, data models, and interfaces:
- Flow domain and slice computation models
- Analysis request/response contracts
- Immutable data structures with builder patterns

### 2. **SharpFocus.Analysis** (`src/SharpFocus.Analysis/`)
Roslyn-based static analysis engine:
- Control-flow graph (CFG) construction
- Dataflow analysis and abstract interpretation
- Alias analysis and mutation detection
- Forward/backward slice computation
- Caching and performance optimization

### 3. **SharpFocus.LanguageServer** (`src/SharpFocus.LanguageServer/`)
LSP-compliant language server using OmniSharp extensions:
- Request handling and orchestration
- Workspace management
- Diagnostics and error handling
- Logging and observability

### 4. **VS Code Extension** (`vscode-extension/`)
TypeScript-based VS Code client:
- LSP client integration
- UI rendering (decorations, CodeLens, tree view)
- Navigation and command handling
- Settings and configuration

---

## Development Setup

### Prerequisites
- .NET 10.0 SDK (preview) or .NET 8.0+
- Node.js 20+ and npm
- VS Code 1.80.0+

### Clone and Build

```bash
# Clone the repository
git clone https://github.com/trrahul/SharpFocus.git
cd SharpFocus

# Build the language server
dotnet build SharpFocus.sln

# Build the VS Code extension
cd vscode-extension
npm install
npm run compile

# Package the extension (includes language server)
npm run bundle
npm run package
```

### Running in Development

1. Open the repository in VS Code
2. Press `F5` to launch Extension Development Host
3. Open a C# project in the new window
4. Test Focus Mode on C# files

### Running Tests

```bash
# Run C# unit tests
dotnet test

# Run integration tests
dotnet test tests/SharpFocus.Integration.Tests/
```

---

## Example Use Cases

### Debugging Null References
```csharp
var result = ProcessData(input);
if (result == null) {  // Click 'result' here
    // SharpFocus shows everywhere result could be assigned null
}
```

### Refactoring Impact Analysis
```csharp
var config = LoadConfig();  // Click 'config'
// See everywhere this config is used before changing it
```

### Code Review
```csharp
public void ComplexMethod(Data input) {
    // Click 'input' to see its flow through 200 lines
    // Focus on what matters, ignore the noise
}
```

---

## Roadmap

### Current (v0.1.0)
- Focus Mode with forward/backward slicing
- Normal and Advanced display modes
- Tree view and CodeLens integration
- Intra-method analysis

### Planned
- **Cross-method analysis** - Slice across method boundaries
- **Property and field analysis** - Track through getters/setters
- **Async/await support** - Handle Task-based flows
- **LINQ query analysis** - Track through query expressions
- **Performance improvements** - Large solution optimization
- **Configuration UI** - Visual settings editor

See [ROADMAP.md](docs/ROADMAP.md) for details.

---

## Acknowledgments

- **[Flowistry](https://github.com/willcrichton/flowistry)**
- **[Roslyn](https://github.com/dotnet/roslyn)** 
- **[OmniSharp](https://github.com/OmniSharp/csharp-language-server-protocol)** 

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Contributing

Contributions are welcome! 

### Areas for Contribution
- Cross-method analysis implementation
- Performance optimization
- Additional display modes
- Documentation improvements
- Bug fixes and testing


---

**Made with ❤️ for C# developers who want to understand their code better**

Please ⭐this repo if you find it useful!
