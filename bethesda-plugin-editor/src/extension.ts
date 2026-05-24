import * as vscode from "vscode";
import * as path from "path";

export function activate(context: vscode.ExtensionContext) {
  context.subscriptions.push(
    vscode.commands.registerCommand("mEdit.openEditor", () => {
      openWebviewPanel(context, "mEdit: Editor", "editor");
    }),
    vscode.commands.registerCommand("mEdit.openCompare", () => {
      openWebviewPanel(context, "mEdit: Compare", "compare");
    })
  );
}

function openWebviewPanel(
  context: vscode.ExtensionContext,
  title: string,
  _view: string
) {
  const panel = vscode.window.createWebviewPanel(
    "mEdit",
    title,
    vscode.ViewColumn.One,
    {
      enableScripts: true,
      localResourceRoots: [
        vscode.Uri.file(path.join(context.extensionPath, "out", "webview")),
      ],
    }
  );

  const scriptUri = panel.webview.asWebviewUri(
    vscode.Uri.file(
      path.join(context.extensionPath, "out", "webview", "assets", "main.js")
    )
  );

  panel.webview.html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy"
    content="default-src 'none'; script-src ${panel.webview.cspSource}; style-src ${panel.webview.cspSource} 'unsafe-inline';">
</head>
<body>
  <div id="root"></div>
  <script type="module" src="${scriptUri}"></script>
</body>
</html>`;
}

export function deactivate() {}
