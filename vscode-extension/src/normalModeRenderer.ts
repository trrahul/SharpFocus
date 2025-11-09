import * as vscode from "vscode";
import { FocusSession } from "./focusSession";

export class NormalModeRenderer {
  private hideDecorationType: vscode.TextEditorDecorationType;
  private seedDecorationType: vscode.TextEditorDecorationType;

  private sessionsByDocument = new Map<string, FocusSession>();
  private documentCloseListener: vscode.Disposable;

  constructor() {
    this.hideDecorationType = vscode.window.createTextEditorDecorationType({
      opacity: "0.3",
    });

    this.seedDecorationType = vscode.window.createTextEditorDecorationType({
      dark: {
        backgroundColor: "rgba(255, 255, 255, 0.1)",
      },
      light: {
        backgroundColor: "rgba(0, 0, 0, 0.1)",
      },
    });

    this.documentCloseListener = vscode.workspace.onDidCloseTextDocument(
      (document) => {
        const uri = document.uri.toString();
        if (this.sessionsByDocument.has(uri)) {
          console.debug(
            "SharpFocus[focus]: clearing normal session for closed document",
            uri
          );
          this.sessionsByDocument.delete(uri);
        }
      }
    );
  }

  render(editor: vscode.TextEditor, session: FocusSession | null): void {
    const uri = editor.document.uri.toString();

    if (!session) {
      this.clear(editor);
      this.sessionsByDocument.delete(uri);
      console.debug(
        "SharpFocus[focus]: normal mode - no session, clearing decorations",
        {
          document: uri,
        }
      );
      return;
    }

    this.sessionsByDocument.set(uri, session);
    this.applyDecorations(editor, session);

    console.debug("SharpFocus[focus]: normal decorations applied", {
      document: uri,
      focusRanges: session.focusSpans.length,
      fadeRanges: session.fadeRanges.length,
    });
  }

  reapplyHighlights(editor?: vscode.TextEditor): void {
    const targetEditors = editor ? [editor] : vscode.window.visibleTextEditors;

    for (const textEditor of targetEditors) {
      if (textEditor.document.languageId !== "csharp") {
        continue;
      }

      const uri = textEditor.document.uri.toString();
      const session = this.sessionsByDocument.get(uri);

      if (session) {
        this.applyDecorations(textEditor, session);
      }
    }
  }

  private applyDecorations(
    editor: vscode.TextEditor,
    session: FocusSession
  ): void {
    editor.setDecorations(this.hideDecorationType, session.fadeRanges);

    const seedDecorations = session.seedRange
      ? [{ range: session.seedRange }]
      : [];
    editor.setDecorations(this.seedDecorationType, seedDecorations);
  }

  clear(editor: vscode.TextEditor): void {
    editor.setDecorations(this.hideDecorationType, []);
    editor.setDecorations(this.seedDecorationType, []);

    const uri = editor.document.uri.toString();
    this.sessionsByDocument.delete(uri);
  }

  clearAll(): void {
    for (const editor of vscode.window.visibleTextEditors) {
      if (editor.document.languageId === "csharp") {
        this.clear(editor);
      }
    }

    this.sessionsByDocument.clear();
  }

  dispose(): void {
    this.documentCloseListener.dispose();
    this.hideDecorationType.dispose();
    this.seedDecorationType.dispose();
    this.sessionsByDocument.clear();
  }
}
