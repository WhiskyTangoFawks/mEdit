import * as assert from 'assert';
import * as http from 'http';
import * as vscode from 'vscode';

const TEST_PORT = 15172;
let mockBackend: http.Server;

// Start a mock backend that answers GET /health → 200 so the extension
// activates in 'attached' state. Uses port 15172 (set via workspace settings).
before(async function () {
  this.timeout(15000);

  // The mock backend must be up before the extension activates so
  // BackendManager's first poll succeeds.
  mockBackend = http.createServer((_req, res) => {
    res.writeHead(200);
    res.end();
  });
  await new Promise<void>(r => mockBackend.listen(TEST_PORT, '127.0.0.1', () => r()));

  // activationEvents: [] means the extension is NOT auto-activated on startup.
  // Force activation so commands are registered before any test runs.
  const ext = vscode.extensions.all.find(e => e.packageJSON?.name === 'medit-vscode');
  if (ext && !ext.isActive) {
    await ext.activate();
  }

  // Give BackendManager time to poll and reach 'attached' (polls every 500 ms).
  await new Promise(r => setTimeout(r, 2000));
});

after(async () => {
  await new Promise<void>((resolve, reject) =>
    mockBackend.close(err => (err ? reject(err) : resolve()))
  );
});

// ── Command registration ───────────────────────────────────────────────────────

describe('mEdit command registration', () => {
  const EXPECTED_COMMANDS = [
    'mEdit.openEditor',
    'mEdit.openCompare',
    'mEdit.loadSession',
    'mEdit.reloadSession',
    'mEdit.refreshTree',
    'mEdit.newPlugin',
    'mEdit.copyAsOverrideInto',
  ];

  it('registers all expected commands on activation', async () => {
    const all = await vscode.commands.getCommands(/* filterInternal */ true);
    for (const cmd of EXPECTED_COMMANDS) {
      assert.ok(all.includes(cmd), `Command not registered: ${cmd}`);
    }
  });
});

// ── openEditor ────────────────────────────────────────────────────────────────

describe('mEdit.openEditor', () => {
  it('opens a new webview tab when no panel exists', async () => {
    const tabsBefore = vscode.window.tabGroups.all.flatMap(g => g.tabs).length;

    await vscode.commands.executeCommand('mEdit.openEditor', {
      formKey: 'Fallout4.esm:000001',
      label: 'Test Record',
    });

    await new Promise(r => setTimeout(r, 500));

    const tabsAfter = vscode.window.tabGroups.all.flatMap(g => g.tabs).length;
    assert.ok(tabsAfter > tabsBefore, 'Expected a new tab to be opened by mEdit.openEditor');
  });

  it('reuses the existing panel on a second call', async () => {
    const tabsAfterFirst = vscode.window.tabGroups.all.flatMap(g => g.tabs).length;

    await vscode.commands.executeCommand('mEdit.openEditor', {
      formKey: 'Fallout4.esm:000002',
      label: 'Another Record',
    });

    await new Promise(r => setTimeout(r, 500));

    const tabsAfterSecond = vscode.window.tabGroups.all.flatMap(g => g.tabs).length;
    assert.strictEqual(
      tabsAfterSecond,
      tabsAfterFirst,
      'Second mEdit.openEditor call should reuse the existing panel, not open a new tab'
    );
  });
});
