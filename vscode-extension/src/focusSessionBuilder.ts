import * as vscode from "vscode";
import { FocusRenderSpan } from "./focusDecorations";
import { FocusRelationCounts, FocusSession } from "./focusSession";
import { normalizeSliceRelation } from "./relationUtils";
import { FocusModeResponse, LspRange, SliceRangeInfo } from "./types";

export class FocusSessionBuilder {
  create(
    editor: vscode.TextEditor,
    response: FocusModeResponse | null
  ): FocusSession | null {
    if (!response) {
      console.debug("SharpFocus[focus]: empty response returned");
      return null;
    }

    const detailMap = this.buildDetailMap(response);
    const focusSpans = this.buildFocusSpans(response.relevantRanges, detailMap);
    if (focusSpans.length === 0) {
      console.debug(
        "SharpFocus[focus]: response contained no relevant ranges",
        {
          document: editor.document.uri.toString(),
          focusedPlace: response.focusedPlace?.name,
        }
      );
      return null;
    }

    const focusRanges = focusSpans.map((span) => span.range);
    const containerRanges = this.mergeRanges(
      this.toVsCodeRanges(response.containerRanges)
    );
    const fadeRanges = this.computeFadeRanges(
      editor.document,
      containerRanges,
      focusRanges
    );
    const seedRange = response.focusedPlace
      ? this.toVsCodeRange(response.focusedPlace.range)
      : undefined;
    const relationCounts = this.computeRelationCounts(focusSpans);

    console.info("SharpFocus[focus]: session built", {
      document: editor.document.uri.toString(),
      focusedPlace: response.focusedPlace?.name,
      focusCount: focusSpans.length,
      containerCount: response.containerRanges.length,
      relationCounts,
    });

    return {
      documentUri: editor.document.uri.toString(),
      targetSymbol: response.focusedPlace?.name,
      focusSpans,
      fadeRanges,
      seedRange,
      relationCounts,
      containerCount: response.containerRanges.length,
    };
  }

  private buildDetailMap(
    response: FocusModeResponse
  ): Map<string, SliceRangeInfo[]> {
    const map = new Map<string, SliceRangeInfo[]>();

    const addSlice = (slice: FocusModeResponse["backwardSlice"]): void => {
      if (!slice?.sliceRangeDetails) {
        console.debug("SharpFocus[focus]: slice missing details", {
          direction: slice?.direction,
        });
        return;
      }

      console.debug(
        `SharpFocus[focus]: processing ${slice.direction} slice with ${slice.sliceRangeDetails.length} details`
      );

      for (const detail of slice.sliceRangeDetails) {
        const key = this.createRangeKeyFromLsp(detail.range);
        const existing = map.get(key);

        console.debug("SharpFocus[focus]: detail range", {
          key,
          relation: detail.relation,
          placeName: detail.place?.name,
          line: detail.range.start.line,
          startChar: detail.range.start.character,
          endChar: detail.range.end.character,
          alreadyExists: !!existing,
        });

        if (existing) {
          existing.push(detail);
        } else {
          map.set(key, [detail]);
        }
      }
    };

    addSlice(response.backwardSlice ?? null);
    addSlice(response.forwardSlice ?? null);

    console.debug("SharpFocus[focus]: detail map populated", {
      keys: map.size,
      uniqueRanges: Array.from(map.keys()),
    });

    return map;
  }

  private buildFocusSpans(
    relevantRanges: LspRange[],
    detailMap: Map<string, SliceRangeInfo[]>
  ): FocusRenderSpan[] {
    const spans = relevantRanges.map<FocusRenderSpan>((range) => {
      const key = this.createRangeKeyFromLsp(range);
      const details = detailMap.get(key) ?? [];
      return {
        range: this.toVsCodeRange(range),
        details: [...details],
      };
    });

    console.debug("SharpFocus[focus]: raw spans constructed", {
      spanCount: spans.length,
    });

    return this.mergeFocusSpans(spans);
  }

