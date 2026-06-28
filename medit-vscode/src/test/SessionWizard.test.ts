import { describe, it, expect, vi, beforeEach } from 'vitest';
import { SessionWizard, type WizardDeps } from '../SessionWizard';
import type { GamePaths } from '../GamePathDetector';

function makeClient(plugins: unknown[], loadData?: unknown) {
  return {
    GET: vi.fn().mockResolvedValue({ data: plugins, response: { ok: true } }),
    POST: vi.fn().mockResolvedValue({
      data: loadData ?? { status: 'loaded', failures: [] },
      response: { ok: true },
    }),
  } as any;
}

const detectedPaths: GamePaths = {
  dataFolder: '/game/Data',
  pluginsTxt: '/config/Plugins.txt',
};

function makeDeps(overrides: Partial<WizardDeps> = {}): WizardDeps {
  return {
    client: makeClient([]),
    detectPaths: vi.fn().mockResolvedValue(detectedPaths),
    showQuickPick: vi.fn(),
    showInputBox: vi.fn(),
    showErrorMessage: vi.fn(),
    showWarningMessage: vi.fn(),
    log: vi.fn(),
    ...overrides,
  };
}

describe('SessionWizard', () => {
  beforeEach(() => vi.resetAllMocks());

  it('skips wizard and returns true when plugins already loaded', async () => {
    const client = makeClient([{ name: 'Fallout4.esm' }]);
    const deps = makeDeps({ client });

    const wizard = new SessionWizard(deps);
    const result = await wizard.run();

    expect(result).toBe(true);
    expect(deps.showQuickPick).not.toHaveBeenCalled();
  });

  it('runs wizard and POSTs detected paths when user accepts', async () => {
    const deps = makeDeps({
      showQuickPick: vi.fn().mockResolvedValue({ label: 'Use detected paths' }),
    });

    const wizard = new SessionWizard(deps);
    const result = await wizard.run();

    expect(result).toBe(true);
    expect(deps.client.POST).toHaveBeenCalledWith(
      '/session/load',
      expect.objectContaining({
        body: { dataFolderPath: '/game/Data', pluginsTxtPath: '/config/Plugins.txt' },
      })
    );
  });

  it('returns false when user cancels Quick Pick', async () => {
    const deps = makeDeps({
      showQuickPick: vi.fn().mockResolvedValue(undefined),
    });

    const wizard = new SessionWizard(deps);
    const result = await wizard.run();

    expect(result).toBe(false);
    expect(deps.client.POST).not.toHaveBeenCalled();
  });

  it('prompts for manual paths when user chooses manually', async () => {
    const deps = makeDeps({
      showQuickPick: vi.fn().mockResolvedValue({ label: 'Choose manually…' }),
      showInputBox: vi.fn()
        .mockResolvedValueOnce('/custom/Data')
        .mockResolvedValueOnce('/custom/Plugins.txt'),
    });

    const wizard = new SessionWizard(deps);
    const result = await wizard.run();

    expect(result).toBe(true);
    expect(deps.client.POST).toHaveBeenCalledWith(
      '/session/load',
      expect.objectContaining({
        body: { dataFolderPath: '/custom/Data', pluginsTxtPath: '/custom/Plugins.txt' },
      })
    );
  });

  it('warns and logs when the load skipped plugins, but still returns true', async () => {
    const client = makeClient([], {
      status: 'loaded',
      failures: [
        { name: 'Lunar-UniqueCreatures.esp', reason: 'Unexpected record type: RBPC != NAME.' },
        { name: 'Bad.esp', reason: 'boom' },
      ],
    });
    const deps = makeDeps({
      client,
      showQuickPick: vi.fn().mockResolvedValue({ label: 'Use detected paths' }),
    });

    const wizard = new SessionWizard(deps);
    const result = await wizard.run();

    expect(result).toBe(true);
    expect(deps.showWarningMessage).toHaveBeenCalledTimes(1);
    const warning = (deps.showWarningMessage as any).mock.calls[0][0] as string;
    expect(warning).toContain('2');
    expect(warning).toContain('Lunar-UniqueCreatures.esp');
    // Full reasons go to the output log, one line per skipped plugin.
    expect(deps.log).toHaveBeenCalledWith(expect.stringContaining('Lunar-UniqueCreatures.esp'));
    expect(deps.log).toHaveBeenCalledWith(expect.stringContaining('RBPC != NAME'));
  });

  it('does not warn when there are no load failures', async () => {
    const deps = makeDeps({
      showQuickPick: vi.fn().mockResolvedValue({ label: 'Use detected paths' }),
    });

    await new SessionWizard(deps).run();

    expect(deps.showWarningMessage).not.toHaveBeenCalled();
  });

  it('returns false when manual path input is cancelled', async () => {
    const deps = makeDeps({
      showQuickPick: vi.fn().mockResolvedValue({ label: 'Choose manually…' }),
      showInputBox: vi.fn().mockResolvedValue(undefined),
    });

    const wizard = new SessionWizard(deps);
    const result = await wizard.run();

    expect(result).toBe(false);
  });
});
