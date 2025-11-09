import * as vscode from "vscode";

export class WordHighlightManager {
  private readonly trackedSettings: Array<{
    readonly key: "occurrencesHighlight" | "selectionHighlight";
    readonly disableValue: boolean;
  }> = [
    { key: "occurrencesHighlight", disableValue: false },
    { key: "selectionHighlight", disableValue: false },
  ];

  private readonly savedSettings = new Map<string, unknown>();
  private isDisabled = false;

  async disable(): Promise<void> {
    if (this.isDisabled) {
      return;
    }

    const config = vscode.workspace.getConfiguration("editor");

    try {
      for (const setting of this.trackedSettings) {
        const currentValue = config.get(setting.key);
        this.savedSettings.set(setting.key, currentValue);
      }

      const updates = this.trackedSettings.map((setting) =>
        config.update(
          setting.key,
          setting.disableValue,
          vscode.ConfigurationTarget.Workspace
        )
      );

      await Promise.all(updates);
      this.isDisabled = true;
    } catch (error) {
      console.warn("SharpFocus: Failed to disable word highlights", error);
    }
  }

  async restore(): Promise<void> {
    if (!this.isDisabled) {
      return;
    }

    const config = vscode.workspace.getConfiguration("editor");

    try {
      const updates = [...this.savedSettings.entries()].map(([key, value]) =>
        config.update(key, value, vscode.ConfigurationTarget.Workspace)
      );

      await Promise.all(updates);
    } catch (error) {
      console.warn(
        "SharpFocus: Failed to restore word highlight settings",
        error
      );
    } finally {
      this.savedSettings.clear();
      this.isDisabled = false;
    }
  }

  dispose(): void {
    void this.restore();
  }
}
