import * as vscode from "vscode";
import { FocusRenderSpan } from "./focusDecorations";

export interface FocusRelationCounts {
  source: number;
  transform: number;
  sink: number;
}

export interface FocusSession {
  readonly documentUri: string;
  readonly targetSymbol?: string;
  readonly focusSpans: FocusRenderSpan[];
  readonly fadeRanges: vscode.Range[];
  readonly seedRange?: vscode.Range;
  readonly relationCounts: FocusRelationCounts;
  readonly containerCount: number;
}
