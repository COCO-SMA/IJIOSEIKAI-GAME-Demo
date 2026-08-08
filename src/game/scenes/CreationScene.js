const DISTRICTS = [
    { id: 'jinyong', name: 'Jinyong', desc: 'glass towers. something about the rules here feels enforceable.', available: true },
    { id: 'yundun', name: 'Yundun', desc: 'data flows with things that should not be in the data.', available: false },
    { id: 'jiuxu', name: 'Jiuxu', desc: 'old things remember things. sometimes they tell you.', available: true },
    { id: 'tiewei', name: 'Tiewei', desc: 'what gets made here is occasionally not what was designed.', available: false },
    { id: 'chagang', name: 'Chagang', desc: 'the roads change on their own. they always have a reason.', available: false },
    { id: 'yanao', name: 'Yanao', desc: 'containers arrive from places that are not places.', available: false },
    { id: 'zhongling', name: 'Zhongling', desc: 'commute time is occasionally not the right time.', available: false },
    { id: 'yeping', name: 'Yeping', desc: 'the mountains hold things older than the city.', available: false },
    { id: 'hetang', name: 'Hetang', desc: 'what grows here is sometimes not what was planted.', available: false },
    { id: 'chaowan', name: 'Chaowan', desc: 'the sea has moods. the third one has no name.', available: false },
    { id: 'bianpu', name: 'Bianpu', desc: 'where new meets old. anything can happen. usually does.', available: false }
];

const ORIGINS = {
    jinyong: [
        { id: 'jinyong_political', name: 'political family kid', desc: 'parents in the system. you cannot go to that district, your dad will know.' },
        { id: 'jinyong_finance', name: 'finance family kid', desc: 'parents in finance. spending is normal. so is sudden bankruptcy.' }
    ],
    yundun: [
        { id: 'yundun_programmer', name: "programmer's kid", desc: 'your dad taught you to see the world like debugging. some bugs are real bugs.' },
        { id: 'yundun_entrepreneur', name: "entrepreneur's kid", desc: 'dinner table talk was either dreams or bankruptcy. usually both.' }
    ],
    jiuxu: [
        { id: 'jiuxu_restaurant', name: "restaurant owner's kid", desc: 'you can read a customer mood from a bowl of noodles.' },
        { id: 'jiuxu_smuggler', name: 'smuggler descendant', desc: 'grandma cabinet always had things with no name.' }
    ],
    tiewei: [
        { id: 'tiewei_factory', name: "factory master's kid", desc: 'your dad hands were like sandpaper. nothing he touched broke.' },
        { id: 'tiewei_landlord', name: "village landlord's kid", desc: 'rent day is when you see the most kinds of people. including some not quite people.' }
    ],
    chagang: [
        { id: 'chagang_courier', name: 'courier family', desc: 'your dad says delivering is running the whole city. run enough, see everything.' },
        { id: 'chagang_village', name: 'walled village kid', desc: 'grandma said do not break this rule. you tried not knocking. that day was indeed not right.' }
    ],
    yanao: [
        { id: 'yanao_port', name: "port worker's kid", desc: 'you saw a container shake by itself at midnight. dad said do not ask.' },
        { id: 'yanao_seafood', name: 'seafood stall kid', desc: 'next stall octopus smelled wrong today. cannot explain. but you know.' }
    ],
    zhongling: [
        { id: 'zhongling_commuter', name: "commuter's kid", desc: 'you rode the line with eyes closed. sometimes arrived at a station you never heard of.' },
        { id: 'zhongling_nightmarket', name: 'night market family', desc: 'mom sold fried noodles for 20 years. you know what each stall owner does by day.' }
    ],
    yeping: [
        { id: 'yeping_research', name: 'research institute kid', desc: 'dad spent 30 years in the lab. says this world has more bugs than code.' },
        { id: 'yeping_village', name: 'old village teen', desc: 'you saw something in the mountains at 12. still cannot explain it.' }
    ],
    hetang: [
        { id: 'hetang_farm', name: "farm worker's kid", desc: 'you grew up milking cows and feeding pigeons. animals listen to you.' },
        { id: 'hetang_goose', name: 'goose roast family', desc: 'grandpa said the key is not the goose, it is the fire. he was right.' }
    ],
    chaowan: [
        { id: 'chaowan_guesthouse', name: "guesthouse owner's kid", desc: 'a guest checked out. you found the address they registered does not exist.' },
        { id: 'chaowan_fisher', name: 'fishing family descendant', desc: 'three weathers to not go out: typhoon, fog, and a third one dad could not name.' }
    ],
    bianpu: [
        { id: 'bianpu_pioneer', name: "pioneer's kid", desc: 'when your family moved here there was nothing. you learned to make everything.' },
        { id: 'bianpu_fishing', name: 'fishing village teen', desc: 'your village is very calm about weird things. oh, again. then carry on.' }
    ]
};

