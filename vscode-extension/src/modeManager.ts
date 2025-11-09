import * as vscode from "vscode";

export type AnalysisMode = "focus" | "off";

export class ModeManager {
  getAnalysisMode(): AnalysisMode {
    const config = vscode.workspace.getConfiguration("sharpfocus");
    return config.get<AnalysisMode>("analysisMode", "focus");
  }

  async setAnalysisMode(mode: AnalysisMode): Promise<void> {
    const config = vscode.workspace.getConfiguration("sharpfocus");
    await config.update(
      "analysisMode",
      mode,
      vscode.ConfigurationTarget.Global
    );
  }

  async toggleMode(): Promise<AnalysisMode> {
    const currentMode = this.getAnalysisMode();
    const modes: AnalysisMode[] = ["focus", "off"];
    const currentIndex = modes.indexOf(currentMode);
    const nextIndex = (currentIndex + 1) % modes.length;
    const nextMode = modes[nextIndex];

    await this.setAnalysisMode(nextMode);

    const modeLabels = {
      focus: "Focus Mode (fade irrelevant code)",
      off: "Off",
    };

    vscode.window.showInformationMessage(
      `SharpFocus: Switched to ${modeLabels[nextMode]}`
    );

    return nextMode;
  }
}
