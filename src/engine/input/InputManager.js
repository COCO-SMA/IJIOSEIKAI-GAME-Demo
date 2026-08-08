export class InputManager {
    constructor() {
        this.keys = {};
        this.keysPressed = {};
        this.keysReleased = {};
        this.mouse = { x: 0, y: 0, down: false, clicked: false };
        this.typedChars = [];  // printable characters typed this frame
    }

    init() {
        window.addEventListener('keydown', (e) => {
            if (!this.keys[e.code]) {
                this.keysPressed[e.code] = true;
            }
            this.keys[e.code] = true;

            // Capture printable characters for text input (name entry, etc.)
            if (e.key.length === 1 && e.key !== ' ' && !e.ctrlKey && !e.altKey && !e.metaKey) {
                this.typedChars.push(e.key);
            }
            // Backspace for text editing
            if (e.key === 'Backspace') {
                this.typedChars.push('\b');
            }

            if (['ArrowUp','ArrowDown','ArrowLeft','ArrowRight','Space'].includes(e.code)) {
                e.preventDefault();
            }
        });

        window.addEventListener('keyup', (e) => {
            this.keysReleased[e.code] = true;
            this.keys[e.code] = false;
        });

        this.canvas = document.getElementById('gameCanvas');
        if (this.canvas) {
            this.canvas.addEventListener('mousemove', (e) => {
                const rect = this.canvas.getBoundingClientRect();
                this.mouse.x = e.clientX - rect.left;
                this.mouse.y = e.clientY - rect.top;
            });
            this.canvas.addEventListener('mousedown', (e) => {
                this.mouse.down = true;
                this.mouse.clicked = true;
            });
            this.canvas.addEventListener('mouseup', () => {
                this.mouse.down = false;
            });
        }
    }

    update(delta) {
        this.keysPressed = {};
        this.keysReleased = {};
        this.mouse.clicked = false;
        this.typedChars = [];
    }

    getTypedChars() {
        return this.typedChars.slice();
    }

    isDown(code) {
        return !!this.keys[code];
    }

    isPressed(code) {
        return !!this.keysPressed[code];
    }

    isReleased(code) {
        return !!this.keysReleased[code];
    }

    anyPressed(codes) {
        return codes.some(c => this.isPressed(c));
    }

    getDirection() {
        let dx = 0, dy = 0;
        if (this.isDown('ArrowLeft') || this.isDown('KeyA')) dx = -1;
        if (this.isDown('ArrowRight') || this.isDown('KeyD')) dx = 1;
        if (this.isDown('ArrowUp') || this.isDown('KeyW')) dy = -1;
        if (this.isDown('ArrowDown') || this.isDown('KeyS')) dy = 1;
        return { x: dx, y: dy };
    }

    isActionPressed(action) {
        const mappings = {
            confirm: ['Space', 'Enter', 'KeyZ'],
            cancel: ['Escape', 'KeyX'],
            menu: ['Tab', 'KeyC'],
            interact: ['Space', 'Enter']
        };
        const codes = mappings[action] || [action];
        return this.anyPressed(codes);
    }
}
