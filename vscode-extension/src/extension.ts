import * as vscode from "vscode";
import { LanguageClient } from "vscode-languageclient/node";
import { SharpFocusCodeLensProvider } from "./codeLensProvider";
import { showFlowDetailsQuickPick } from "./flowDetailsQuickPick";
import { FlowTreeViewProvider } from "./flowTreeViewProvider";
import { FocusModeController } from "./focusModeController";
import { FocusModeRenderer } from "./focusModeRenderer";
import { FocusSessionBuilder } from "./focusSessionBuilder";
import { SharpFocusHoverProvider } from "./hoverProvider";
import { LanguageServerManager } from "./languageServerManager";
import { ModeManager } from "./modeManager";
import { NavigationManager } from "./navigationManager";
import { AnalysisStatusBar } from "./statusBar";
import { FocusModeResponse } from "./types";
import { WordHighlightManager } from "./wordHighlightManager";

let client: LanguageClient | undefined;
let languageServerManager: LanguageServerManager;
let modeManager: ModeManager;
let focusModeRenderer: FocusModeRenderer;
let focusSessionBuilder: FocusSessionBuilder;
let focusModeController: FocusModeController;
let codeLensProvider: SharpFocusCodeLensProvider;
let hoverProvider: SharpFocusHoverProvider;
let statusBar: AnalysisStatusBar;
let wordHighlightManager: WordHighlightManager;
let navigationManager: NavigationManager;
let flowTreeViewProvider: FlowTreeViewProvider;
let suppressSelectionHandling = false;

let focusModeTimeout: NodeJS.Timeout | undefined;
let pendingFocusRequest: AbortController | undefined;
let requestSequence = 0;
let consecutiveFailures = 0;
const MAX_FAILURES_BEFORE_CIRCUIT_BREAK = 5;
const NAVIGATION_SUPPRESSION_DELAY_MS = 400;

