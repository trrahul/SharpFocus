import * as vscode from "vscode";
import { FocusDecorationFactory } from "./focusDecorations";
import { FocusSession } from "./focusSession";

export class AdvancedModeRenderer {
  private fadeDecorationType: vscode.TextEditorDecorationType;
  private seedDecorationType: vscode.TextEditorDecorationType;
  private sourceDecorationType: vscode.TextEditorDecorationType;
  private transformDecorationType: vscode.TextEditorDecorationType;
  private sinkDecorationType: vscode.TextEditorDecorationType;
  private relatedDecorationType: vscode.TextEditorDecorationType;
  private readonly decorations = new FocusDecorationFactory();

  private sessionsByDocument = new Map<string, FocusSession>();
  private documentCloseListener: vscode.Disposable;

  constructor() {
    this.fadeDecorationType = vscode.window.createTextEditorDecorationType({
      opacity: "0.35",
    });

    this.documentCloseListener = vscode.workspace.onDidCloseTextDocument(
      (document) => {
        const uri = document.uri.toString();
        if (this.sessionsByDocument.has(uri)) {
          console.debug(
            "SharpFocus[focus]: clearing advanced session for closed document",
            uri
          );
          this.sessionsByDocument.delete(uri);
        }
      }
    );

    this.seedDecorationType = vscode.window.createTextEditorDecorationType({
      border: "2px double rgba(255, 215, 0, 0.9)",
      borderRadius: "3px",
      gutterIconSize: "contain",
      dark: {
        backgroundColor: "rgba(255, 215, 0, 0.18)",
        gutterIconPath: vscode.Uri.file(
          __dirname + "/../resources/seed-icon-dark.svg"
        ),
      },
      light: {
        backgroundColor: "rgba(255, 215, 0, 0.22)",
        gutterIconPath: vscode.Uri.file(
          __dirname + "/../resources/seed-icon-light.svg"
        ),
      },
    });

    this.sourceDecorationType = vscode.window.createTextEditorDecorationType({
      border: "1.5px dashed rgba(255, 140, 0, 0.7)",
      borderRadius: "2px",
      gutterIconSize: "contain",
      dark: {
        backgroundColor: "rgba(255, 140, 0, 0.12)",
        gutterIconPath: vscode.Uri.file(
          __dirname + "/../resources/source-icon-dark.svg"
        ),
      },
      light: {
        backgroundColor: "rgba(255, 140, 0, 0.15)",
        gutterIconPath: vscode.Uri.file(
          __dirname + "/../resources/source-icon-light.svg"
        ),
      },
    });

    this.transformDecorationType = vscode.window.createTextEditorDecorationType(
      {
        border: "1.5px dotted rgba(200, 162, 255, 0.7)",
        borderRadius: "2px",
        gutterIconSize: "contain",
        dark: {
          backgroundColor: "rgba(200, 162, 255, 0.12)",
          gutterIconPath: vscode.Uri.file(
            __dirname + "/../resources/transform-icon-dark.svg"
          ),
        },
        light: {
          backgroundColor: "rgba(200, 162, 255, 0.15)",
          gutterIconPath: vscode.Uri.file(
            __dirname + "/../resources/transform-icon-light.svg"
          ),
        },
      }
    );

    this.sinkDecorationType = vscode.window.createTextEditorDecorationType({
      border: "1.5px solid rgba(0, 206, 209, 0.7)",
      borderRadius: "2px",
      gutterIconSize: "contain",
      dark: {
        backgroundColor: "rgba(64, 224, 208, 0.12)",
        gutterIconPath: vscode.Uri.file(
          __dirname + "/../resources/sink-icon-dark.svg"
        ),
      },
      light: {
        backgroundColor: "rgba(64, 224, 208, 0.15)",
        gutterIconPath: vscode.Uri.file(
          __dirname + "/../resources/sink-icon-light.svg"
        ),
      },
    });

    this.relatedDecorationType = vscode.window.createTextEditorDecorationType({
      dark: {
        backgroundColor: "rgba(255, 255, 255, 0.08)",
      },
      light: {
        backgroundColor: "rgba(0, 0, 0, 0.05)",
      },
      border: "1px solid rgba(128, 128, 128, 0.4)",
      borderRadius: "2px",
    });
  }

  render(editor: vscode.TextEditor, session: FocusSession | null): void {
    const uri = editor.document.uri.toString();

    if (!session) {
      this.clear(editor);
      this.sessionsByDocument.delete(uri);
      console.debug(
        "SharpFocus[focus]: advanced mode - no session, clearing decorations",
        {
          document: uri,
        }
      );
      return;
    }

    this.sessionsByDocument.set(uri, session);
    this.applyDecorations(editor, session);

    console.debug("SharpFocus[focus]: advanced decorations applied", {
      document: uri,
      focusRanges: session.focusSpans.length,
      fadeRanges: session.fadeRanges.length,
      relationCounts: session.relationCounts,
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
    editor.setDecorations(this.fadeDecorationType, session.fadeRanges);
    console.debug("SharpFocus[focus]: advanced fade ranges set", {
      count: session.fadeRanges.length,
    });

    const seedOptions = this.decorations.createSeedDecorations(
      session.targetSymbol,
      session.seedRange
    );
    editor.setDecorations(this.seedDecorationType, seedOptions);

    const decorationsByRelation =
      this.decorations.createFocusDecorationsByRelation(
        editor.document,
        session.targetSymbol,
        session.focusSpans
      );

    editor.setDecorations(
      this.sourceDecorationType,
      decorationsByRelation.source
    );
    editor.setDecorations(
      this.transformDecorationType,
      decorationsByRelation.transform
    );
    editor.setDecorations(this.sinkDecorationType, decorationsByRelation.sink);
    editor.setDecorations(
      this.relatedDecorationType,
      decorationsByRelation.related
    );
  }

  clear(editor: vscode.TextEditor): void {
    editor.setDecorations(this.fadeDecorationType, []);
    editor.setDecorations(this.seedDecorationType, []);
    editor.setDecorations(this.sourceDecorationType, []);
    editor.setDecorations(this.transformDecorationType, []);
    editor.setDecorations(this.sinkDecorationType, []);
    editor.setDecorations(this.relatedDecorationType, []);

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
    this.fadeDecorationType.dispose();
    this.seedDecorationType.dispose();
    this.sourceDecorationType.dispose();
    this.transformDecorationType.dispose();
    this.sinkDecorationType.dispose();
    this.relatedDecorationType.dispose();
    this.sessionsByDocument.clear();
  }
}
