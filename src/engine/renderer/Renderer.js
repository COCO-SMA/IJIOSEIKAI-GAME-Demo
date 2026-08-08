export class Renderer {
    constructor(canvas) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.width = 0;
        this.height = 0;
        this.scale = 1;

        this.camera = {
            x: 0,
            y: 0,
            targetX: 0,
            targetY: 0,
            zoom: 1  // tech demo: no pixel-art assets yet, use 1:1
        };

        this.layers = {
            tilemap: [],
            entities: [],
            effects: [],
            ui: []
        };
    }

    init() {
        this._resize();
        window.addEventListener('resize', () => this._resize());
        this.ctx.imageSmoothingEnabled = false;
    }

    _resize() {
        const dpr = window.devicePixelRatio || 1;
        const w = window.innerWidth;
        const h = window.innerHeight;

        this.canvas.width = w * dpr;
        this.canvas.height = h * dpr;
        this.canvas.style.width = w + 'px';
        this.canvas.style.height = h + 'px';

        // Use setTransform to avoid compounding scale on resize
        this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        this.ctx.imageSmoothingEnabled = false;

        this.width = w;
        this.height = h;
    }

    render(scene) {
        this.ctx.clearRect(0, 0, this.width, this.height);

        if (!scene) return;

        this._updateCamera();

        this.ctx.save();
        this.ctx.scale(this.camera.zoom, this.camera.zoom);
        this.ctx.translate(-this.camera.x, -this.camera.y);

        if (scene.render) {
            scene.render(this.ctx, this.camera);
        }

        this.ctx.restore();

        if (scene.renderUI) {
            scene.renderUI(this.ctx, this.width, this.height);
        }
    }

    _updateCamera() {
        const lerp = 0.1;
        this.camera.x += (this.camera.targetX - this.camera.x) * lerp;
        this.camera.y += (this.camera.targetY - this.camera.y) * lerp;
    }

    setCameraTarget(x, y) {
        this.camera.targetX = x - (this.width / this.camera.zoom) / 2;
        this.camera.targetY = y - (this.height / this.camera.zoom) / 2;
    }

    drawSprite(ctx, sprite, x, y, frameX = 0, frameY = 0) {
        if (!sprite || !sprite.image) return;
        const sw = sprite.frameWidth || sprite.image.width;
        const sh = sprite.frameHeight || sprite.image.height;
        ctx.drawImage(
            sprite.image,
            frameX * sw, frameY * sh, sw, sh,
            Math.floor(x), Math.floor(y), sw, sh
        );
    }

    drawText(ctx, text, x, y, options = {}) {
        ctx.font = options.font || '14px "Microsoft YaHei", sans-serif';
        ctx.fillStyle = options.color || '#ffffff';
        ctx.textAlign = options.align || 'left';
        ctx.textBaseline = options.baseline || 'top';
        if (options.shadow) {
            ctx.fillStyle = options.shadow;
            ctx.fillText(text, x + 1, y + 1);
            ctx.fillStyle = options.color || '#ffffff';
        }
        ctx.fillText(text, x, y);
    }

    drawRect(ctx, x, y, w, h, fill, stroke) {
        if (fill) {
            ctx.fillStyle = fill;
            ctx.fillRect(x, y, w, h);
        }
        if (stroke) {
            ctx.strokeStyle = stroke;
            ctx.lineWidth = 0.5;
            ctx.strokeRect(x, y, w, h);
        }
    }
}
