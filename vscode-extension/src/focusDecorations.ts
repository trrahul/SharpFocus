import * as vscode from "vscode";
import { normalizeSliceRelation } from "./relationUtils";
import { SliceRangeInfo } from "./types";

export interface FocusRenderSpan {
  range: vscode.Range;
  details: SliceRangeInfo[];
}

export class FocusDecorationFactory {
  createSeedDecorations(
    symbolName: string | undefined,
    seedRange: vscode.Range | undefined
  ): vscode.DecorationOptions[] {
    if (!seedRange) {
      return [];
    }

    return [
      {
        range: seedRange,
      },
    ];
  }

  createFocusDecorationsByRelation(
    document: vscode.TextDocument,
    symbolName: string | undefined,
    spans: FocusRenderSpan[]
  ): {
    source: vscode.DecorationOptions[];
    transform: vscode.DecorationOptions[];
    sink: vscode.DecorationOptions[];
    related: vscode.DecorationOptions[];
  } {
    const result = {
      source: [] as vscode.DecorationOptions[],
      transform: [] as vscode.DecorationOptions[],
      sink: [] as vscode.DecorationOptions[],
      related: [] as vscode.DecorationOptions[],
    };

    console.debug("SharpFocus[decorations]: creating decorations for spans", {
      spanCount: spans.length,
      targetSymbol: symbolName,
    });

    for (const span of spans) {
      const relations = new Set(
        span.details
          .map((d) => normalizeSliceRelation(d.relation))
          .filter((r): r is NonNullable<typeof r> => r !== undefined)
      );

      console.debug("SharpFocus[decorations]: processing span", {
        line: span.range.start.line,
        startChar: span.range.start.character,
        endChar: span.range.end.character,
        detailCount: span.details.length,
        uniqueRelations: Array.from(relations),
        places: span.details.map((d) => d.place?.name),
        text: document.getText(span.range).substring(0, 50),
      });

      const decoration: vscode.DecorationOptions = {
        range: span.range,
      };

      if (relations.has("Source")) {
        console.debug("SharpFocus[decorations]: assigning Source decoration");
        result.source.push(decoration);
      } else if (relations.has("Sink")) {
        console.debug("SharpFocus[decorations]: assigning Sink decoration");
        result.sink.push(decoration);
      } else if (relations.has("Transform")) {
        console.debug(
          "SharpFocus[decorations]: assigning Transform decoration"
        );
        result.transform.push(decoration);
      } else {
        console.debug("SharpFocus[decorations]: assigning Related decoration");
        result.related.push(decoration);
      }
    }

    console.debug("SharpFocus[decorations]: decoration assignment complete", {
      sources: result.source.length,
      sinks: result.sink.length,
      transforms: result.transform.length,
      related: result.related.length,
    });

    return result;
  }
}
