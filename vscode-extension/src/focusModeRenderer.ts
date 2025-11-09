import * as vscode from "vscode";
import { AdvancedModeRenderer } from "./advancedModeRenderer";
import { FocusSession } from "./focusSession";
import { NormalModeRenderer } from "./normalModeRenderer";

type DisplayMode = "normal" | "advanced";

export class FocusModeRenderer {
  private readonly advancedRenderer = new AdvancedModeRenderer();
  private readonly normalRenderer = new NormalModeRenderer();
  private lastMode: DisplayMode | null = null;

  render(editor: vscode.TextEditor, session: FocusSession | null): void {
    const { active, inactive } = this.getRenderers();
    active.render(editor, session);
    inactive.clear(editor);
  }

  reapplyHighlights(editor?: vscode.TextEditor): void {
    const { active } = this.getRenderers();
    active.reapplyHighlights(editor);
  }

  clear(editor: vscode.TextEditor): void {
    this.normalRenderer.clear(editor);
    this.advancedRenderer.clear(editor);
  }

  clearAll(): void {
    this.normalRenderer.clearAll();
    this.advancedRenderer.clearAll();
  }

  dispose(): void {
    this.normalRenderer.dispose();
    this.advancedRenderer.dispose();
  }

  private getRenderers(): {
    active: NormalModeRenderer | AdvancedModeRenderer;
    inactive: NormalModeRenderer | AdvancedModeRenderer;
  } {
    const mode = this.getDisplayMode();

    if (mode !== this.lastMode) {
      this.normalRenderer.clearAll();
      this.advancedRenderer.clearAll();
      this.lastMode = mode;
    }

    if (mode === "advanced") {
      return {
        active: this.advancedRenderer,
        inactive: this.normalRenderer,
      };
    }

    return {
      active: this.normalRenderer,
      inactive: this.advancedRenderer,
    };
  }

  private getDisplayMode(): DisplayMode {
    const config = vscode.workspace.getConfiguration("sharpfocus");
    const value = config.get<DisplayMode>("displayMode", "normal");
    return value === "advanced" ? "advanced" : "normal";
  }
}
