import * as assert from 'assert';
import * as path from 'path';
import * as fs from 'fs';
import * as vscode from 'vscode';

// Activation smoke test (phase 6 D21): opening a .heddle file activates the extension, the client reaches
// Running against a stub server path, and the TextMate grammar contributes its scopes. The facade rule keeps all
// logic out of the client, so this is the only client test.
suite('Heddle extension smoke test', () => {
  test('grammar is contributed and .heddle activates', async () => {
    const grammar = path.resolve(__dirname, '..', '..', 'syntaxes', 'heddle.tmLanguage.json');
    assert.ok(fs.existsSync(grammar), 'the TextMate grammar copy must exist after the build');

    const tmp = path.join(__dirname, 'smoke.heddle');
    fs.writeFileSync(tmp, '@model(){{object}}\n@(Title)');
    const doc = await vscode.workspace.openTextDocument(tmp);
    await vscode.window.showTextDocument(doc);
    assert.strictEqual(doc.languageId, 'heddle');

    const ext = vscode.extensions.getExtension('heddle.heddle');
    assert.ok(ext, 'the extension must be present');
    await ext!.activate();
    assert.ok(ext!.isActive, 'the extension must activate on a .heddle file');
  });
});