export function activate(context: vscode.ExtensionContext) {
  console.log("SharpFocus: activating");

  (global as any).extensionContext = context;

  languageServerManager = new LanguageServerManager();
  modeManager = new ModeManager();
  focusModeRenderer = new FocusModeRenderer();
  focusSessionBuilder = new FocusSessionBuilder();
  codeLensProvider = new SharpFocusCodeLensProvider();
  hoverProvider = new SharpFocusHoverProvider();
  statusBar = new AnalysisStatusBar();
  wordHighlightManager = new WordHighlightManager();
  navigationManager = new NavigationManager();
  flowTreeViewProvider = new FlowTreeViewProvider(navigationManager);
  focusModeController = new FocusModeController(
    focusSessionBuilder,
    focusModeRenderer,
    codeLensProvider,
    hoverProvider,
    statusBar
  );

  navigationManager.onNavigationChange(() => {
    flowTreeViewProvider.refresh();
  });

  navigationManager.onBeforeNavigate(() => {
    suppressSelectionHandling = true;

    if (focusModeTimeout) {
      clearTimeout(focusModeTimeout);
      focusModeTimeout = undefined;
    }

    if (pendingFocusRequest) {
      pendingFocusRequest.abort();
      pendingFocusRequest = undefined;
    }
  });

  navigationManager.onAfterNavigate(() => {
    const activeEditor = vscode.window.activeTextEditor;
    if (activeEditor) {
      focusModeRenderer.reapplyHighlights(activeEditor);
    }

    setTimeout(() => {
      suppressSelectionHandling = false;
    }, NAVIGATION_SUPPRESSION_DELAY_MS);
  });

  applyUiSettings();

  const treeView = vscode.window.createTreeView("sharpfocus.flowTree", {
    treeDataProvider: flowTreeViewProvider,
  });

  context.subscriptions.push(
    statusBar,
    wordHighlightManager,
    codeLensProvider,
    treeView,
    { dispose: () => focusModeRenderer.dispose() },
    { dispose: () => hoverProvider.clear() }
  );

  context.subscriptions.push(
    vscode.languages.registerCodeLensProvider(
      { language: "csharp" },
      codeLensProvider
    ),
    vscode.languages.registerHoverProvider(
      { language: "csharp" },
      hoverProvider
    ),
    new vscode.Disposable(() => {
      if (focusModeTimeout) {
        clearTimeout(focusModeTimeout);
        focusModeTimeout = undefined;
      }
    })
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("sharpfocus.toggleMode", async () => {
      const nextMode = await modeManager.toggleMode();
      clearHighlights();

      if (nextMode === "focus") {
        const editor = vscode.window.activeTextEditor;
        if (shouldAnalyze(editor)) {
          scheduleFocusMode(editor);
        }
      }
    }),
    vscode.commands.registerCommand("sharpfocus.clearHighlights", () => {
      clearHighlights();
    }),
    vscode.commands.registerCommand(
      "sharpfocus.showFlowDetails",
      async (documentUri: string, focusMode: FocusModeResponse) => {
        navigationManager.initialize(documentUri, focusMode);
        await showFlowDetailsQuickPick(
          documentUri,
          focusMode,
          navigationManager
        );
      }
    ),
    vscode.commands.registerCommand("sharpfocus.navigateNext", async () => {
      if (navigationManager.hasLocations()) {
        await navigationManager.navigateNext();
      } else {
        vscode.window.showInformationMessage(
          "No flow locations to navigate. Click on a symbol first."
        );
      }
    }),
    vscode.commands.registerCommand("sharpfocus.navigatePrevious", async () => {
      if (navigationManager.hasLocations()) {
        await navigationManager.navigatePrevious();
      } else {
        vscode.window.showInformationMessage(
          "No flow locations to navigate. Click on a symbol first."
        );
      }
    }),
    vscode.commands.registerCommand(
      "sharpfocus.navigateToLocation",
      async (location: any) => {
        if (navigationManager.hasLocations() && location) {
          await navigationManager.navigateToLocation(location);
        }
      }
    ),
    vscode.commands.registerCommand("sharpfocus.restartServer", async () => {
      await restartLanguageServer();
    })
  );

  context.subscriptions.push(
    vscode.window.onDidChangeTextEditorSelection((event) => {
      if (suppressSelectionHandling) {
        return;
      }

      if (shouldAnalyze(event.textEditor)) {
        scheduleFocusMode(event.textEditor);
      }
    }),
    vscode.window.onDidChangeActiveTextEditor((editor) => {
      if (suppressSelectionHandling) {
        return;
      }

      if (editor && editor.document.languageId === "csharp") {
        focusModeRenderer.reapplyHighlights(editor);
      }

      if (shouldAnalyze(editor)) {
        scheduleFocusMode(editor);
      } else if (editor) {
        focusModeController.clear(editor);
      } else {
        clearHighlights();
      }
    }),
    vscode.window.onDidChangeVisibleTextEditors(() => {
      focusModeRenderer.reapplyHighlights();
    })
  );

  context.subscriptions.push(
    vscode.workspace.onDidChangeConfiguration((event) => {
      if (event.affectsConfiguration("sharpfocus.ui.disableWordHighlight")) {
        applyUiSettings();
      }

      if (event.affectsConfiguration("sharpfocus.analysisMode")) {
        clearHighlights();
        const editor = vscode.window.activeTextEditor;
        if (shouldAnalyze(editor)) {
          scheduleFocusMode(editor);
        }
      }

      if (event.affectsConfiguration("sharpfocus.displayMode")) {
        focusModeRenderer.clearAll();
        const editor = vscode.window.activeTextEditor;
        if (shouldAnalyze(editor)) {
          scheduleFocusMode(editor);
        }
      }
    })
  );

  try {
    const startedClient = languageServerManager.start(context);
    client = startedClient;
    setTimeout(() => {
      const editor = vscode.window.activeTextEditor;
      if (shouldAnalyze(editor)) {
        scheduleFocusMode(editor);
      }
    }, 0);
    console.log("SharpFocus: language server started");
  } catch (error) {
    console.error("SharpFocus: failed to start language server", error);
    vscode.window.showErrorMessage(
      `SharpFocus: Failed to start language server: ${error}`
    );
  }
}

export function deactivate(): Thenable<void> | undefined {
  if (focusModeTimeout) {
    clearTimeout(focusModeTimeout);
    focusModeTimeout = undefined;
  }

  return languageServerManager?.stop();
}

function scheduleFocusMode(editor: vscode.TextEditor): void {
  if (focusModeTimeout) {
    clearTimeout(focusModeTimeout);
  }

  if (pendingFocusRequest) {
    pendingFocusRequest.abort();
    pendingFocusRequest = undefined;
  }

  focusModeTimeout = setTimeout(() => {
    focusModeTimeout = undefined;
    if (shouldAnalyze(editor)) {
      void runFocusMode(editor);
    }
  }, 300);
}

