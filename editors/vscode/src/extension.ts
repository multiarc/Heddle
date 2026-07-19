import * as path from 'path';
import * as fs from 'fs';
import { execFileSync } from 'child_process';
import * as vscode from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions,
  TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;
const PINNED_VERSION = '2.0.0';
const INSTALL_HINT =
  `Heddle language server not found. Install it with: dotnet tool install --global Heddle.LanguageServer --version ${PINNED_VERSION} — or install the .NET 10 runtime.`;

export function activate(context: vscode.ExtensionContext): void {
  context.subscriptions.push(
    vscode.commands.registerCommand('heddle.restartServer', () => restart(context))
  );
  start(context);
}

export async function deactivate(): Promise<void> {
  if (client) {
    await client.stop();
    client = undefined;
  }
}

function restart(context: vscode.ExtensionContext): void {
  deactivate().then(() => start(context));
}

function start(context: vscode.ExtensionContext): void {
  // A LogOutputChannel (createOutputChannel with { log: true }) — vscode-languageclient's
  // LanguageClientOptions.outputChannel requires LogOutputChannel; it still satisfies the plain
  // OutputChannel parameters (discoverServer / hasDotnet10) since LogOutputChannel extends it.
  const output = vscode.window.createOutputChannel('Heddle Language Server', { log: true });
  const server = discoverServer(context, output);
  if (!server) {
    // No server: the extension stays active so the TextMate grammar keeps coloring (success criterion 5).
    vscode.window.showErrorMessage(INSTALL_HINT);
    output.appendLine(INSTALL_HINT);
    return;
  }

  const serverOptions: ServerOptions = server;
  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'heddle' }],
    initializationOptions: readSettings(),
    outputChannel: output
  };

  client = new LanguageClient('heddle', 'Heddle Language Server', serverOptions, clientOptions);
  client.start();
}

function readSettings(): Record<string, unknown> {
  const config = vscode.workspace.getConfiguration('heddle');
  const mode = config.get<string>('compile.expressionMode', 'native');
  return {
    assemblies: config.get<string[]>('model.assemblies', []),
    rootPath: config.get<string>('workspace.rootPath', ''),
    outputProfile: config.get<string>('compile.outputProfile', 'text'),
    expressionMode: mode,
    fileNamePostfix: config.get<string>('compile.fileNamePostfix', '')
  };
}

function discoverServer(
  context: vscode.ExtensionContext,
  output: vscode.OutputChannel
): ServerOptions | undefined {
  const config = vscode.workspace.getConfiguration('heddle');

  // 1. Explicit path setting — an explicit setting must not be silently overridden.
  const explicit = config.get<string>('server.path', '').trim();
  if (explicit) {
    return launch(explicit);
  }

  if (!hasDotnet10(output)) {
    return undefined;
  }

  // 2. Bundled server (per-RID publish output under server/).
  const bundled = context.asAbsolutePath(path.join('server', 'Heddle.LanguageServer.dll'));
  if (fs.existsSync(bundled)) {
    // `dotnet exec` runs a local file — unrelated to the rejected feed-restoring `dotnet tool exec`.
    return { command: 'dotnet', args: ['exec', bundled], transport: TransportKind.stdio };
  }

  // 3. heddle-lsp on PATH.
  return { command: 'heddle-lsp', args: [], transport: TransportKind.stdio };
}

function launch(serverPath: string): ServerOptions {
  if (serverPath.toLowerCase().endsWith('.dll')) {
    return { command: 'dotnet', args: ['exec', serverPath], transport: TransportKind.stdio };
  }
  return { command: serverPath, args: [], transport: TransportKind.stdio };
}

function hasDotnet10(output: vscode.OutputChannel): boolean {
  try {
    const runtimes = execFileSync('dotnet', ['--list-runtimes'], { encoding: 'utf8' });
    if (runtimes.includes('Microsoft.NETCore.App 10.')) {
      return true;
    }
    output.appendLine('The .NET 10 runtime was not found (dotnet --list-runtimes).');
    vscode.window.showErrorMessage(INSTALL_HINT);
    return false;
  } catch {
    output.appendLine('Could not run `dotnet --list-runtimes`. Is the .NET SDK/runtime installed?');
    vscode.window.showErrorMessage(INSTALL_HINT);
    return false;
  }
}
