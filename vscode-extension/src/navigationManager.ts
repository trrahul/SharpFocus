import * as vscode from "vscode";
import { normalizeSliceRelation } from "./relationUtils";
import { FocusModeResponse, SliceRangeInfo, SliceRelation } from "./types";

export interface NavigableLocation {
  uri: string;
  range: {
    start: { line: number; character: number };
    end: { line: number; character: number };
  };
  label: string;
  description: string;
  detail?: string;
  relation: string;
  category: "source" | "sink" | "transform";
  methodName?: string;
  summary?: string;
}

export class NavigationManager {
  private locations: NavigableLocation[] = [];
  private currentIndex: number = -1;
  private focusedSymbolName: string = "";
  private onNavigationChangeCallback: (() => void) | null = null;
  private onBeforeNavigateCallback: (() => void) | null = null;
  private onAfterNavigateCallback: (() => void) | null = null;
  private navigationInProgress: boolean = false;

  public onNavigationChange(callback: () => void): void {
    this.onNavigationChangeCallback = callback;
  }

  public onBeforeNavigate(callback: () => void): void {
    this.onBeforeNavigateCallback = callback;
  }

  public onAfterNavigate(callback: () => void): void {
    this.onAfterNavigateCallback = callback;
  }

  private notifyNavigationChange(): void {
    if (this.onNavigationChangeCallback) {
      this.onNavigationChangeCallback();
    }
  }

  public isNavigationInProgress(): boolean {
    return this.navigationInProgress;
  }

  private beginNavigation(): void {
    this.navigationInProgress = true;
    if (this.onBeforeNavigateCallback) {
      this.onBeforeNavigateCallback();
    }
  }

  private endNavigation(): void {
    setTimeout(() => {
      this.navigationInProgress = false;
      if (this.onAfterNavigateCallback) {
        this.onAfterNavigateCallback();
      }
    }, 0);
  }

  private async withNavigation<T>(action: () => Promise<T>): Promise<T> {
    this.beginNavigation();
    try {
      return await action();
    } finally {
      this.endNavigation();
    }
  }

  public initialize(documentUri: string, focusMode: FocusModeResponse): void {
    this.locations = this.extractLocations(documentUri, focusMode);
    this.currentIndex = -1;
    this.focusedSymbolName = focusMode.focusedPlace.name;
    this.notifyNavigationChange();
  }

  public clear(): void {
    this.locations = [];
    this.currentIndex = -1;
    this.focusedSymbolName = "";
    this.notifyNavigationChange();
  }

  public getLocations(): NavigableLocation[] {
    return [...this.locations];
  }

  public getCurrentIndex(): number {
    return this.currentIndex;
  }

  public getTotalCount(): number {
    return this.locations.length;
  }

  public getSymbolName(): string {
    return this.focusedSymbolName;
  }

  public hasLocations(): boolean {
    return this.locations.length > 0;
  }

  public async navigateNext(): Promise<boolean> {
    if (!this.hasLocations()) {
      vscode.window.showInformationMessage("No flow locations to navigate");
      return false;
    }

    this.currentIndex = (this.currentIndex + 1) % this.locations.length;
    await this.navigateToCurrentLocation();
    this.notifyNavigationChange();
    return true;
  }

  public async navigatePrevious(): Promise<boolean> {
    if (!this.hasLocations()) {
      vscode.window.showInformationMessage("No flow locations to navigate");
      return false;
    }

    this.currentIndex =
      this.currentIndex <= 0
        ? this.locations.length - 1
        : this.currentIndex - 1;
    await this.navigateToCurrentLocation();
    this.notifyNavigationChange();
    return true;
  }

  public async navigateToIndex(index: number): Promise<boolean> {
    if (index < 0 || index >= this.locations.length) {
      return false;
    }

    this.currentIndex = index;
    await this.navigateToCurrentLocation();
    this.notifyNavigationChange();
    return true;
  }

