export class SceneManager {
    constructor(game) {
        this.game = game;
        this.currentScene = null;
        this.nextScene = null;
        this.transitioning = false;
        this.transitionAlpha = 0;
        this.transitionType = 'fade';

        this.sceneFactories = {};
    }

    register(name, factory) {
        this.sceneFactories[name] = factory;
    }

    switchTo(name, params = {}) {
        if (this.transitioning) return;

        const factory = this.sceneFactories[name];
        if (!factory) {
            console.warn(`Scene "${name}" not registered`);
            return;
        }

        if (this.currentScene && this.currentScene.onExit) {
            this.currentScene.onExit();
        }

        this.currentScene = factory(this.game, params);
        if (this.currentScene.onEnter) {
            this.currentScene.onEnter(params);
        }
    }

    update(delta) {
        if (this.currentScene && this.currentScene.update) {
            this.currentScene.update(delta);
        }
    }
}
