import * as vscode from "vscode";
import { FocusModeResponse } from "./types";

interface AnalysisState {
  documentUri: string;
  focusMode?: FocusModeResponse;
}

export class SharpFocusCodeLensProvider implements vscode.CodeLensProvider {
  private readonly _onDidChangeCodeLenses = new vscode.EventEmitter<void>();
  public readonly onDidChangeCodeLenses = this._onDidChangeCodeLenses.event;

  private analysisState: AnalysisState | undefined;

  updateFocusMode(
    documentUri: string,
    response: FocusModeResponse | null
  ): void {
    if (!response) {
      if (this.analysisState?.documentUri === documentUri) {
        this.analysisState.focusMode = undefined;
      }
      this._onDidChangeCodeLenses.fire();
      return;
    }

    if (!this.analysisState || this.analysisState.documentUri !== documentUri) {
      this.analysisState = { documentUri, focusMode: response };
    } else {
      this.analysisState.focusMode = response;
    }

    this._onDidChangeCodeLenses.fire();
  }

  clear(): void {
    this.analysisState = undefined;
    this._onDidChangeCodeLenses.fire();
  }

  provideCodeLenses(
    document: vscode.TextDocument,
    _token: vscode.CancellationToken
  ): vscode.CodeLens[] {
    const lenses: vscode.CodeLens[] = [];

    if (
      !this.analysisState ||
      this.analysisState.documentUri !== document.uri.toString()
    ) {
      return lenses;
    }

    const { focusMode } = this.analysisState;

    if (focusMode) {
      const seedRange = this.toVsRange(focusMode.focusedPlace.range);
      const symbolName = focusMode.focusedPlace.name;
      const seedLine = focusMode.focusedPlace.range.start.line;

      // Add CodeLens for the seed (always shown)
      const counts = this.countRelations(focusMode);
      const sourceCount = counts.sources;
      const sinkCount = counts.sinks;
      const transformCount = counts.transforms;
      const title = this.generateCodeLensText(
        symbolName,
        sourceCount,
        sinkCount,
        transformCount
      );

      lenses.push(
        new vscode.CodeLens(seedRange, {
          title,
          tooltip: this.generateTooltip(
            symbolName,
            sourceCount,
            sinkCount,
            transformCount
          ),
          command: "sharpfocus.showFlowDetails",
          arguments: [document.uri.toString(), focusMode],
        })
      );

      // Check if per-line CodeLens is enabled
      const config = vscode.workspace.getConfiguration("sharpfocus");
      const showPerLineCodeLens = config.get<boolean>("showPerLineCodeLens", false);

      if (showPerLineCodeLens) {
        // Add CodeLens for each line in the slices
        const processedLines = new Set<number>();

        // Process backward slice (what influences the seed)
        if (focusMode.backwardSlice?.sliceRangeDetails) {
          for (const detail of focusMode.backwardSlice.sliceRangeDetails) {
            const line = detail.place.range.start.line;

            // Skip seed line and already processed lines
            if (line === seedLine || processedLines.has(line)) {
              continue;
            }

            processedLines.add(line);
            const detailRange = this.toVsRange(detail.place.range);

            lenses.push(
              new vscode.CodeLens(detailRange, {
                title: `influences`,
                tooltip: `This code influences ${symbolName}`,
                command: "",
              })
            );
          }
        }

        // Process forward slice (what is influenced by the seed)
        if (focusMode.forwardSlice?.sliceRangeDetails) {
          for (const detail of focusMode.forwardSlice.sliceRangeDetails) {
            const line = detail.place.range.start.line;

            // Skip seed line and already processed lines
            if (line === seedLine || processedLines.has(line)) {
              continue;
            }

            processedLines.add(line);
            const detailRange = this.toVsRange(detail.place.range);

            lenses.push(
              new vscode.CodeLens(detailRange, {
                title: `influenced by`,
                tooltip: `${symbolName} influences this code`,
                command: "",
              })
            );
          }
        }
      }
    }

    return lenses;
  }

  private toVsRange(lspRange: {
    start: { line: number; character: number };
    end: { line: number; character: number };
  }): vscode.Range {
    return new vscode.Range(
      new vscode.Position(lspRange.start.line, lspRange.start.character),
      new vscode.Position(lspRange.end.line, lspRange.end.character)
    );
  }

  private countRelations(response: FocusModeResponse): {
    sources: number;
    transforms: number;
    sinks: number;
  } {
    // Count slice sizes directly instead of counting relation types
    const backwardCount = response.backwardSlice?.sliceRangeDetails?.length ?? 0;
    const forwardCount = response.forwardSlice?.sliceRangeDetails?.length ?? 0;

    // Count transforms (items that appear in both slices)
    const backwardLines = new Set(
      response.backwardSlice?.sliceRangeDetails?.map(d => d.place.range.start.line) ?? []
    );
    const forwardLines = new Set(
      response.forwardSlice?.sliceRangeDetails?.map(d => d.place.range.start.line) ?? []
    );
    const transformCount = [...backwardLines].filter(line => forwardLines.has(line)).length;

    // Return: sources = backward count (what influences seed)
    //         sinks = forward count (what seed influences)
    //         transforms = intersection count
    return {
      sources: backwardCount,
      sinks: forwardCount,
      transforms: transformCount
    };
  }

  private generateCodeLensText(
    symbolName: string,
    sourceCount: number,
    sinkCount: number,
    transformCount: number
  ): string {
    const totalCount = sourceCount + sinkCount + transformCount;

    if (totalCount === 0) {
      return `${symbolName} · isolated · no data flow detected`;
    }

    if (sourceCount > 0 && sinkCount === 0 && transformCount === 0) {
      return `${symbolName} · influenced by ${sourceCount} · not used`;
    }

    if (sourceCount === 0 && sinkCount > 0 && transformCount === 0) {
      return `${symbolName} · influences ${sinkCount} ${this.pluralize(
        "location",
        sinkCount
      )}`;
    }

    if (sourceCount === 0 && sinkCount === 0 && transformCount > 0) {
      return `${symbolName} · ${transformCount} ${this.pluralize(
        "transform",
        transformCount
      )}`;
    }

    const parts: string[] = [];
    if (sourceCount > 0) {
      parts.push(`influenced by ${sourceCount}`);
    }
    if (sinkCount > 0) {
      parts.push(`influences ${sinkCount}`);
    }
    // Don't show transform count in main text when sources/sinks are present
    // It will appear in the tooltip

    return `${symbolName} · ${parts.join(" · ")}  [Show Flow]`;
  }

  private generateTooltip(
    symbolName: string,
    sourceCount: number,
    sinkCount: number,
    transformCount: number
  ): string {
    const parts: string[] = [`Data flow for \`${symbolName}\``];

    if (sourceCount > 0) {
      parts.push(
        `• ${sourceCount} ${this.pluralize(
          "source",
          sourceCount
        )} (what influences it)`
      );
    }
    if (transformCount > 0) {
      parts.push(
        `• ${transformCount} ${this.pluralize("transform", transformCount)}`
      );
    }
    if (sinkCount > 0) {
      parts.push(
        `• ${sinkCount} ${this.pluralize(
          "destination",
          sinkCount
        )} (what it influences)`
      );
    }

    if (sourceCount === 0 && sinkCount === 0 && transformCount === 0) {
      parts.push("No data flow connections detected");
    }

    parts.push("\nClick to view details and navigate");

    return parts.join("\n");
  }

  private pluralize(word: string, count: number): string {
    return count === 1 ? word : `${word}s`;
  }

  dispose(): void {
    this._onDidChangeCodeLenses.dispose();
  }
}
