import * as vscode from "vscode";
import { normalizeSliceRelation } from "./relationUtils";
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
    let sources = 0;
    let transforms = 0;
    let sinks = 0;

    if (response.backwardSlice?.sliceRangeDetails) {
      for (const detail of response.backwardSlice.sliceRangeDetails) {
        const relation = normalizeSliceRelation(detail.relation);
        if (relation === "Source") sources++;
        else if (relation === "Transform") transforms++;
        else if (relation === "Sink") sinks++;
      }
    }

    if (response.forwardSlice?.sliceRangeDetails) {
      for (const detail of response.forwardSlice.sliceRangeDetails) {
        const relation = normalizeSliceRelation(detail.relation);
        if (relation === "Source") sources++;
        else if (relation === "Transform") transforms++;
        else if (relation === "Sink") sinks++;
      }
    }

    return { sources, transforms, sinks };
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
      return `${symbolName} · influenced by ${sourceCount} · not used (warning)`;
    }

    if (sourceCount === 0 && sinkCount > 0) {
      return `${symbolName} · influences ${sinkCount} ${this.pluralize(
        "location",
        sinkCount
      )}`;
    }

    const sourceText = sourceCount > 0 ? `influenced by ${sourceCount}` : null;
    const sinkText = sinkCount > 0 ? `influences ${sinkCount}` : null;

    const parts = [sourceText, sinkText].filter((p) => p !== null);

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
