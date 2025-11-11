# Changelog

## [0.1.3] - 2025-11-11

### Added
- **Per-line CodeLens hints**: Optional setting to show "influences" or "influenced by" on each line in the dataflow slice (disabled by default)
  - Enable via `sharpfocus.showPerLineCodeLens`

### Fixed
- **CodeLens counting logic**: Fixed to count slice sizes instead of incorrectly merging relation types
- **CodeLens terminology**: Corrected terminology to match dataflow semantics
  - "influenced by X" = sources that flow into the seed (backward slice)
  - "influences Y" = sinks that the seed flows into (forward slice)
- **Language server startup**: Fixed server path detection to properly handle self-contained executables

### Changed
- Removed unused `normalizeSliceRelation` import from CodeLens provider
- Simplified CodeLens display logic with cleaner conditional handling

## [0.1.2] - 2025-11-09

### Changed
- Language server now ships as self-contained deployment - no .NET installation required by users
- Single executable per platform for easier distribution and faster startup

## [0.1.1] - 2025-11-09

### Fixed
- Updated README image links to use GitHub raw content URLs for proper display on VS Code Marketplace

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
- Analysis may take a moment for very large methods
- Some complex aliasing patterns may be conservatively approximated

### Dependencies
- VS Code 1.80.0 or later
- vscode-languageclient 9.0.0

---

## Version History

- **0.1.3** (2025-11-11): Fixed CodeLens counting and terminology, added optional per-line hints
- **0.1.2** (2025-11-09): Self-contained deployment - no .NET installation required
- **0.1.1** (2025-11-09): Fixed README image links
- **0.1.0** (2025-11-09): Initial release with core Focus Mode functionality

[Unreleased]: https://github.com/trrahul/SharpFocus/compare/v0.1.3...HEAD
[0.1.3]: https://github.com/trrahul/SharpFocus/compare/v0.1.2...v0.1.3
[0.1.2]: https://github.com/trrahul/SharpFocus/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/trrahul/SharpFocus/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/trrahul/SharpFocus/releases/tag/v0.1.0