const COMMON_ORIGINS = [
    { id: 'common_normal', name: 'ordinary person', desc: 'nothing special. but weird things love finding ordinary people.' },
    { id: 'common_doctor', name: "doctor's family kid", desc: 'your family can tell what is wrong with someone by looking. sometimes what is wrong is not medical.' },
    { id: 'common_business', name: 'business family kid', desc: 'your family sold things. everything. the art is not the product, it is the timing.' }
];

export class CreationScene {
    constructor(game) {
        this.game = game;
        this.phase = 'district';
        this.districtIndex = 0;
        this.originIndex = 0;
        this.nameInput = '';
        this.inputtingName = false;
    }

    onEnter() {
        this.phase = 'district';
        this.districtIndex = 0;
        this.originIndex = 0;
        this.nameInput = '';
        this.inputtingName = false;
    }

    get availableDistricts() {
        return DISTRICTS.filter(d => d.available);
    }

    getAllOrigins(districtId) {
        const districtOrigins = ORIGINS[districtId] || [];
        return [...districtOrigins, ...COMMON_ORIGINS];
    }

    update(delta) {
        const input = this.game.input;

        if (this.inputtingName) {
            this._handleNameInput();
            return;
        }

        if (this.phase === 'district') {
            const districts = this.availableDistricts;
            if (input.isPressed('ArrowRight') || input.isPressed('KeyD')) {
                this.districtIndex = (this.districtIndex + 1) % districts.length;
            }
            if (input.isPressed('ArrowLeft') || input.isPressed('KeyA')) {
                this.districtIndex = (this.districtIndex - 1 + districts.length) % districts.length;
            }
            if (input.isActionPressed('confirm')) {
                this.phase = 'origin';
                this.originIndex = 0;
            }
        } else if (this.phase === 'origin') {
            const district = this.availableDistricts[this.districtIndex];
            const origins = this.getAllOrigins(district.id);
            if (input.isPressed('ArrowDown') || input.isPressed('KeyS')) {
                this.originIndex = (this.originIndex + 1) % origins.length;
            }
            if (input.isPressed('ArrowUp') || input.isPressed('KeyW')) {
                this.originIndex = (this.originIndex - 1 + origins.length) % origins.length;
            }
            if (input.isActionPressed('confirm')) {
                this.phase = 'name';
                this.inputtingName = true;
            }
            if (input.isActionPressed('cancel')) {
                this.phase = 'district';
            }
        } else if (this.phase === 'name') {
            if (input.isActionPressed('confirm')) {
                this._createCharacter();
            }
        } else if (this.phase === 'confirm') {
            if (input.isActionPressed('confirm')) {
                this._createCharacter();
            }
            if (input.isActionPressed('cancel')) {
                this.inputtingName = true;
                this.phase = 'name';
            }
        }
    }

    _handleNameInput() {
        const input = this.game.input;

        const chars = input.getTypedChars();
        for (const ch of chars) {
            if (ch === '\b') {
                this.nameInput = this.nameInput.slice(0, -1);
            } else if (this.nameInput.length < 12) {
                this.nameInput += ch;
            }
        }

        if (input.isActionPressed('confirm') && this.nameInput.length > 0) {
            this.inputtingName = false;
            this.phase = 'confirm';
        }
        if (input.isActionPressed('cancel')) {
            this.inputtingName = false;
            this.phase = 'origin';
        }
    }

    _createCharacter() {
        const district = this.availableDistricts[this.districtIndex];
        const origins = this.getAllOrigins(district.id);
        const origin = origins[this.originIndex];

        const save = this.game.save.createNewSave({
            district: district.id,
            origin: origin.id,
            name: this.nameInput || 'Kid'
        });

        this.game.setGameState(save);
        this.game.scenes.switchTo('explore');
    }

    render(ctx, camera) {
        const w = this.game.renderer.width;
        const h = this.game.renderer.height;

        ctx.fillStyle = '#0f0f1a';
        ctx.fillRect(0, 0, w, h);

        ctx.save();
        ctx.textAlign = 'center';

        if (this.phase === 'district') {
            this._renderDistrictSelect(ctx, w, h);
        } else if (this.phase === 'origin') {
            this._renderOriginSelect(ctx, w, h);
        } else if (this.phase === 'name' || this.inputtingName) {
            this._renderNameInput(ctx, w, h);
        } else if (this.phase === 'confirm') {
            this._renderConfirm(ctx, w, h);
        }

        ctx.restore();
    }

