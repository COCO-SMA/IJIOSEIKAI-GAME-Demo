import { TitleScene } from '../game/scenes/TitleScene.js';
import { CreationScene } from '../game/scenes/CreationScene.js';
import { ExploreScene } from '../game/scenes/ExploreScene.js';

export function registerScenes(sceneManager) {
    sceneManager.register('title', (game, params) => new TitleScene(game, params));
    sceneManager.register('creation', (game, params) => new CreationScene(game, params));
    sceneManager.register('explore', (game, params) => new ExploreScene(game, params));
}
