import { Game } from './engine/core/Game.js';
import { TitleScene } from './game/scenes/TitleScene.js';
import { CreationScene } from './game/scenes/CreationScene.js';
import { ExploreScene } from './game/scenes/ExploreScene.js';

const loadingText = document.getElementById('loadingText');

console.log('[BOOT] Starting Kuncheng RPG...');

try {
    console.log('[BOOT] Creating Game instance...');
    const game = new Game({
        canvas: document.getElementById('gameCanvas'),
        loadingScreen: document.getElementById('loadingScreen'),
        loadingText: loadingText,
        loadingBar: document.getElementById('loadingBarFill')
    });
    console.log('[BOOT] Game created, registering scenes...');

    game.scenes.register('title', (g, params) => new TitleScene(g, params));
    game.scenes.register('creation', (g, params) => new CreationScene(g, params));
    game.scenes.register('explore', (g, params) => new ExploreScene(g, params));
    console.log('[BOOT] Scenes registered, starting game...');

    game.start().then(() => {
        console.log('[BOOT] Game started successfully!');
    }).catch(e => {
        console.error('[BOOT] Game start failed:', e);
        loadingText.textContent = 'ERROR: ' + e.message;
    });
} catch (e) {
    console.error('[BOOT] Init error:', e);
    loadingText.textContent = 'INIT ERROR: ' + e.message;
}
