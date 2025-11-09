import * as vscode from "vscode";
import { SharpFocusCodeLensProvider } from "./codeLensProvider";
import { FocusModeRenderer } from "./focusModeRenderer";
import { FocusSessionBuilder } from "./focusSessionBuilder";
import { SharpFocusHoverProvider } from "./hoverProvider";
import { AnalysisStatusBar } from "./statusBar";
import { FocusModeResponse } from "./types";

export class FocusModeController {
  constructor(
    private readonly sessionBuilder: FocusSessionBuilder,
    private readonly renderer: FocusModeRenderer,
    private readonly codeLensProvider: SharpFocusCodeLensProvider,
    private readonly hoverProvider: SharpFocusHoverProvider,
    private readonly statusBar: AnalysisStatusBar
  ) {}

  apply(editor: vscode.TextEditor, response: FocusModeResponse | null): void {
    const session = this.sessionBuilder.create(editor, response);
    console.info("SharpFocus[focus]: controller apply", {
      document: editor.document.uri.toString(),
      hasResponse: !!response,
      hasSession: !!session,
      focusedPlace: response?.focusedPlace?.name,
    });
    this.renderer.render(editor, session);

    const documentUri = editor.document.uri.toString();
    this.codeLensProvider.updateFocusMode(documentUri, response ?? null);
    this.hoverProvider.updateFocusMode(documentUri, response ?? null);

    if (session && response) {
      this.statusBar.show({
        symbolName: response.focusedPlace.name,
        sourceCount: session.relationCounts.source,
        transformCount: session.relationCounts.transform,
        sinkCount: session.relationCounts.sink,
      });
    } else {
      this.statusBar.hide();
    }
  }

  clear(editor: vscode.TextEditor): void {
    this.apply(editor, null);
  }

  clearAll(): void {
    this.renderer.clearAll();
    this.codeLensProvider.clear();
    this.hoverProvider.clear();
    this.statusBar.hide();
  }
}
