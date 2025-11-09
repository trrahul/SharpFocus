import * as vscode from "vscode";

export interface FocusStatusBarState {
  symbolName: string;
  sourceCount: number;
  transformCount: number;
  sinkCount: number;
}

export class AnalysisStatusBar implements vscode.Disposable {
  private readonly statusBarItem: vscode.StatusBarItem;
  private isDegraded = false;

  constructor() {
    this.statusBarItem = vscode.window.createStatusBarItem(
      vscode.StatusBarAlignment.Right,
      100
    );
    this.statusBarItem.tooltip = "SharpFocus focus mode summary";
  }

  show(state: FocusStatusBarState): void {
    const parts: string[] = [`$(symbol-field) ${state.symbolName}`];

    if (state.sourceCount > 0) {
      parts.push(`${state.sourceCount} sources`);
    }
    if (state.transformCount > 0) {
      parts.push(`${state.transformCount} transforms`);
    }
    if (state.sinkCount > 0) {
      parts.push(`${state.sinkCount} sinks`);
    }

    this.statusBarItem.text = parts.join(" · ");
    this.statusBarItem.show();
  }

  setLoading(message: string = "Analyzing..."): void {
    this.statusBarItem.text = `$(sync~spin) ${message}`;
    this.statusBarItem.show();
  }

  setIdle(): void {
    this.statusBarItem.hide();
  }

  setDegraded(): void {
    this.isDegraded = true;
    this.statusBarItem.text = "$(warning) SharpFocus: Degraded";
    this.statusBarItem.tooltip =
      "Language server not responding. Click to restart.";
    this.statusBarItem.command = "sharpfocus.restartServer";
    this.statusBarItem.backgroundColor = new vscode.ThemeColor(
      "statusBarItem.warningBackground"
    );
    this.statusBarItem.show();
  }

  clearDegraded(): void {
    if (this.isDegraded) {
      this.isDegraded = false;
      this.statusBarItem.command = undefined;
      this.statusBarItem.backgroundColor = undefined;
      this.statusBarItem.tooltip = "SharpFocus focus mode summary";
    }
  }

  hide(): void {
    this.statusBarItem.hide();
  }

  dispose(): void {
    this.statusBarItem.dispose();
  }
}