async function runFocusMode(editor: vscode.TextEditor): Promise<void> {
  if (!client) {
    focusModeController.clear(editor);
    return;
  }

  if (consecutiveFailures >= MAX_FAILURES_BEFORE_CIRCUIT_BREAK) {
    console.warn("SharpFocus: circuit breaker open, skipping request");
    statusBar.setDegraded();
    return;
  }

  const abortController = new AbortController();
  pendingFocusRequest = abortController;
  const currentSequence = ++requestSequence;

  const position = editor.selection.active;
  const request = {
    textDocument: {
      uri: editor.document.uri.toString(),
    },
    position: {
      line: position.line,
      character: position.character,
    },
  };

  try {
    const response = await client.sendRequest<FocusModeResponse | null>(
      "sharpfocus/focusMode",
      request
    );

    if (abortController.signal.aborted) {
      console.log("SharpFocus: request cancelled");
      return;
    }

    if (currentSequence !== requestSequence) {
      console.log("SharpFocus: request superseded by newer request");
      return;
    }

    if (pendingFocusRequest === abortController) {
      pendingFocusRequest = undefined;
    }

    consecutiveFailures = 0;
    statusBar.clearDegraded();

    focusModeController.apply(editor, response ?? null);

    if (response) {
      navigationManager.initialize(editor.document.uri.toString(), response);
      flowTreeViewProvider.updateFromFocusMode(
        editor.document.uri.toString(),
        response
      );
    } else {
      navigationManager.clear();
      flowTreeViewProvider.clear();
    }
  } catch (error) {
    if (abortController.signal.aborted) {
      console.log("SharpFocus: request aborted");
      return;
    }

    consecutiveFailures++;

    console.error("SharpFocus: focus mode request failed", error);

    if (consecutiveFailures >= MAX_FAILURES_BEFORE_CIRCUIT_BREAK) {
      const action = await vscode.window.showErrorMessage(
        `SharpFocus: Language server is not responding after ${consecutiveFailures} failures. ` +
          `The extension is temporarily disabled.`,
        "Restart Language Server",
        "Dismiss"
      );

      if (action === "Restart Language Server") {
        await restartLanguageServer();
      }
    } else {
      vscode.window.showErrorMessage(
        `SharpFocus: Analysis failed (${consecutiveFailures}/${MAX_FAILURES_BEFORE_CIRCUIT_BREAK}). ${error}`
      );
    }

    focusModeController.clear(editor);
  }
}

async function restartLanguageServer(): Promise<void> {
  try {
    statusBar.setLoading("Restarting language server...");
    await languageServerManager.stop();

    await new Promise((resolve) => setTimeout(resolve, 1000));

    const context = (global as any).extensionContext as vscode.ExtensionContext;
    const restartedClient = languageServerManager.start(context);
    client = restartedClient;

    consecutiveFailures = 0;
    statusBar.clearDegraded();
    statusBar.setIdle();

    vscode.window.showInformationMessage(
      "SharpFocus: Language server restarted successfully"
    );
  } catch (error) {
    console.error("SharpFocus: failed to restart language server", error);
    vscode.window.showErrorMessage(
      `SharpFocus: Failed to restart language server: ${error}. ` +
        `Please reload the window.`
    );
  }
}

function clearHighlights(): void {
  if (focusModeTimeout) {
    clearTimeout(focusModeTimeout);
    focusModeTimeout = undefined;
  }

  focusModeController.clearAll();
  navigationManager.clear();
  flowTreeViewProvider.clear();
}

function shouldAnalyze(
  editor: vscode.TextEditor | undefined
): editor is vscode.TextEditor {
  return (
    !!editor &&
    editor.document.languageId === "csharp" &&
    modeManager.getAnalysisMode() === "focus"
  );
}

function applyUiSettings(): void {
  if (!wordHighlightManager) {
    return;
  }

  const config = vscode.workspace.getConfiguration("sharpfocus");
  const disableWordHighlight = config.get<boolean>(
    "ui.disableWordHighlight",
    true
  );

  if (disableWordHighlight) {
    void wordHighlightManager.disable();
  } else {
    void wordHighlightManager.restore();
  }
}
