import * as vscode from "vscode";
import { NavigableLocation, NavigationManager } from "./navigationManager";
import { FocusModeResponse } from "./types";

export async function showFlowDetailsQuickPick(
  documentUri: string,
  focusMode: FocusModeResponse,
  navigationManager: NavigationManager
): Promise<void> {
  const locations = navigationManager.getLocations();
  const currentIndex = navigationManager.getCurrentIndex();

  if (locations.length === 0) {
    vscode.window.showInformationMessage(
      `No data flow connections found for ${focusMode.focusedPlace.name}`
    );
    return;
  }

  const items = createQuickPickItems(
    locations,
    focusMode.focusedPlace.name,
    currentIndex
  );

  const selected = await vscode.window.showQuickPick(items, {
    placeHolder: `Data flow for ${focusMode.focusedPlace.name} · ${
      locations.length
    } ${locations.length === 1 ? "location" : "locations"}`,
    matchOnDescription: true,
    matchOnDetail: true,
  });

  if (selected) {
    if (selected.action === "clear") {
      await vscode.commands.executeCommand("sharpfocus.clearHighlights");
    } else if (selected.action === "next") {
      await vscode.commands.executeCommand("sharpfocus.navigateNext");
    } else if (selected.action === "previous") {
      await vscode.commands.executeCommand("sharpfocus.navigatePrevious");
    } else if (selected.location) {
      await navigationManager.navigateToLocation(selected.location);
    }
  }
}

function createQuickPickItems(
  locations: NavigableLocation[],
  symbolName: string,
  currentIndex: number
): Array<
  vscode.QuickPickItem & { location?: NavigableLocation; action?: string }
> {
  const items: Array<
    vscode.QuickPickItem & { location?: NavigableLocation; action?: string }
  > = [];

  items.push({
    label: "Navigation",
    kind: vscode.QuickPickItemKind.Separator,
  });

  items.push({
    label: "$(arrow-down) Next Location",
    description: "Ctrl+Alt+N",
    detail: "Jump to the next flow location",
    action: "next",
  });

  items.push({
    label: "$(arrow-up) Previous Location",
    description: "Ctrl+Alt+P",
    detail: "Jump to the previous flow location",
    action: "previous",
  });

  const sources = locations.filter((l) => l.category === "source");
  const transforms = locations.filter((l) => l.category === "transform");
  const sinks = locations.filter((l) => l.category === "sink");

  if (sources.length > 0) {
    items.push({
      label: `Sources (${sources.length})`,
      kind: vscode.QuickPickItemKind.Separator,
    });

    sources.forEach((loc, index) => {
      const locationIndex = locations.indexOf(loc);
      const isCurrent = locationIndex === currentIndex;
      const position = locationIndex + 1;

      items.push({
        label: `${isCurrent ? "$(arrow-small-right) " : ""}${loc.label}${
          isCurrent ? ` · ${position}/${locations.length}` : ""
        }`,
        description: loc.description,
        detail: loc.detail,
        location: loc,
      });
    });
  }

  if (transforms.length > 0) {
    items.push({
      label: `Transforms (${transforms.length})`,
      kind: vscode.QuickPickItemKind.Separator,
    });

    transforms.forEach((loc, index) => {
      const locationIndex = locations.indexOf(loc);
      const isCurrent = locationIndex === currentIndex;
      const position = locationIndex + 1;

      items.push({
        label: `${isCurrent ? "$(arrow-small-right) " : ""}$(sync) ${
          loc.label
        }${isCurrent ? ` · ${position}/${locations.length}` : ""}`,
        description: loc.description,
        detail: loc.detail,
        location: loc,
      });
    });
  }

  if (sinks.length > 0) {
    items.push({
      label: `Destinations (${sinks.length})`,
      kind: vscode.QuickPickItemKind.Separator,
    });

    sinks.forEach((loc, index) => {
      const locationIndex = locations.indexOf(loc);
      const isCurrent = locationIndex === currentIndex;
      const position = locationIndex + 1;

      items.push({
        label: `${isCurrent ? "$(arrow-small-right) " : ""}${loc.label}${
          isCurrent ? ` · ${position}/${locations.length}` : ""
        }`,
        description: loc.description,
        detail: loc.detail,
        location: loc,
      });
    });
  }

  items.push({
    label: "Actions",
    kind: vscode.QuickPickItemKind.Separator,
  });

  items.push({
    label: "$(close) Clear Highlights",
    description: "Exit focus mode",
    detail: "Remove all data flow highlighting",
    action: "clear",
  });

  return items;
}
