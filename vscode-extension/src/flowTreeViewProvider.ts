import * as path from "path";
import * as vscode from "vscode";
import { NavigableLocation, NavigationManager } from "./navigationManager";
import { FocusModeResponse } from "./types";

export class FlowTreeViewProvider
  implements vscode.TreeDataProvider<FlowTreeItem>
{
  private _onDidChangeTreeData = new vscode.EventEmitter<
    FlowTreeItem | undefined | null | void
  >();
  readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

  private currentResponse: FocusModeResponse | null = null;
  private currentDocumentUri: string | null = null;

  constructor(private navigationManager: NavigationManager) {}

  refresh(): void {
    this._onDidChangeTreeData.fire();
  }

  updateFromFocusMode(
    documentUri: string,
    response: FocusModeResponse | null
  ): void {
    this.currentDocumentUri = documentUri;
    this.currentResponse = response;
    this.refresh();
  }

  clear(): void {
    this.currentResponse = null;
    this.currentDocumentUri = null;
    this.refresh();
  }

  getTreeItem(element: FlowTreeItem): vscode.TreeItem {
    return element;
  }

  getChildren(element?: FlowTreeItem): Thenable<FlowTreeItem[]> {
    if (!this.currentResponse || !this.navigationManager.hasLocations()) {
      return Promise.resolve([
        new FlowTreeItem(
          "No flow analysis active",
          "",
          vscode.TreeItemCollapsibleState.None,
          "empty"
        ),
      ]);
    }

    if (!element) {
      return Promise.resolve(this.getRootItems());
    }

    if (element.contextValue === "group" && element.children) {
      return Promise.resolve(element.children);
    }

    return Promise.resolve([]);
  }

  private getRootItems(): FlowTreeItem[] {
    if (!this.currentResponse || !this.navigationManager.hasLocations()) {
      return [];
    }

    const items: FlowTreeItem[] = [];
    const locations = this.navigationManager.getLocations();
    const currentIndex = this.navigationManager.getCurrentIndex();
    const focusedSymbol = this.currentResponse.focusedPlace.name;

    const positionText =
      currentIndex >= 0
        ? `${currentIndex + 1}/${locations.length}`
        : `${locations.length} location${locations.length === 1 ? "" : "s"}`;

    items.push(
      new FlowTreeItem(
        focusedSymbol,
        positionText,
        vscode.TreeItemCollapsibleState.None,
        "header"
      )
    );

    const grouped = this.groupLocationsByMethod(locations, currentIndex);

    for (const group of grouped) {
      if (group.locations.length === 1) {
        const location = group.locations[0];
        const isCurrent = location.index === currentIndex;
        items.push(
          this.createLocationItem(location.location, isCurrent, location.index)
        );
      } else {
        const children = group.locations.map((loc) =>
          this.createLocationItem(
            loc.location,
            loc.index === currentIndex,
            loc.index
          )
        );

        const groupItem = new FlowTreeItem(
          group.methodName,
          `${group.locations.length} location${
            group.locations.length > 1 ? "s" : ""
          }`,
          vscode.TreeItemCollapsibleState.Expanded,
          "group",
          undefined,
          children
        );
        groupItem.iconPath = new vscode.ThemeIcon("symbol-method");
        items.push(groupItem);
      }
    }

    return items;
  }

  private groupLocationsByMethod(
    locations: NavigableLocation[],
    currentIndex: number
  ): Array<{
    methodName: string;
    locations: Array<{ location: NavigableLocation; index: number }>;
  }> {
    const groups = new Map<
      string,
      Array<{ location: NavigableLocation; index: number }>
    >();

    locations.forEach((location, index) => {
      const methodName = location.summary || this.extractMethodName(location);

      console.log(
        `SharpFocus[tree]: grouping location at line ${
          location.range.start.line + 1
        } under method "${methodName}"`
      );

      if (!groups.has(methodName)) {
        groups.set(methodName, []);
      }
      groups.get(methodName)!.push({ location, index });
    });

    const result = Array.from(groups.entries()).map(([methodName, locs]) => ({
      methodName,
      locations: locs,
    }));

    console.log(
      `SharpFocus[tree]: created ${result.length} groups:`,
      result.map((g) => `${g.methodName} (${g.locations.length})`)
    );

    return result;
  }

  private extractMethodName(location: NavigableLocation): string {
    return `Line ${location.range.start.line + 1}`;
  }

  private createLocationItem(
    location: NavigableLocation,
    isCurrent: boolean,
    index: number
  ): FlowTreeItem {
    const lineNumber = location.range.start.line + 1;
    const label = `Line ${lineNumber}`;
    const description = location.description;

    const item = new FlowTreeItem(
      label,
      description,
      vscode.TreeItemCollapsibleState.None,
      isCurrent ? "currentLocation" : "location",
      location
    );

    const symbolInfo = location.detail ? ` (${location.detail})` : "";
    item.tooltip = new vscode.MarkdownString(
      `**Line ${lineNumber}**${symbolInfo}\n\n` +
        `${location.relation} reference\n\n` +
        `Click to navigate · ${
          isCurrent ? "**Current location**" : "Ctrl+Alt+N/P to navigate"
        }`
    );

    item.command = {
      command: "sharpfocus.navigateToLocation",
      title: "Navigate to Location",
      arguments: [location],
    };

    return item;
  }

  private getRelationBadge(relation: string): string {
    switch (relation) {
      case "Source":
        return "[Source]";
      case "Transform":
        return "[Transform]";
      case "Sink":
        return "[Sink]";
      default:
        return "";
    }
  }
}

class FlowTreeItem extends vscode.TreeItem {
  public children?: FlowTreeItem[];

  constructor(
    public readonly label: string,
    public readonly description: string,
    public readonly collapsibleState: vscode.TreeItemCollapsibleState,
    public readonly contextValue: string,
    public readonly location?: NavigableLocation,
    children?: FlowTreeItem[]
  ) {
    super(label, collapsibleState);
    this.description = description;
    this.contextValue = contextValue;
    this.children = children;

    switch (contextValue) {
      case "header":
        this.iconPath = new vscode.ThemeIcon(
          "symbol-field",
          new vscode.ThemeColor("symbolIcon.fieldForeground")
        );
        break;
      case "currentLocation":
        if (location) {
          this.iconPath = this.getLocationIcon(location.category);
        }
        this.accessibilityInformation = {
          label: `Current location: ${label}`,
          role: "treeitem",
        };
        break;
      case "location":
        if (location) {
          this.iconPath = this.getLocationIcon(location.category);
        }
        break;
      case "empty":
        this.iconPath = new vscode.ThemeIcon("info");
        break;
    }
  }

  private getLocationIcon(category: "source" | "transform" | "sink"): {
    light: vscode.Uri;
    dark: vscode.Uri;
  } {
    const iconName = `${category}-icon`;
    return {
      light: vscode.Uri.file(
        path.join(__dirname, "..", "resources", `${iconName}-light.svg`)
      ),
      dark: vscode.Uri.file(
        path.join(__dirname, "..", "resources", `${iconName}-dark.svg`)
      ),
    };
  }
}
