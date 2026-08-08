export class AssetManager {
    constructor() {
        this.images = new Map();
        this.jsonData = new Map();
        this.audio = new Map();
        this.atlases = new Map();
        this.loaded = 0;
        this.total = 0;
    }

    async preload(manifest) {
        for (const item of manifest) {
            this.total++;
            try {
                if (item.type === 'image') {
                    await this.loadImage(item.key, item.path);
                } else if (item.type === 'json') {
                    await this.loadJSON(item.key, item.path);
                } else if (item.type === 'audio') {
                    await this.loadAudio(item.key, item.path);
                } else if (item.type === 'atlas') {
                    await this.loadAtlas(item.key, item.imagePath, item.dataPath);
                }
            } catch (e) {
                console.warn(`Failed to load asset: ${item.key}`, e);
            }
            this.loaded++;
        }
    }

    loadImage(key, path) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => {
                this.images.set(key, img);
                resolve(img);
            };
            img.onerror = reject;
            img.src = path;
        });
    }

    loadJSON(key, path) {
        return fetch(path)
            .then(r => r.json())
            .then(data => {
                this.jsonData.set(key, data);
                return data;
            });
    }

    loadAudio(key, path) {
        return new Promise((resolve, reject) => {
            const audio = new Audio(path);
            audio.addEventListener('canplaythrough', () => {
                this.audio.set(key, audio);
                resolve(audio);
            });
            audio.onerror = reject;
            audio.src = path;
            audio.load();
        });
    }

    async loadAtlas(key, imagePath, dataPath) {
        const [image, data] = await Promise.all([
            this.loadImage(key + '_img', imagePath),
            this.loadJSON(key + '_data', dataPath)
        ]);
        this.atlases.set(key, { image, data });
    }

    getImage(key) {
        return this.images.get(key);
    }

    getJSON(key) {
        return this.jsonData.get(key);
    }

    getAudio(key) {
        return this.audio.get(key);
    }

    getAtlasFrame(atlasKey, frameName) {
        const atlas = this.atlases.get(atlasKey);
        if (!atlas) return null;
        const frame = atlas.data.frames[frameName];
        if (!frame) return null;
        return {
            image: atlas.image,
            sx: frame.x,
            sy: frame.y,
            sw: frame.w,
            sh: frame.h
        };
    }

    getProgress() {
        return this.total === 0 ? 1 : this.loaded / this.total;
    }
}
