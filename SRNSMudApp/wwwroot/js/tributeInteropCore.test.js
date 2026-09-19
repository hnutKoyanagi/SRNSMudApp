const assert = require('node:assert/strict');
const test = require('node:test');

const { createCompositionSyncController, extractText } = require('./tributeInteropCore.js');

function text(value) {
    return { nodeType: 3, textContent: value };
}

function element(tagName, childNodes = [], attributes = {}) {
    return {
        nodeType: 1,
        tagName,
        childNodes,
        textContent: childNodes.map(child => child.textContent ?? '').join(''),
        hasAttribute: name => Object.hasOwn(attributes, name),
        getAttribute: name => attributes[name],
        querySelectorAll: selector => selector === '[data-url]' && attributes['data-url'] ? [{}] : []
    };
}

test('extractText replaces internal-link pills with their URLs', () => {
    const editor = element('DIV', [
        text('前 '),
        element('SPAN', [text('Rust')], { 'data-url': '/TagDetail/12' }),
        text(' 後')
    ]);

    assert.equal(extractText(editor), '前 /TagDetail/12 後');
});

test('extractText preserves line breaks and normalizes non-breaking spaces', () => {
    const editor = element('DIV', [
        text('一\u00A0二'),
        element('BR'),
        element('P', [text('三')])
    ]);

    assert.equal(extractText(editor), '一 二\n三\n');
});

test('composition input is ignored until compositionend', () => {
    const syncCalls = [];
    const controller = createCompositionSyncController(() => syncCalls.push('sync'));

    controller.start();
    controller.input();
    controller.input();
    assert.deepEqual(syncCalls, []);

    controller.end();
    assert.deepEqual(syncCalls, ['sync']);

    controller.input();
    assert.deepEqual(syncCalls, ['sync', 'sync']);
});