  public async navigateToLocation(location: NavigableLocation): Promise<void> {
    await this.withNavigation(async () => {
      const index = this.locations.indexOf(location);
      if (index >= 0) {
        this.currentIndex = index;
      }

      const uri = vscode.Uri.parse(location.uri);
      const document = await vscode.workspace.openTextDocument(uri);
      const editor = await vscode.window.showTextDocument(document);

      const range = new vscode.Range(
        new vscode.Position(
          location.range.start.line,
          location.range.start.character
        ),
        new vscode.Position(
          location.range.end.line,
          location.range.end.character
        )
      );

      editor.selection = new vscode.Selection(range.start, range.end);
      editor.revealRange(range, vscode.TextEditorRevealType.InCenter);

      this.showNavigationStatus();
      this.notifyNavigationChange();
    });
  }

  public getCurrentLocationInfo(): string {
    if (!this.hasLocations() || this.currentIndex < 0) {
      return "";
    }

    const location = this.locations[this.currentIndex];
    const position = this.currentIndex + 1;
    return `${position}/${this.locations.length}: ${location.label} · ${location.description}`;
  }

  private async navigateToCurrentLocation(): Promise<void> {
    if (this.currentIndex < 0 || this.currentIndex >= this.locations.length) {
      return;
    }

    const location = this.locations[this.currentIndex];
    await this.navigateToLocation(location);
  }

  private showNavigationStatus(): void {
    if (!this.hasLocations() || this.currentIndex < 0) {
      return;
    }

    const location = this.locations[this.currentIndex];
    const position = this.currentIndex + 1;
    const total = this.locations.length;

    const message = `${this.focusedSymbolName}: ${position}/${total} · ${
      location.description
    } · Line ${location.range.start.line + 1}`;

    vscode.window.setStatusBarMessage(`SharpFocus: ${message}`, 3000);
  }

  private extractLocations(
    documentUri: string,
    focusMode: FocusModeResponse
  ): NavigableLocation[] {
    const locations: NavigableLocation[] = [];

    if (focusMode.backwardSlice?.sliceRangeDetails) {
      for (const detail of focusMode.backwardSlice.sliceRangeDetails) {
        if (!this.isValidSliceRangeInfo(detail)) {
          console.warn(
            "SharpFocus[navigation]: skipping invalid backward slice detail",
            detail
          );
          continue;
        }

        const relation = normalizeSliceRelation(detail.relation) ?? "Source";
        const category = this.toCategory(relation);
        locations.push(
          this.createLocation(documentUri, detail, category, relation)
        );
      }
    }

    if (focusMode.forwardSlice?.sliceRangeDetails) {
      for (const detail of focusMode.forwardSlice.sliceRangeDetails) {
        if (!this.isValidSliceRangeInfo(detail)) {
          console.warn(
            "SharpFocus[navigation]: skipping invalid forward slice detail",
            detail
          );
          continue;
        }

        const relation = normalizeSliceRelation(detail.relation) ?? "Sink";
        const category = this.toCategory(relation);
        locations.push(
          this.createLocation(documentUri, detail, category, relation)
        );
      }
    }

    locations.sort((a, b) => {
      if (a.range.start.line !== b.range.start.line) {
        return a.range.start.line - b.range.start.line;
      }
      return a.range.start.character - b.range.start.character;
    });

    return locations;
  }

  private isValidSliceRangeInfo(detail: SliceRangeInfo): boolean {
    if (!detail || !detail.range) {
      return false;
    }

    if (
      typeof detail.range.start?.line !== "number" ||
      typeof detail.range.start?.character !== "number" ||
      typeof detail.range.end?.line !== "number" ||
      typeof detail.range.end?.character !== "number"
    ) {
      return false;
    }

    return true;
  }

  private createLocation(
    documentUri: string,
    detail: SliceRangeInfo,
    category: "source" | "sink" | "transform",
    relation: SliceRelation
  ): NavigableLocation {
    const line = detail.range.start.line + 1;
    const symbolName = detail.place?.name ?? "";

    console.log(
      `SharpFocus[navigation]: creating location with summary="${detail.summary}", operationKind="${detail.operationKind}"`
    );

    return {
      uri: documentUri,
      range: detail.range,
      label: `Line ${line}`,
      description: `${relation}${symbolName ? ` · ${symbolName}` : ""}`,
      detail: symbolName || undefined,
      relation,
      category,
      summary: detail.summary,
    };
  }

  private toCategory(relation: SliceRelation): "source" | "sink" | "transform" {
    switch (relation) {
      case "Source":
        return "source";
      case "Transform":
        return "transform";
      case "Sink":
      default:
        return "sink";
    }
  }
}
