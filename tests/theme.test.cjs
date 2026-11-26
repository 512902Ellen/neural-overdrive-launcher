const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { createHash } = require('node:crypto');
const vm = require('node:vm');

const theme = '../themes/neural-overdrive/';
const reference = readFileSync(require.resolve(theme + 'reference/codex-cyber-boot-v03.html'));
const runtime = readFileSync(require.resolve(theme + 'runtime/boot.html'), 'utf8');
const script = runtime.match(/<script>([\s\S]*?)<\/script>/)[1];
const id = '0123456789abcdef0123456789abcdef';

function simulate({ systemReduced = false } = {}) {
    const noop = () => {};
    const nodes = new Map();
    const handlers = new Map();
    const messages = [];
    const queued = new Map();
    const timers = [];
    let sender;
    let frameId = 0;
    const context = new Proxy({}, { get: (_, key) =>
        key === 'createRadialGradient' ? () => ({ addColorStop: noop }) : noop });
    const node = () => ({
        getContext: () => context,
        clientWidth: 1280,
        clientHeight: 720,
        style: { setProperty: noop },
        dataset: {},
        classList: { add: noop },
        children: [],
        appendChild: noop,
        setAttribute: noop,
        querySelector: node
    });
    const get = key => {
        if (!nodes.has(key)) nodes.set(key, node());
        return nodes.get(key);
    };
    vm.runInNewContext(script, {
        document: {
            querySelector: get,
            querySelectorAll: () => Array.from({ length: 5 }, node),
            createElement: node,
            body: node()
        },
        window: { chrome: { webview: {
            postMessage: m => messages.push({ ...m }),
            addEventListener: (_, cb) => { sender = cb; }
        } } },
        addEventListener: (event, cb) => handlers.set(event, cb),
        devicePixelRatio: 3,
        matchMedia: () => ({ matches: systemReduced }),
        requestAnimationFrame: cb => { queued.set(++frameId, cb); return frameId; },
        cancelAnimationFrame: i => queued.delete(i),
        setTimeout: (cb, delay) => timers.push({ cb, delay })
    });
    return {
        get, messages, queued, timers,
        send: data => sender({ data }),
        start: (mode, reducedMotion = false) => sender({ data: { v: 1, type: 'start', id, mode, reducedMotion } }),
        tick: time => {
            const frames = [...queued.values()];
            queued.clear();
            for (const frame of frames) frame(time);
        },
        escape: () => handlers.get('keydown')({ key: 'Escape', repeat: false, preventDefault: noop })
    };
}

test('preserved reference and documented simulated telemetry', () => {
    assert.equal(createHash('sha256').update(reference).digest('hex'),
        '98d0a00d6f3f197863e362db05bfc0c551b3690b09b841fd44ce28b1c613ad23');
    assert.doesNotMatch(runtime, /<button\b/);
    assert.match(runtime, /ANIMATION DEMO \/ SIMULATED TELEMETRY/);
    const original = reference.toString('utf8').replaceAll('\r\n', '\n');
    const drawing = html => html.slice(html.indexOf('function draw(T)'), html.indexOf('function render()'));
    assert.equal(drawing(runtime), drawing(original));
});

for (const [mode, duration] of [['quick', 5000], ['full', 9000]]) {
    test(mode + ' reaches the matching timeline boundary and stops', () => {
        const demo = simulate();
        assert.deepEqual(demo.messages, [{ v: 1, type: 'ready' }]);
        assert.equal(demo.queued.size, 0);
        demo.start(mode);
        demo.tick(1000);
        assert.equal(demo.get('#field').width, 2560);
        assert.equal(demo.get('#stage').dataset.finished, 'false');
        assert.deepEqual(demo.messages.at(-1), { v: 1, type: 'started', id });
        demo.tick(1000 + duration - 1);
        assert.equal(demo.get('#stage').dataset.finished, 'false');
        demo.tick(1000 + duration);
        assert.equal(demo.get('#stage').dataset.finished, 'true');
        assert.deepEqual(demo.messages.at(-1), { v: 1, type: 'completed', id });
        assert.equal(demo.queued.size, 0);
    });
}

test('rejects invalid start commands and skips via Escape once', () => {
    const demo = simulate();
    demo.send({ v: 2, type: 'start', id, mode: 'full', reducedMotion: false });
    demo.send({ v: 1, type: 'activate', id });
    assert.equal(demo.queued.size, 0);
    demo.start('quick');
    demo.tick(1000);
    demo.escape();
    demo.escape();
    assert.equal(demo.messages.filter(message => message.type === 'skipRequested').length, 1);
    assert.equal(demo.queued.size, 0);
});

test('native or browser reduced motion draws a static final frame', () => {
    for (const systemReduced of [false, true]) {
        const demo = simulate({ systemReduced });
        demo.start('full', !systemReduced);
        demo.tick(1000);
        assert.equal(demo.get('#stage').dataset.finished, 'true');
        assert.equal(demo.get('#flash').style.opacity, 0);
        assert.equal(demo.queued.size, 0);
        assert.equal(demo.timers[0].delay, 350);
        demo.timers[0].cb();
        assert.deepEqual(demo.messages.at(-1), { v: 1, type: 'completed', id });
    }
});

test('explicit reduced-motion opt-out overrides the browser setting', () => {
    const demo = simulate({ systemReduced: true });
    const start = { v: 1, type: 'start', id, mode: 'quick', reducedMotion: false };
    demo.send({ ...start, respectSystemReducedMotion: 'false' });
    assert.equal(demo.queued.size, 0);
    demo.send({ ...start, respectSystemReducedMotion: false });
    demo.tick(1000);
    assert.equal(demo.get('#stage').dataset.finished, 'false');
    demo.tick(6000);
    assert.equal(demo.get('#stage').dataset.finished, 'true');
});
