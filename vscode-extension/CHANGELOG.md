# Changelog

All notable changes to the SharpFocus extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2025-11-09

### Added
- **Focus Mode**: Click on any variable to see its complete data flow
  - Backward slicing: See what influences a variable
  - Forward slicing: See what a variable influences
  - Automatic fading of unrelated code
- **Navigation System**:
  - Tree view for exploring flow locations
  - Keyboard shortcuts for next/previous navigation (`Ctrl+Alt+N`, `Ctrl+Alt+P`)
  - Jump to any flow location from tree view
- **Display Modes**:
  - Normal Mode: Clean, Flowistry-style minimalist interface
  - Advanced Mode: Detailed styling with colored relations and gutter icons
- **Language Server Integration**:
  - Roslyn-based analysis engine
  - Automatic server discovery and startup
  - Robust error handling and recovery
- **Smart Features**:
  - CodeLens annotations showing flow context
  - Hover providers for detailed information
  - Status bar indicator for analysis state
  - Automatic word highlight disabling to prevent conflicts
- **Configuration Options**:
  - Analysis mode (focus/off)
  - Display mode (normal/advanced)
  - Custom server path support
  - Debug tracing capabilities
- **Commands**:
  - Toggle Analysis Mode
  - Clear Highlights
  - Show Flow Details
  - Navigate Next/Previous
  - Navigate to Location
  - Restart Server

### Technical Details
- Built on Language Server Protocol (LSP)
- TypeScript implementation with full type safety
- Modular architecture with clean separation of concerns
- Comprehensive error handling and user feedback
- Performance optimizations for large codebases

### Known Limitations
- Single-method analysis only (interprocedural analysis planned)
- Requires .NET 8.0 SDK or later
- Analysis may take a moment for very large methods
- Some complex aliasing patterns may be conservatively approximated

### Dependencies
- VS Code 1.80.0 or later
- .NET 8.0 SDK or later
- vscode-languageclient 9.0.0

---

## Version History

- **0.1.0** (2025-11-09): Initial release with core Focus Mode functionality

[Unreleased]: https://github.com/trrahul/SharpFocus/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/trrahul/SharpFocus/releases/tag/v0.1.0
