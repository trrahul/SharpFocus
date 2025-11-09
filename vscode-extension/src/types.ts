export type SliceDirection = "Backward" | "Forward";

export interface LspPosition {
  line: number;
  character: number;
}

export interface LspRange {
  start: LspPosition;
  end: LspPosition;
}

export interface PlaceInfo {
  name: string;
  range: LspRange;
  kind: string;
}

export interface SliceRangeInfo {
  range: LspRange;
  place: PlaceInfo;
  relation: SliceRelation;
  operationKind: string;
  summary?: string;
}

export type SliceRelation = "Source" | "Transform" | "Sink";

export interface SliceResponse {
  direction: SliceDirection;
  focusedPlace: PlaceInfo;
  sliceRanges: LspRange[];
  sliceRangeDetails?: SliceRangeInfo[];
  containerRanges: LspRange[];
}

export interface FocusModeResponse {
  focusedPlace: PlaceInfo;
  relevantRanges: LspRange[];
  containerRanges: LspRange[];
  backwardSlice?: SliceResponse | null;
  forwardSlice?: SliceResponse | null;
}