    _renderDistrictSelect(ctx, w, h) {
        const districts = this.availableDistricts;

        ctx.fillStyle = '#a0a0c0';
        ctx.font = 'bold 24px "Microsoft YaHei", sans-serif';
        ctx.fillText('Where were you born?', w / 2, 60);

        const d = districts[this.districtIndex];
        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 32px "Microsoft YaHei", sans-serif';
        ctx.fillText(d.name, w / 2, 130);

        ctx.fillStyle = '#8080a0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        this._wrapText(ctx, d.desc, w / 2, 170, w - 100);

        ctx.fillStyle = '#404060';
        ctx.font = '12px "Microsoft YaHei", sans-serif';
        ctx.fillText('< left / right > to choose, space to confirm', w / 2, h - 40);

        ctx.fillStyle = '#303040';
        ctx.font = '11px monospace';
        ctx.textAlign = 'right';
        ctx.fillText(`${this.districtIndex + 1} / ${districts.length}`, w - 20, h - 20);
    }

    _renderOriginSelect(ctx, w, h) {
        const district = this.availableDistricts[this.districtIndex];
        const origins = this.getAllOrigins(district.id);

        ctx.fillStyle = '#a0a0c0';
        ctx.font = 'bold 20px "Microsoft YaHei", sans-serif';
        ctx.fillText(`Born in ${district.name}`, w / 2, 50);
        ctx.fillStyle = '#606080';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        ctx.fillText('Who are you?', w / 2, 78);

        origins.forEach((origin, i) => {
            const y = 120 + i * 80;
            const selected = i === this.originIndex;
            ctx.fillStyle = selected ? '#5dcaa5' : '#505060';
            ctx.font = selected ? 'bold 16px "Microsoft YaHei", sans-serif' : '15px "Microsoft YaHei", sans-serif';
            ctx.fillText((selected ? '> ' : '  ') + origin.name, w / 2, y);
            ctx.fillStyle = selected ? '#6080a0' : '#404050';
            ctx.font = '12px "Microsoft YaHei", sans-serif';
            this._wrapText(ctx, origin.desc, w / 2, y + 22, w - 200);
        });

        ctx.fillStyle = '#404060';
        ctx.font = '12px "Microsoft YaHei", sans-serif';
        ctx.fillText('up/down to choose, space to confirm, esc to go back', w / 2, h - 40);
    }

    _renderNameInput(ctx, w, h) {
        ctx.fillStyle = '#a0a0c0';
        ctx.font = 'bold 20px "Microsoft YaHei", sans-serif';
        ctx.fillText('What is your name?', w / 2, h / 2 - 40);

        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 28px "Microsoft YaHei", sans-serif';
        const displayName = this.nameInput || '_';
        ctx.fillText(displayName, w / 2, h / 2 + 10);

        ctx.fillStyle = '#404060';
        ctx.font = '12px "Microsoft YaHei", sans-serif';
        ctx.fillText('type your name, space to confirm, esc to go back', w / 2, h - 40);
    }

    _renderConfirm(ctx, w, h) {
        const district = this.availableDistricts[this.districtIndex];
        const origins = this.getAllOrigins(district.id);
        const origin = origins[this.originIndex];

        ctx.fillStyle = '#a0a0c0';
        ctx.font = 'bold 20px "Microsoft YaHei", sans-serif';
        ctx.fillText('Ready?', w / 2, 80);

        ctx.fillStyle = '#8080a0';
        ctx.font = '15px "Microsoft YaHei", sans-serif';
        ctx.fillText(`Name: ${this.nameInput || 'Kid'}`, w / 2, 130);
        ctx.fillText(`Born: ${district.name}`, w / 2, 160);
        ctx.fillText(`Family: ${origin.name}`, w / 2, 190);

        ctx.fillStyle = '#505060';
        ctx.font = '13px "Microsoft YaHei", sans-serif';
        ctx.fillText('Generation 1. Kuncheng does not know you yet.', w / 2, 240);

        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 16px "Microsoft YaHei", sans-serif';
        ctx.fillText('> press space to begin <', w / 2, h - 80);

        ctx.fillStyle = '#404060';
        ctx.font = '12px "Microsoft YaHei", sans-serif';
        ctx.fillText('esc to change name', w / 2, h - 40);
    }

    _wrapText(ctx, text, centerX, y, maxWidth) {
        const words = text.split(' ');
        let line = '';
        let lineY = y;
        for (const word of words) {
            const testLine = line + (line ? ' ' : '') + word;
            const metrics = ctx.measureText(testLine);
            if (metrics.width > maxWidth && line) {
                ctx.fillText(line, centerX, lineY);
                line = word;
                lineY += 18;
            } else {
                line = testLine;
            }
        }
        ctx.fillText(line, centerX, lineY);
    }

    renderUI(ctx, w, h) {}
}
