import { SliceRelation } from "./types";

const VALID_SLICE_RELATIONS: ReadonlySet<SliceRelation> = new Set([
  "Source",
  "Transform",
  "Sink",
]);

function isSliceRelation(value: unknown): value is SliceRelation {
  return (
    typeof value === "string" &&
    VALID_SLICE_RELATIONS.has(value as SliceRelation)
  );
}

export function normalizeSliceRelation(
  value: SliceRelation | number | undefined | null
): SliceRelation | undefined {
  if (value === null || value === undefined) {
    return undefined;
  }

  if (isSliceRelation(value)) {
    return value;
  }

  if (typeof value === "number") {
    switch (value) {
      case 0:
        return "Source";
      case 1:
        return "Transform";
      case 2:
        return "Sink";
      default:
        console.warn(
          `SharpFocus[relationUtils]: Unknown numeric SliceRelation ${value}`
        );
        return undefined;
    }
  }

  console.warn(`SharpFocus[relationUtils]: Invalid SliceRelation value`, value);
  return undefined;
}
