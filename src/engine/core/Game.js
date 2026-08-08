import { Renderer } from '../renderer/Renderer.js';
import { InputManager } from '../input/InputManager.js';
import { SceneManager } from '../scene/SceneManager.js';
import { AssetManager } from './AssetManager.js';
import { SaveManager } from './SaveManager.js';

export class Game {
    constructor(config) {
        this.canvas = config.canvas;
        this.loadingScreen = config.loadingScreen;
        this.loadingText = config.loadingText;
        this.loadingBar = config.loadingBar;

        this.running = false;
        this.lastTime = 0;

        this.renderer = new Renderer(this.canvas);
        this.input = new InputManager();
        this.assets = new AssetManager();
        this.save = new SaveManager();
        this.scenes = new SceneManager(this);

        this.gameState = null;
        this.fps = 0;
        this.frameCount = 0;
        this.fpsTimer = 0;
    }

    async start() {
        this._updateLoading('Initializing engine...', 0.1);

        this.renderer.init();
        this.input.init();

        this._updateLoading('Loading game data...', 0.3);

        // Data is lazy-loaded by MapManager/EventSystem on demand.
        // No directory fetch needed (file:// protocol can't list dirs).
        await new Promise(r => setTimeout(r, 100));

        this._updateLoading('Preparing Kuncheng...', 0.6);

        this.scenes.switchTo('title');

        this._updateLoading('Ready.', 1.0);

        setTimeout(() => {
            this.loadingScreen.classList.add('hidden');
            this.running = true;
            this.lastTime = performance.now();
            requestAnimationFrame((t) => this._loop(t));
        }, 300);
    }

    _loop(timestamp) {
        if (!this.running) return;

        const delta = Math.min((timestamp - this.lastTime) / 1000, 0.05);
        this.lastTime = timestamp;

        this.frameCount++;
        this.fpsTimer += delta;
        if (this.fpsTimer >= 1) {
            this.fps = this.frameCount;
            this.frameCount = 0;
            this.fpsTimer = 0;
        }

        // Scene reads input BEFORE input.update() clears per-frame state.
        // This ensures key presses accumulated between frames are not lost.
        this.scenes.update(delta);
        this.input.update(delta);
        this.renderer.render(this.scenes.currentScene);

        requestAnimationFrame((t) => this._loop(t));
    }

    _updateLoading(text, progress) {
        if (this.loadingText) this.loadingText.textContent = text;
        if (this.loadingBar) this.loadingBar.style.width = `${progress * 100}%`;
    }

    stop() {
        this.running = false;
    }

    getGameState() {
        return this.gameState;
    }

    setGameState(state) {
        this.gameState = state;
    }
}
