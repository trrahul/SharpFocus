import * as fs from "fs";
import * as path from "path";
import * as vscode from "vscode";
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind,
} from "vscode-languageclient/node";

export class LanguageServerManager {
  private client: LanguageClient | undefined;

  start(context: vscode.ExtensionContext): LanguageClient {
    const serverPath = this.findServerPath();

    if (!serverPath) {
      const msg =
        "SharpFocus: Could not find language server. Please configure sharpfocus.serverPath in settings.";
      vscode.window.showErrorMessage(msg);
      console.error(msg);
      throw new Error("Language server path not configured");
    }

    console.log(`SharpFocus: Starting language server at ${serverPath}`);
    vscode.window.showInformationMessage(
      `SharpFocus: Starting language server...`
    );

    const serverOptions: ServerOptions = {
      run: {
        command: "dotnet",
        args: [serverPath],
        transport: TransportKind.stdio,
      },
      debug: {
        command: "dotnet",
        args: [serverPath],
        transport: TransportKind.stdio,
      },
    };

    const clientOptions: LanguageClientOptions = {
      documentSelector: [{ scheme: "file", language: "csharp" }],
      synchronize: {
        fileEvents: vscode.workspace.createFileSystemWatcher("**/*.cs"),
      },
    };

    this.client = new LanguageClient(
      "sharpfocus",
      "SharpFocus Language Server",
      serverOptions,
      clientOptions
    );

    this.client.start();

    return this.client;
  }

  getClient(): LanguageClient | undefined {
    return this.client;
  }

  async stop(): Promise<void> {
    if (this.client) {
      await this.client.stop();
      this.client = undefined;
    }
  }

  private findServerPath(): string | undefined {
    const config = vscode.workspace.getConfiguration("sharpfocus");
    let serverPath = config.get<string>("serverPath");

    if (serverPath) {
      console.log(
        `SharpFocus: Using configured serverPath setting: ${serverPath}`
      );
      return serverPath;
    }

    const possiblePaths = this.getPossibleServerPaths();

    console.log("SharpFocus: Searching for language server DLL...");
    for (const candidatePath of possiblePaths) {
      console.log(`  Checking: ${candidatePath}`);
      if (fs.existsSync(candidatePath)) {
        serverPath = candidatePath;
        console.log(`  ✓ Found server at: ${serverPath}`);
        return serverPath;
      }
    }

    console.log("SharpFocus: Language server not found in candidate paths");
    return undefined;
  }

  private getPossibleServerPaths(): string[] {
    const extensionPath = vscode.extensions.getExtension("RahulTR.sharpfocus")?.extensionPath;

    const platform = process.platform;
    const arch = process.arch;
    let runtimeFolder = "";

    if (platform === "win32") {
      runtimeFolder = arch === "arm64" ? "win-arm64" : "win-x64";
    } else if (platform === "darwin") {
      runtimeFolder = arch === "arm64" ? "osx-arm64" : "osx-x64";
    } else if (platform === "linux") {
      runtimeFolder = arch === "arm64" ? "linux-arm64" : "linux-x64";
    }

    const paths: string[] = [];

    if (extensionPath && runtimeFolder) {
      paths.push(
        path.join(extensionPath, "server", runtimeFolder, "SharpFocus.LanguageServer.dll")
      );
    }

    const serverDllPath = path.join(
      "src",
      "SharpFocus.LanguageServer",
      "bin",
      "Debug",
      "net10.0",
      "SharpFocus.LanguageServer.dll"
    );

    paths.push(path.join("d:", "workspace", "SharpFocus", serverDllPath));

    (vscode.workspace.workspaceFolders || []).forEach((folder) => {
      paths.push(path.join(folder.uri.fsPath, serverDllPath));

      paths.push(path.join(folder.uri.fsPath, "..", "..", serverDllPath));
    });

    return paths;
  }
}