  private mergeFocusSpans(spans: FocusRenderSpan[]): FocusRenderSpan[] {
    if (spans.length === 0) {
      return [];
    }

    const sorted = [...spans].sort((a, b) => {
      const compareStart = a.range.start.compareTo(b.range.start);
      if (compareStart !== 0) {
        return compareStart;
      }
      return a.range.end.compareTo(b.range.end);
    });

    console.debug("SharpFocus[focus]: merging spans", {
      inputCount: spans.length,
      sortedSpans: sorted.map((s) => ({
        line: s.range.start.line,
        startChar: s.range.start.character,
        endChar: s.range.end.character,
        detailCount: s.details.length,
        relations: s.details.map((d) => normalizeSliceRelation(d.relation)),
      })),
    });

    const merged: FocusRenderSpan[] = [];
    let current: FocusRenderSpan = {
      range: sorted[0].range,
      details: [...sorted[0].details],
    };

    for (let i = 1; i < sorted.length; i += 1) {
      const next = sorted[i];
      const overlapsOrTouches =
        next.range.start.isBefore(current.range.end) ||
        next.range.start.isEqual(current.range.end);

      console.debug("SharpFocus[focus]: comparing spans", {
        currentSpan: {
          line: current.range.start.line,
          start: current.range.start.character,
          end: current.range.end.character,
        },
        nextSpan: {
          line: next.range.start.line,
          start: next.range.start.character,
          end: next.range.end.character,
        },
        overlapsOrTouches,
        willMerge: overlapsOrTouches,
      });

      if (overlapsOrTouches) {
        const newEnd = next.range.end.isAfter(current.range.end)
          ? next.range.end
          : current.range.end;
        const combinedDetails = [...current.details];

        for (const detail of next.details) {
          if (!combinedDetails.includes(detail)) {
            combinedDetails.push(detail);
          }
        }

        console.debug("SharpFocus[focus]: merged spans", {
          oldEnd: current.range.end.character,
          newEnd: newEnd.character,
          totalDetails: combinedDetails.length,
        });

        current = {
          range: new vscode.Range(current.range.start, newEnd),
          details: combinedDetails,
        };
      } else {
        merged.push(current);
        current = {
          range: next.range,
          details: [...next.details],
        };
      }
    }

    merged.push(current);

    console.debug("SharpFocus[focus]: merge complete", {
      outputCount: merged.length,
      mergedSpans: merged.map((s) => ({
        line: s.range.start.line,
        startChar: s.range.start.character,
        endChar: s.range.end.character,
        detailCount: s.details.length,
        places: s.details.map((d) => d.place?.name),
      })),
    });

    return merged;
  }

  private computeRelationCounts(spans: FocusRenderSpan[]): FocusRelationCounts {
    const counts: FocusRelationCounts = { source: 0, transform: 0, sink: 0 };

    for (const span of spans) {
      for (const detail of span.details) {
        const relation = normalizeSliceRelation(detail.relation);
        switch (relation) {
          case "Source":
            counts.source += 1;
            break;
          case "Transform":
            counts.transform += 1;
            break;
          case "Sink":
            counts.sink += 1;
            break;
        }
      }
    }

    return counts;
  }

  private computeFadeRanges(
    document: vscode.TextDocument,
    containerRanges: vscode.Range[],
    focusRanges: vscode.Range[]
  ): vscode.Range[] {
    if (containerRanges.length === 0 || focusRanges.length === 0) {
      return [];
    }

    const fadeRanges: vscode.Range[] = [];
    for (const container of containerRanges) {
      fadeRanges.push(
        ...this.subtractRangesWithinContainer(container, focusRanges, document)
      );
    }

    return this.mergeRanges(fadeRanges);
  }

  private subtractRangesWithinContainer(
    container: vscode.Range,
    focusRanges: vscode.Range[],
    document: vscode.TextDocument
  ): vscode.Range[] {
    const containerStart = document.offsetAt(container.start);
    const containerEnd = document.offsetAt(container.end);

    const overlaps = focusRanges
      .map<{ start: number; end: number } | null>((range) => {
        const start = Math.max(containerStart, document.offsetAt(range.start));
        const end = Math.min(containerEnd, document.offsetAt(range.end));
        return start < end ? { start, end } : null;
      })
      .filter(
        (value): value is { start: number; end: number } => value !== null
      )
      .sort((a, b) => a.start - b.start);

    const fadeRanges: vscode.Range[] = [];
    let cursor = containerStart;

    for (const interval of overlaps) {
      if (interval.start > cursor) {
        fadeRanges.push(
          new vscode.Range(
            document.positionAt(cursor),
            document.positionAt(interval.start)
          )
        );
      }
      cursor = Math.max(cursor, interval.end);
    }

    if (cursor < containerEnd) {
      fadeRanges.push(
        new vscode.Range(
          document.positionAt(cursor),
          document.positionAt(containerEnd)
        )
      );
    }

    return fadeRanges;
  }

  private mergeRanges(ranges: vscode.Range[]): vscode.Range[] {
    if (ranges.length === 0) {
      return [];
    }

    const sorted = [...ranges].sort((a, b) => {
      const compareStart = a.start.compareTo(b.start);
      if (compareStart !== 0) {
        return compareStart;
      }
      return a.end.compareTo(b.end);
    });

    const merged: vscode.Range[] = [];
    let current = sorted[0];

    for (let i = 1; i < sorted.length; i += 1) {
      const next = sorted[i];
      const startsBeforeOrAtCurrentEnd =
        next.start.isBefore(current.end) || next.start.isEqual(current.end);
      if (startsBeforeOrAtCurrentEnd) {
        const end = next.end.isAfter(current.end) ? next.end : current.end;
        current = new vscode.Range(current.start, end);
      } else {
        merged.push(current);
        current = next;
      }
    }

    merged.push(current);
    return merged;
  }

  private toVsCodeRange(lspRange: LspRange): vscode.Range {
    return new vscode.Range(
      new vscode.Position(lspRange.start.line, lspRange.start.character),
      new vscode.Position(lspRange.end.line, lspRange.end.character)
    );
  }

  private toVsCodeRanges(ranges: LspRange[]): vscode.Range[] {
    return ranges.map((range) => this.toVsCodeRange(range));
  }

  private createRangeKeyFromLsp(range: LspRange): string {
    return `${range.start.line}:${range.start.character}-${range.end.line}:${range.end.character}`;
  }
}
