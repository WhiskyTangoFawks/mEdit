import type { ApiClient } from './ApiClient';
import type { GamePaths } from './GamePathDetector';

export interface WizardDeps {
  client: ApiClient;
  detectPaths: () => Promise<GamePaths | null>;
  showQuickPick: (items: Array<{ label: string; detail?: string }>) => Promise<{ label: string } | undefined>;
  showInputBox: (opts: { prompt: string; value?: string }) => Thenable<string | undefined>;
  showErrorMessage: (msg: string) => void;
  showWarningMessage: (msg: string) => void;
  log: (msg: string) => void;
}

export class SessionWizard {
  constructor(private readonly deps: WizardDeps) {}

  async run(): Promise<boolean> {
    const { data } = await this.deps.client.GET('/plugins', {});
    const plugins = data as unknown as unknown[];
    if (Array.isArray(plugins) && plugins.length > 0) {
      return true;
    }

    const detected = await this.deps.detectPaths();
    const items = [
      ...(detected ? [{
        label: 'Use detected paths',
        detail: `${detected.dataFolder}  •  ${detected.pluginsTxt}`,
      }] : []),
      { label: 'Choose manually…' },
    ];

    const choice = await this.deps.showQuickPick(items);
    if (!choice) return false;

    let paths: GamePaths;

    if (choice.label === 'Use detected paths' && detected) {
      paths = detected;
    } else {
      const dataFolder = await this.deps.showInputBox({ prompt: 'Data folder path', value: detected?.dataFolder });
      if (!dataFolder) return false;
      const pluginsTxt = await this.deps.showInputBox({ prompt: 'Plugins.txt path', value: detected?.pluginsTxt });
      if (!pluginsTxt) return false;
      paths = { dataFolder, pluginsTxt };
    }

    const { data: loadData, response } = await this.deps.client.POST('/session/load', {
      body: { dataFolderPath: paths.dataFolder, pluginsTxtPath: paths.pluginsTxt },
    });

    if (!response.ok) {
      this.deps.showErrorMessage(`Failed to load session: ${response.status}`);
      return false;
    }

    this.reportFailures(loadData?.failures);
    return true;
  }

  // A plugin skipped during load (e.g. records Mutagen can't parse) means its records are
  // missing from the session. This must never be silent — warn the user and log every reason.
  private reportFailures(
    failures: ReadonlyArray<{ name?: string | null; reason?: string | null }> | null | undefined,
  ): void {
    if (!failures || failures.length === 0) return;

    for (const f of failures) {
      this.deps.log(`[SessionWizard] skipped plugin '${f.name ?? '?'}': ${f.reason ?? 'unknown error'}`);
    }

    const names = failures.map((f) => f.name ?? '?').join(', ');
    this.deps.showWarningMessage(
      `mEdit: ${failures.length} plugin(s) were skipped — their records are NOT loaded: ${names}. ` +
        `See the 'mEdit' output for details.`,
    );
  }
}
