export class TitleScene {
    constructor(game) {
        this.game = game;
        this.titleY = -50;
        this.titleTargetY = 120;
        this.menuIndex = 0;
        this.menuItems = [
            { label: 'New Game', action: () => this.game.scenes.switchTo('creation') },
            { label: 'Continue', action: () => this._continue() },
            { label: 'Settings', action: () => {} }
        ];
        this.blinkTimer = 0;
        this.showPressKey = false;
    }

    onEnter() {
        this.titleY = -50;
    }

    update(delta) {
        this.titleY += (this.titleTargetY - this.titleY) * 0.05;
        this.blinkTimer += delta;
        this.showPressKey = Math.floor(this.blinkTimer * 2) % 2 === 0;

        const input = this.game.input;

        if (input.isPressed('ArrowUp') || input.isPressed('KeyW')) {
            this.menuIndex = (this.menuIndex - 1 + this.menuItems.length) % this.menuItems.length;
        }
        if (input.isPressed('ArrowDown') || input.isPressed('KeyS')) {
            this.menuIndex = (this.menuIndex + 1) % this.menuItems.length;
        }
        if (input.isActionPressed('confirm')) {
            this.menuItems[this.menuIndex].action();
        }
    }

    _continue() {
        const save = this.game.save.load();
        if (save) {
            this.game.setGameState(save);
            this.game.scenes.switchTo('explore');
        }
    }

    render(ctx, camera) {
        const w = this.game.renderer.width;
        const h = this.game.renderer.height;

        ctx.fillStyle = '#0f0f1a';
        ctx.fillRect(0, 0, w, h);

        ctx.save();
        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 48px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('KUNCHENG', w / 2, this.titleY);
        ctx.fillStyle = '#a0a0c0';
        ctx.font = '20px "Microsoft YaHei", sans-serif';
        ctx.fillText('a pixel RPG about becoming a local', w / 2, this.titleY + 40);
        ctx.restore();

        const menuY = h / 2 + 40;
        ctx.save();
        ctx.textAlign = 'center';
        ctx.font = '18px "Microsoft YaHei", sans-serif';
        this.menuItems.forEach((item, i) => {
            const y = menuY + i * 36;
            if (i === this.menuIndex) {
                ctx.fillStyle = '#5dcaa5';
                ctx.fillText('> ' + item.label + ' <', w / 2, y);
            } else {
                ctx.fillStyle = '#606080';
                ctx.fillText(item.label, w / 2, y);
            }
        });
        ctx.restore();

        if (this.showPressKey) {
            ctx.save();
            ctx.fillStyle = '#404060';
            ctx.font = '12px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText('arrow keys to navigate, space to confirm', w / 2, h - 30);
            ctx.restore();
        }

        ctx.save();
        ctx.fillStyle = '#303040';
        ctx.font = '11px monospace';
        ctx.textAlign = 'right';
        ctx.fillText(`FPS: ${this.game.fps}`, w - 10, 20);
        ctx.restore();
    }

    renderUI(ctx, w, h) {}
}
