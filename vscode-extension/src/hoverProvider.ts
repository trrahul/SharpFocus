import * as vscode from "vscode";
import {
  FocusModeResponse,
  LspRange,
  SliceRangeInfo,
  SliceRelation,
} from "./types";

interface HoverState {
  documentUri: string;
  response?: FocusModeResponse;
  detailMap?: Map<string, SliceRangeInfo[]>;
}

type FocusRangeKind = "seed" | "relevant";

interface RangeInfo {
  range: vscode.Range;
  kind: FocusRangeKind;
  symbolName: string;
  relations: Set<SliceRelation>;
}

export class SharpFocusHoverProvider implements vscode.HoverProvider {
  private state: HoverState | undefined;

  updateFocusMode(
    documentUri: string,
    response: FocusModeResponse | null
  ): void {
    if (!response) {
      if (this.state?.documentUri === documentUri) {
        this.state.response = undefined;
        this.state.detailMap = undefined;
      }
      return;
    }

    const detailMap = this.buildDetailMap(response);

    if (!this.state || this.state.documentUri !== documentUri) {
      this.state = { documentUri, response, detailMap };
    } else {
      this.state.response = response;
      this.state.detailMap = detailMap;
    }
  }

  clear(): void {
    this.state = undefined;
  }

  provideHover(
    document: vscode.TextDocument,
    position: vscode.Position,
    _token: vscode.CancellationToken
  ): vscode.Hover | undefined {
    if (!this.state || this.state.documentUri !== document.uri.toString()) {
      return undefined;
    }

    const match = this.collectRanges().find((range) =>
      range.range.contains(position)
    );

    if (!match) {
      return undefined;
    }

    const markdown = this.createHoverContent(match, document, position);
    return new vscode.Hover(markdown, match.range);
  }

  private collectRanges(): RangeInfo[] {
    const response = this.state?.response;
    const detailMap = this.state?.detailMap ?? new Map();
    if (!response) {
      return [];
    }

    const symbolName = response.focusedPlace.name;
    const ranges: RangeInfo[] = [
      {
        range: this.toVsRange(response.focusedPlace.range),
        kind: "seed",
        symbolName,
        relations: new Set<SliceRelation>(),
      },
    ];

    for (const relevant of response.relevantRanges) {
      const key = `${relevant.start.line}:${relevant.start.character}:${relevant.end.line}:${relevant.end.character}`;
      const details = detailMap.get(key) ?? [];
      const relations = new Set<SliceRelation>(
        details.map((d: SliceRangeInfo) => d.relation)
      );

      ranges.push({
        range: this.toVsRange(relevant),
        kind: "relevant",
        symbolName,
        relations,
      });
    }

    return ranges;
  }

  private createHoverContent(
    range: RangeInfo,
    document: vscode.TextDocument,
    position: vscode.Position
  ): vscode.MarkdownString {
    const md = new vscode.MarkdownString();
    md.isTrusted = true;

    md.appendMarkdown(`### Focus: \`${range.symbolName}\`\n\n`);

    if (range.kind === "seed") {
      md.appendMarkdown(
        "**◉ Analysis seed** — SharpFocus highlights code that flows into or out of this symbol.\n\n"
      );
    } else {
      if (range.relations.has("Source")) {
        md.appendMarkdown(
          `**Influences** \`${range.symbolName}\` (Source) — this statement provides data that flows into the seed.\n\n`
        );
      } else if (range.relations.has("Sink")) {
        md.appendMarkdown(
          `**Influenced by** \`${range.symbolName}\` (Sink) — this statement consumes data from the seed.\n\n`
        );
      } else if (range.relations.has("Transform")) {
        md.appendMarkdown(
          `**Transforms** \`${range.symbolName}\` — this statement modifies the value.\n\n`
        );
      } else {
        md.appendMarkdown(
          "**Related** — participates in the data flow for this symbol.\n\n"
        );
      }
    }

    const snippet = document.lineAt(position.line).text.trim();
    if (snippet.length > 0) {
      md.appendMarkdown("**Statement:**\n");
      md.appendCodeblock(snippet, "csharp");
      md.appendMarkdown("\n");
    }

    md.appendMarkdown("---\n\n");
    md.appendMarkdown("💡 *Toggle mode: `SharpFocus: Toggle Analysis Mode`*");

    return md;
  }

  private buildDetailMap(
    response: FocusModeResponse
  ): Map<string, SliceRangeInfo[]> {
    const map = new Map<string, SliceRangeInfo[]>();

    const addSlice = (slice: FocusModeResponse["backwardSlice"]): void => {
      if (!slice?.sliceRangeDetails) {
        return;
      }

      for (const detail of slice.sliceRangeDetails) {
        const key = `${detail.range.start.line}:${detail.range.start.character}:${detail.range.end.line}:${detail.range.end.character}`;
        const existing = map.get(key);
        if (existing) {
          existing.push(detail);
        } else {
          map.set(key, [detail]);
        }
      }
    };

    addSlice(response.backwardSlice ?? null);
    addSlice(response.forwardSlice ?? null);

    return map;
  }

  private toVsRange(range: LspRange): vscode.Range {
    return new vscode.Range(
      new vscode.Position(range.start.line, range.start.character),
      new vscode.Position(range.end.line, range.end.character)
    );
  }
}
