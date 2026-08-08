import { MapManager } from '../map/MapManager.js';
import { Player } from '../player/Player.js';
import { DialogueSystem } from '../dialogue/DialogueSystem.js';
import { CombatSystem, COMBAT_ACTIONS, ACTION_LABELS } from '../combat/CombatSystem.js';
import { LIFE_STAGES } from '../lifecycle/LifecycleManager.js';

const TILE_SIZE = 32;
const MOVE_COOLDOWN = 0.15;

const TILESET_COLUMNS = 8;
const TILESET_PATH = 'assets/tilesets/city_tileset.png';

const SOLID_TILES = new Set([1, 2, 3, 8, 9, 11, 12, 13, 14, 18, 19, 20, 21]);

// Walking is free. AP is consumed by meaningful actions: POI interaction, NPC dialogue, idle.
// Random encounters removed from walking. Combat triggered by events/story only.

const NEMESIS_BY_STAGE = {
    childhood: ['nemesis_bully_grade6'],
    teen: ['nemesis_bully_grade6', 'nemesis_crammed_bus'],
    young_adult: ['nemesis_crammed_bus', 'nemesis_rent_hike'],
    prime: ['nemesis_rent_hike'],
    middle_age: ['nemesis_rent_hike'],
    elder: []
};

export class ExploreScene {
    constructor(game, params = {}) {
        this.game = game;
        this.params = params;
        this.mapManager = new MapManager(game.assets);
        this.dialogue = new DialogueSystem();
        this.combat = new CombatSystem();

        this.player = null;
        this.district = null;
        this.districtId = null;

        // Tile movement
        this.tileX = 15;
        this.tileY = 10;
        this.visualX = 15 * TILE_SIZE;
        this.visualY = 10 * TILE_SIZE;
        this.facing = 'down';
        this.moveTimer = 0;
        this.moving = false;

        // Visual
        this.bobTimer = 0;
        this.nearbyNpc = null;
        this.nearbyExit = null;
        this.nearbyPoi = null;

        // Mode: exploring | message | dialogue | event | combat | year_end | game_over
        this.mode = 'exploring';

        // Messages
        this.messageQueue = [];
        this.currentMessage = null;

        // Lifecycle
        this.actionsPerYear = 4;
        this.actionsThisYear = 0;
        this.currentAge = 6;
        this.currentStage = 'childhood';
        this.yearEndData = null;

        // Events
        this.activeEvent = null;
        this.eventChoiceIndex = 0;
        this.eventResult = null;
        this.eventResultTimer = 0;

        // Combat
        this.combatEnemy = null;
        this.combatActionIndex = 0;
        this.combatLog = [];
        this.combatResult = null;
        this.combatPendingEnemyTurn = false;
        this.combatTurnDelay = 0;

        // Pending AP consumption (consumed after event/dialogue resolves)
        this._pendingActionConsume = false;

        // HUD
        this.hudVisible = true;
        this.transitioning = false;
        this.loadError = null;

        // Cached data
        this.itemsCache = null;
        this.eventsCache = null;

        // Tileset
        this.tilesetImage = null;
        this.tilesetColumns = TILESET_COLUMNS;
        this._loadTileset();
    }

    _loadTileset() {
        const img = new Image();
        img.src = TILESET_PATH;
        img.onload = () => {
            this.tilesetImage = img;
        };
        img.onerror = () => {
            console.warn('Failed to load tileset:', TILESET_PATH);
        };
    }

    _drawTile(ctx, tileId, screenX, screenY) {
        if (!this.tilesetImage) return;
        const col = tileId % this.tilesetColumns;
        const row = Math.floor(tileId / this.tilesetColumns);
        const srcX = col * TILE_SIZE;
        const srcY = row * TILE_SIZE;
        ctx.drawImage(
            this.tilesetImage,
            srcX, srcY, TILE_SIZE, TILE_SIZE,
            Math.floor(screenX), Math.floor(screenY), TILE_SIZE, TILE_SIZE
        );
    }

    async onEnter(params) {
        const state = this.game.getGameState();
        if (!state || !state.createOptions) {
            this.loadError = 'No save data. Go back and start a new game.';
            return;
        }

        const opts = state.createOptions;
        this.player = new Player({
            name: opts.name,
            district: opts.district,
            origin: opts.origin,
            generation: state.generation || 1
        });

        // Load origin data and apply bonuses
        await this._loadOriginData(opts.origin);

        // Set starting age/stage
        this.player.age = 6;
        this.player.lifeStage = 'childhood';
        this.currentAge = 6;
        this.currentStage = 'childhood';
        this.actionsPerYear = LIFE_STAGES.CHILDHOOD.actionsPerYear;
        this.actionsThisYear = 0;

        this.districtId = opts.district;
        state.currentCharacter = this.player.serialize();

        try {
            this.district = await this.mapManager.switchDistrict(this.districtId);
            if (!this.district) {
                this.loadError = `District data not found: ${this.districtId}`;
                return;
            }
            this.tileX = Math.floor(this.district.width / 2);
            this.tileY = Math.floor(this.district.height / 2);
            this.visualX = this.tileX * TILE_SIZE;
            this.visualY = this.tileY * TILE_SIZE;
        } catch (e) {
            this.loadError = `Failed to load district: ${e.message}`;
            return;
        }

        this._showMessage(`Generation ${this.player.generation}. ${this.player.name}, age 6.`);
        this._showMessage('Kuncheng does not know you yet.');
        this._showMessage('Arrow keys to move. Space to interact with POIs/NPCs. E to end year. I to slack off.');
    }

    // ==================== ORIGIN LOADING ====================

    async _loadOriginData(originId) {
        try {
            const response = await fetch(`src/data/origins/${originId}.json`);
            if (!response.ok) return;
            const data = await response.json();

            // Apply stat modifiers
            if (data.statModifiers) {
                for (const [key, value] of Object.entries(data.statModifiers)) {
                    this.player.modifyStat(key, value);
                }
            }

            // Apply starting money
            if (data.startingMoney) {
                this.player.money = data.startingMoney;
            }

            // Apply component affinity
            if (data.componentAffinity && this.player.body) {
                for (const [type, affinity] of Object.entries(data.componentAffinity)) {
                    const comp = this.player.body.get(type);
                    if (comp) {
                        comp.growth = affinity;
                    }
                }
            }

            // Apply birth lottery
            if (data.birthLottery) {
                this.player.flags.birthLottery = data.birthLottery;
                if (data.birthLottery === 'native') {
                    this.player.flags.rooted = true;
                }
            }

            // Apply starting items
            if (data.startingItems && data.startingItems.length > 0) {
                const items = await this._loadItemsData();
                if (items) {
                    for (const itemId of data.startingItems) {
                        const item = items.find(i => i.id === itemId);
                        if (item) {
                            this.player.addItem({ ...item });
                        }
                    }
                }
            }

            // Apply initial skill
            if (data.initialSkill) {
                this.player.learnSkill({
                    id: data.initialSkill.id,
                    name: data.initialSkill.name,
                    description: data.initialSkill.description
                });
            }
        } catch (e) {
            // Non-fatal: continue with default stats
        }
    }

    async _loadItemsData() {
        if (this.itemsCache) return this.itemsCache;
        try {
            const response = await fetch('src/data/items/items_basic.json');
            if (!response.ok) return null;
            this.itemsCache = await response.json();
            return this.itemsCache;
        } catch (e) {
            return null;
        }
    }

    async _loadEventsData() {
        if (this.eventsCache) return this.eventsCache;
        try {
            const response = await fetch('src/data/events/events_demo.json');
            if (!response.ok) return [];
            this.eventsCache = await response.json();
            return this.eventsCache;
        } catch (e) {
            return [];
        }
    }

    // ==================== UPDATE LOOP ====================

    update(delta) {
        this.bobTimer += delta;

        if (this.loadError) return;

        // Event result timer
        if (this.mode === 'event' && this.eventResult) {
            this.eventResultTimer -= delta;
            if (this.eventResultTimer <= 0) {
                if (this.game.input.isActionPressed('confirm')) {
                    this.activeEvent = null;
                    this.eventResult = null;
                    this.mode = 'exploring';
                }
            }
            return;
        }

        if (this.mode === 'message' && this.currentMessage) {
            if (this.game.input.isActionPressed('confirm')) {
                this.currentMessage = this.messageQueue.shift() || null;
                if (!this.currentMessage) {
                    this.mode = this._pendingMode || 'exploring';
                    this._pendingMode = null;
                }
            }
            return;
        }

        if (this.mode === 'dialogue') {
            if (this.dialogue.isActive()) {
                this._handleDialogueInput();
            } else {
                this.mode = 'exploring';
                if (this._pendingActionConsume) {
                    this._pendingActionConsume = false;
                    this._consumeAction();
                }
            }
            return;
        }

        if (this.mode === 'event') {
            this._handleEventInput();
            return;
        }

        if (this.mode === 'combat') {
            this._handleCombatInput();
            return;
        }

        if (this.mode === 'year_end') {
            if (this.game.input.isActionPressed('confirm')) {
                this._finishYearEnd();
            }
            return;
        }

        if (this.mode === 'game_over') {
            if (this.game.input.isActionPressed('confirm')) {
                this.game.scenes.switchTo('title');
            }
            return;
        }

        if (this.mode === 'loading') return;  // Wait for async operation to complete

        if (this.mode !== 'exploring') return;
        if (this.transitioning) return;

        // NPC interaction — AP consumed when dialogue ends
        if (this.game.input.isActionPressed('confirm') && this.nearbyNpc) {
            this._startDialogue(this.nearbyNpc);
            this._pendingActionConsume = true;
            return;
        }

        // POI interaction — triggers event, AP consumed when event resolves
        if (this.game.input.isActionPressed('confirm') && this.nearbyPoi) {
            this._pendingActionConsume = true;
            this._triggerRandomEvent();
            return;
        }

        // Idle action — consumes 1 AP
        if (this.game.input.isPressed('KeyI') && this.player.age >= 19) {
            this._doIdle();
            return;
        }

        // End year early — skip remaining AP
        if (this.game.input.isPressed('KeyE')) {
            this._startYearEnd();
            return;
        }

        this._updateMovement(delta);
        this._checkProximity();
    }

    // ==================== ACTION POINTS ====================

    _consumeAction() {
        this.actionsThisYear++;
        this.player.stats.actionPoints = Math.max(0, this.actionsPerYear - this.actionsThisYear);
        if (this.actionsThisYear >= this.actionsPerYear) {
            this._startYearEnd();
        }
    }
    }

    // ==================== MOVEMENT ====================

    _updateMovement(delta) {
        this.moveTimer -= delta;

        if (this.moving) {
            const targetX = this.tileX * TILE_SIZE;
            const targetY = this.tileY * TILE_SIZE;
            const dx = targetX - this.visualX;
            const dy = targetY - this.visualY;
            const dist = Math.sqrt(dx * dx + dy * dy);

            if (dist < 2) {
                this.visualX = targetX;
                this.visualY = targetY;
                this.moving = false;
                this._onStepComplete();
            } else {
                const speed = TILE_SIZE / MOVE_COOLDOWN;
                this.visualX += (dx / dist) * speed * delta;
                this.visualY += (dy / dist) * speed * delta;
            }
            return;
        }

        if (this.moveTimer > 0) return;

        const input = this.game.input;
        let dx = 0, dy = 0;

        if (input.isPressed('ArrowUp') || input.isPressed('KeyW')) { dy = -1; this.facing = 'up'; }
        else if (input.isPressed('ArrowDown') || input.isPressed('KeyS')) { dy = 1; this.facing = 'down'; }
        else if (input.isPressed('ArrowLeft') || input.isPressed('KeyA')) { dx = -1; this.facing = 'left'; }
        else if (input.isPressed('ArrowRight') || input.isPressed('KeyD')) { dx = 1; this.facing = 'right'; }

        if (dx === 0 && dy === 0) return;

        const newX = this.tileX + dx;
        const newY = this.tileY + dy;

        if (this._isWalkable(newX, newY)) {
            this.tileX = newX;
            this.tileY = newY;
            this.moving = true;
            this.moveTimer = MOVE_COOLDOWN;
        }
    }

    _onStepComplete() {
        // Check exit first
        this._checkProximity();
        if (this.nearbyExit) {
            this._checkExit();
            return;
        }

        // Walking is free — no AP consumption, no random encounters.
        // Events are triggered by POI interaction (Space near a POI).
        // Combat is triggered by events/story, not random walking.
    }

    _isWalkable(x, y) {
        if (!this.district) return false;
        if (x < 0 || y < 0 || x >= this.district.width || y >= this.district.height) return false;
        const tile = this.district.tiles[y] && this.district.tiles[y][x];
        // Any tile not in SOLID_TILES is walkable; unknown tiles are walkable by default.
        return tile === undefined || !SOLID_TILES.has(tile);
    }

    _checkProximity() {
        this.nearbyNpc = null;
        this.nearbyExit = null;
        this.nearbyPoi = null;

        if (!this.district) return;

        for (const npc of this.district.npcs) {
            const dx = Math.abs(npc.x - this.tileX);
            const dy = Math.abs(npc.y - this.tileY);
            if (dx + dy <= 1) {
                this.nearbyNpc = npc;
                break;
            }
        }

        for (const exit of this.district.exits) {
            if (exit.x === this.tileX && exit.y === this.tileY) {
                this.nearbyExit = exit;
                break;
            }
        }

        for (const point of this.district.points) {
            const dx = Math.abs(point.x - this.tileX);
            const dy = Math.abs(point.y - this.tileY);
            if (dx + dy <= 1) {
                this.nearbyPoi = point;
                break;
            }
        }
    }

    _checkExit() {
        if (!this.nearbyExit) return;
        this._transitionToDistrict(this.nearbyExit.target);
    }

    async _transitionToDistrict(targetId) {
        this.transitioning = true;
        const fromDistrictId = this.districtId;
        this.mode = 'message';
        this._showMessage(`Leaving for ${targetId}...`);

        const newDistrict = await this.mapManager.switchDistrict(targetId);
        if (!newDistrict) {
            this._showMessage(`Can't go there yet. (district data missing)`);
            this.transitioning = false;
            return;
        }

        this.district = newDistrict;
        this.districtId = targetId;

        const reverseExit = newDistrict.exits.find(e => e.target === fromDistrictId);
        if (reverseExit) {
            this.tileX = reverseExit.x;
            this.tileY = reverseExit.y;
        } else {
            this.tileX = Math.floor(newDistrict.width / 2);
            this.tileY = Math.floor(newDistrict.height / 2);
        }

        this.visualX = this.tileX * TILE_SIZE;
        this.visualY = this.tileY * TILE_SIZE;
        this.moving = false;
        this.transitioning = false;
        this._showMessage(`Arrived at ${newDistrict.name}.`);
    }

    // ==================== IDLE ====================

    _doIdle() {
        if (this.player.money < 50) {
            this.mode = 'message';
            this._showMessage('You are too broke to slack off. Go do something.');
            return;
        }
        this.player.money -= 50;
        this.player.flags.weightGain = (this.player.flags.weightGain || 0) + 1;

        const yearEnded = this.actionsThisYear + 1 >= this.actionsPerYear;
        this._consumeAction();

        if (!yearEnded) {
            this.mode = 'message';
            this._showMessage('You slacked off. -$50. Gained weight. Lost an action.');
        }
    }

    // ==================== YEAR END ====================

    _startYearEnd() {
        const oldAge = this.currentAge;
        this.currentAge++;
        this.player.age = this.currentAge;

        // Check stage transition
        let stageChanged = null;
        const newStage = this._getStageForAge(this.currentAge);
        if (newStage && newStage !== this.currentStage) {
            stageChanged = newStage;
            this.currentStage = newStage;
            this.player.lifeStage = newStage;
            this.actionsPerYear = LIFE_STAGES[newStage.toUpperCase()] ? LIFE_STAGES[newStage.toUpperCase()].actionsPerYear : 14;
        }

        // HP change based on age
        let hpChange = 0;
        if (this.currentAge >= 90) {
            const dmg = Math.floor(5 + (this.currentAge - 90) * 0.5);
            this.player.takeDamage(dmg);
            hpChange = -dmg;
        } else if (this.currentAge < 36) {
            // Natural healing for young
            const heal = Math.min(5, this.player.stats.maxHp - this.player.stats.hp);
            this.player.heal(heal);
            hpChange = heal;
        } else {
            // Small healing
            const heal = Math.min(2, this.player.stats.maxHp - this.player.stats.hp);
            this.player.heal(heal);
            hpChange = heal;
        }

        this.yearEndData = {
            oldAge,
            newAge: this.currentAge,
            stageChanged,
            hpChange,
            money: this.player.money,
            stageName: this._getStageName(this.currentStage)
        };

        // Check death
        if (this.player.stats.hp <= 0) {
            this.mode = 'game_over';
            return;
        }

        this.mode = 'year_end';
    }

    _finishYearEnd() {
        this.actionsThisYear = 0;
        this.player.stats.actionPoints = this.actionsPerYear;
        this.yearEndData = null;
        this.mode = 'exploring';
    }

    _getStageForAge(age) {
        for (const stage of Object.values(LIFE_STAGES)) {
            if (age >= stage.ageRange[0] && age <= stage.ageRange[1]) {
                return stage.id;
            }
        }
        return null;
    }

    _getStageName(stageId) {
        const names = {
            baby: 'Baby',
            childhood: 'Childhood',
            teen: 'Teen',
            young_adult: 'Young Adult',
            prime: 'Prime',
            middle_age: 'Middle Age',
            elder: 'Elder'
        };
        return names[stageId] || stageId;
    }

    // ==================== EVENTS ====================

    async _triggerRandomEvent() {
        this.mode = 'event';  // Set immediately to prevent movement during fetch
        const events = await this._loadEventsData();
        if (events.length === 0) {
            this.mode = 'exploring';
            this._pendingActionConsume = false;  // No event, no AP cost
            this._showMessage('Nothing happens here. Try another spot.');
            return;
        }

        const event = events[Math.floor(Math.random() * events.length)];
        this.activeEvent = event;
        this.eventChoiceIndex = 0;
        this.eventResult = null;
    }

    _handleEventInput() {
        const input = this.game.input;

        if (this.eventResult) {
            if (input.isActionPressed('confirm')) {
                this.activeEvent = null;
                this.eventResult = null;
                this.mode = 'exploring';
                if (this._pendingActionConsume) {
                    this._pendingActionConsume = false;
                    this._consumeAction();
                }
            }
            return;
        }

        if (!this.activeEvent || !this.activeEvent.choices) return;

        if (input.isPressed('ArrowUp') || input.isPressed('KeyW')) {
            this.eventChoiceIndex = (this.eventChoiceIndex - 1 + this.activeEvent.choices.length) % this.activeEvent.choices.length;
        }
        if (input.isPressed('ArrowDown') || input.isPressed('KeyS')) {
            this.eventChoiceIndex = (this.eventChoiceIndex + 1) % this.activeEvent.choices.length;
        }
        if (input.isActionPressed('confirm')) {
            this._resolveEventChoice(this.activeEvent.choices[this.eventChoiceIndex]);
        }
    }

    _resolveEventChoice(choice) {
        const c = choice.consequence || {};
        const results = [];

        if (c.money) {
            this.player.money = Math.max(0, this.player.money + c.money);
            results.push(c.money > 0 ? `+$${c.money}` : `-$${Math.abs(c.money)}`);
        }
        if (c.hp) {
            if (c.hp > 0) {
                this.player.heal(c.hp);
                results.push(`+${c.hp} HP`);
            } else {
                this.player.takeDamage(Math.abs(c.hp));
                results.push(`${c.hp} HP`);
            }
        }
        if (c.stamina) {
            this.player.stats.stamina = Math.max(0, this.player.stats.stamina + c.stamina);
            results.push(c.stamina > 0 ? `+${c.stamina} stamina` : `${c.stamina} stamina`);
        }
        if (c.perception) {
            this.player.modifyStat('perception', c.perception);
            results.push(`+${c.perception} perception`);
        }
        if (c.fortune) {
            this.player.modifyStat('fortune', c.fortune);
            results.push(c.fortune > 0 ? `+${c.fortune} fortune` : `${c.fortune} fortune`);
        }
        if (c.weight) {
            this.player.flags.weightGain = (this.player.flags.weightGain || 0) + c.weight;
            results.push(`+${c.weight} weight`);
        }
        if (c.affinity) {
            results.push(`+${c.affinity} affinity`);
        }

        this.eventResult = results.length > 0 ? results.join(', ') : 'nothing happened.';
        this.eventResultTimer = 0.5;

        // Check death from event
        if (this.player.stats.hp <= 0) {
            this.mode = 'game_over';
        }
    }

    // ==================== COMBAT ====================

    _canEncounterNemesis() {
        const nemesisList = NEMESIS_BY_STAGE[this.currentStage];
        return nemesisList && nemesisList.length > 0 && this.currentAge >= 6;
    }

    async _triggerCombat() {
        const nemesisList = NEMESIS_BY_STAGE[this.currentStage];
        if (!nemesisList || nemesisList.length === 0) return;

        this.mode = 'combat';  // Set immediately to prevent movement during fetch
        const nemesisId = nemesisList[Math.floor(Math.random() * nemesisList.length)];

        try {
            const response = await fetch(`src/data/enemies/${nemesisId}.json`);
            if (!response.ok) {
                this.mode = 'exploring';
                return;
            }
            const data = await response.json();

            this.combatEnemy = {
                name: data.name,
                title: data.title,
                eventName: data.eventName,
                stats: {
                    hp: data.stats.hp,
                    maxHp: data.stats.hp,
                    attack: data.stats.attack,
                    defense: data.stats.defense,
                    speed: data.stats.speed
                },
                drops: data.drops
            };

            this.combatLog = [];
            this.combatActionIndex = 0;
            this.combatResult = null;
            this.combatPendingEnemyTurn = false;

            // Start combat
            const playerEntity = {
                name: this.player.name,
                stats: this.player.stats,
                body: this.player.body,
                inventory: this.player.inventory,
                takeDamage: (dmg) => this.player.takeDamage(dmg)
            };

            this.combat.start([playerEntity], [this.combatEnemy], (result, rewards) => {
                this._endCombat(result, rewards);
            });

            this.combatLog.push({ text: `${data.title} ${data.eventName}` });
            this.combatLog.push({ text: data.appearance });
            this.combatLog.push({ text: data.thatMoment });

            this.mode = 'combat';
        } catch (e) {
            this.mode = 'exploring';  // Revert on error
        }
    }

    _handleCombatInput() {
        if (!this.combatEnemy) return;  // Still loading

        if (!this.combat.active || this.combatResult) {
            if (this.combatResult && this.game.input.isActionPressed('confirm')) {
                this.mode = 'exploring';
                this.combatResult = null;
                this.combatEnemy = null;
                this.combatLog = [];
            }
            return;
        }

        if (this.combatPendingEnemyTurn) return;

        const input = this.game.input;
        const actions = this.combat.getAvailableActions({ inventory: this.player.inventory });

        if (input.isPressed('ArrowUp') || input.isPressed('KeyW')) {
            this.combatActionIndex = (this.combatActionIndex - 1 + actions.length) % actions.length;
        }
        if (input.isPressed('ArrowDown') || input.isPressed('KeyS')) {
            this.combatActionIndex = (this.combatActionIndex + 1) % actions.length;
        }
        if (input.isActionPressed('confirm')) {
            const action = actions[this.combatActionIndex];
            this._executeCombatAction(action);
        }
    }

    _executeCombatAction(action) {
        const target = this.combatEnemy;
        const context = { district: this.districtId };

        this.combat.executeAction(action.id, target, context);

        // Sync log (enemy turns already processed synchronously by CombatSystem)
        this.combatLog = [...this.combat.getLog()].slice(-6);

        // Reset action index for next turn
        this.combatActionIndex = 0;
    }

    _processEnemyTurn() {
        if (!this.combat.active) return;

        // The CombatSystem._nextTurn auto-processes enemy turns, but we need to sync
        // Actually, CombatSystem already handled enemy turns in executeAction -> _nextTurn
        // So we just need to sync the log and check if combat ended
        this.combatLog = [...this.combat.getLog()].slice(-6);

        if (!this.combat.active && this.combatResult) {
            // Combat ended during enemy turn
            return;
        }
    }

    _endCombat(result, rewards) {
        this.combatResult = { result, rewards };

        if (result === 'victory' && rewards) {
            this.player.money += rewards.money || 0;
            if (this.combatEnemy.drops && this.combatEnemy.drops.resonanceShards) {
                this.player.resonanceShards += this.combatEnemy.drops.resonanceShards;
            }
        } else if (result === 'defeat') {
            this.player.money = Math.max(0, this.player.money - 30);
            this.player.takeDamage(10);
            if (this.player.stats.hp <= 0) {
                this.mode = 'game_over';
                return;
            }
        }

        this.combatLog.push({ text: `Combat ended: ${result}` });
        if (rewards) {
            this.combatLog.push({ text: `Got $${rewards.money || 0}` });
        }
    }

    // ==================== DIALOGUE ====================

    async _startDialogue(npc) {
        this.mode = 'loading';  // Prevent input during fetch
        try {
            const response = await fetch(`src/data/dialogue/${npc.dialogueId}.json`);
            if (!response.ok) {
                this.mode = 'message';
                this._showMessage(`${npc.name} doesn't seem to want to talk.`);
                this._pendingActionConsume = false;  // Dialogue didn't start, no AP cost
                return;
            }
            const dialogueTree = await response.json();
            this.mode = 'dialogue';
            this.dialogue.start(dialogueTree, npc.name, (history) => {
                // Dialogue ended via callback — consume AP here
                if (this._pendingActionConsume) {
                    this._pendingActionConsume = false;
                    this._consumeAction();
                }
                if (this.mode !== 'year_end' && this.mode !== 'game_over') {
                    this.mode = 'message';
                    this._showMessage(`(conversation ended)`);
                }
            });
        } catch (e) {
            this.mode = 'message';
            this._showMessage(`${npc.name} stares at you. nothing happens.`);
            this._pendingActionConsume = false;  // Dialogue didn't start, no AP cost
        }
    }

    _handleDialogueInput() {
        const input = this.game.input;
        const line = this.dialogue.getCurrentLine();
        if (!line) return;

        if (line.choices.length === 0) {
            if (input.isActionPressed('confirm')) {
                this.dialogue.advance();
            }
            return;
        }

        if (line.choices.length === 1 && line.choices[0].text === '...') {
            if (input.isActionPressed('confirm')) {
                this.dialogue.selectChoice(line.choices[0].id);
            }
            return;
        }

        if (!this._dialogueChoiceIndex) this._dialogueChoiceIndex = 0;
        if (input.isPressed('ArrowUp') || input.isPressed('KeyW')) {
            this._dialogueChoiceIndex = (this._dialogueChoiceIndex - 1 + line.choices.length) % line.choices.length;
        }
        if (input.isPressed('ArrowDown') || input.isPressed('KeyS')) {
            this._dialogueChoiceIndex = (this._dialogueChoiceIndex + 1) % line.choices.length;
        }
        if (input.isActionPressed('confirm')) {
            this.dialogue.selectChoice(line.choices[this._dialogueChoiceIndex].id);
            this._dialogueChoiceIndex = 0;
        }
    }

    // ==================== MESSAGES ====================

    _showMessage(text) {
        this.messageQueue.push({ text, duration: 0 });
        if (!this.currentMessage) {
            this.currentMessage = this.messageQueue.shift();
        }
        if (this.mode === 'exploring') {
            this.mode = 'message';
        }
    }

    // ==================== RENDER ====================

    render(ctx, camera) {
        const w = this.game.renderer.width;
        const h = this.game.renderer.height;

        ctx.fillStyle = '#0f0f1a';
        ctx.fillRect(0, 0, w, h);

        if (this.loadError) {
            ctx.fillStyle = '#e04040';
            ctx.font = '16px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText(this.loadError, w / 2, h / 2);
            return;
        }

        if (!this.district) {
            ctx.fillStyle = '#606080';
            ctx.font = '14px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText('Loading...', w / 2, h / 2);
            return;
        }

        // Always render map and entities underneath
        this._renderMap(ctx, w, h);
        this._renderNpcs(ctx);
        this._renderPlayer(ctx);
        this._renderHUD(ctx, w, h);

        // Overlay UI based on mode
        if (this.mode === 'combat') {
            this._renderCombat(ctx, w, h);
        } else if (this.mode === 'event') {
            this._renderEvent(ctx, w, h);
        } else if (this.mode === 'year_end') {
            this._renderYearEnd(ctx, w, h);
        } else if (this.mode === 'game_over') {
            this._renderGameOver(ctx, w, h);
        } else if (this.mode === 'dialogue' && this.dialogue.isActive()) {
            this._renderDialogue(ctx, w, h);
        } else if (this.mode === 'message' && this.currentMessage) {
            this._renderMessage(ctx, w, h);
        } else {
            this._renderInteractionPrompt(ctx, w, h);
        }
    }

    _renderMap(ctx, w, h) {
        const camX = this.visualX - w / 2 + TILE_SIZE / 2;
        const camY = this.visualY - h / 2 + TILE_SIZE / 2;

        const startTileX = Math.max(0, Math.floor(camX / TILE_SIZE));
        const startTileY = Math.max(0, Math.floor(camY / TILE_SIZE));
        const endTileX = Math.min(this.district.width, Math.ceil((camX + w) / TILE_SIZE) + 1);
        const endTileY = Math.min(this.district.height, Math.ceil((camY + h) / TILE_SIZE) + 1);

        for (let y = startTileY; y < endTileY; y++) {
            for (let x = startTileX; x < endTileX; x++) {
                const tile = this.district.tiles[y] && this.district.tiles[y][x];
                const screenX = x * TILE_SIZE - camX;
                const screenY = y * TILE_SIZE - camY;

                if (this.tilesetImage) {
                    this._drawTile(ctx, tile || 0, screenX, screenY);
                } else {
                    // Fallback if tileset hasn't loaded yet
                    ctx.fillStyle = '#1a1a2e';
                    ctx.fillRect(screenX, screenY, TILE_SIZE, TILE_SIZE);
                }
            }
        }

        for (const point of this.district.points) {
            const screenX = point.x * TILE_SIZE - camX;
            const screenY = point.y * TILE_SIZE - camY;
            // Pulsing glow circle
            const pulse = 0.6 + 0.4 * Math.sin(this.bobTimer * 3);
            ctx.fillStyle = `rgba(192, 160, 96, ${0.15 * pulse})`;
            ctx.beginPath();
            ctx.arc(screenX + TILE_SIZE / 2, screenY + TILE_SIZE / 2, TILE_SIZE * 0.5, 0, Math.PI * 2);
            ctx.fill();
            // Star marker
            ctx.fillStyle = `rgba(192, 160, 96, ${pulse})`;
            ctx.font = 'bold 16px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText('\u2605', screenX + TILE_SIZE / 2, screenY + TILE_SIZE / 2);
            ctx.textBaseline = 'alphabetic';
        }

        for (const exit of this.district.exits) {
            const screenX = exit.x * TILE_SIZE - camX;
            const screenY = exit.y * TILE_SIZE - camY;
            ctx.fillStyle = 'rgba(93, 202, 165, 0.2)';
            ctx.fillRect(screenX, screenY, TILE_SIZE, TILE_SIZE);
            ctx.strokeStyle = '#5dcaa5';
            ctx.lineWidth = 1;
            ctx.strokeRect(screenX + 2, screenY + 2, TILE_SIZE - 4, TILE_SIZE - 4);
        }
    }

    _renderNpcs(ctx) {
        const camX = this.visualX - this.game.renderer.width / 2 + TILE_SIZE / 2;
        const camY = this.visualY - this.game.renderer.height / 2 + TILE_SIZE / 2;
        const scale = TILE_SIZE / 24;

        for (const npc of this.district.npcs) {
            const screenX = npc.x * TILE_SIZE - camX;
            const screenY = npc.y * TILE_SIZE - camY;
            const bob = Math.sin(this.bobTimer * 3 + npc.x) * 1;

            const bodyW = 16 * scale;
            const bodyH = 18 * scale;
            const footH = 2 * scale;
            const eyeSize = 3 * scale;

            ctx.fillStyle = '#c0a060';
            ctx.fillRect(screenX + 4 * scale, screenY + 2 * scale + bob, bodyW, bodyH);

            ctx.fillStyle = '#3a3a2e';
            ctx.fillRect(screenX + 4 * scale, screenY + TILE_SIZE - 6 * scale + bob, bodyW, footH);

            ctx.fillStyle = '#e0e0c0';
            ctx.fillRect(screenX + 7 * scale, screenY + 5 * scale + bob, eyeSize, eyeSize);
            ctx.fillRect(screenX + 14 * scale, screenY + 5 * scale + bob, eyeSize, eyeSize);

            if (this.nearbyNpc && this.nearbyNpc.id === npc.id) {
                ctx.fillStyle = '#5dcaa5';
                ctx.font = `${10 * scale}px "Microsoft YaHei", sans-serif`;
                ctx.textAlign = 'center';
                const nameY = screenY - 4 * scale + Math.sin(this.bobTimer * 4) * 2;
                ctx.fillText(npc.name, screenX + TILE_SIZE / 2, nameY);
            }
        }
    }

    _renderPlayer(ctx) {
        const camX = this.visualX - this.game.renderer.width / 2 + TILE_SIZE / 2;
        const camY = this.visualY - this.game.renderer.height / 2 + TILE_SIZE / 2;
        const screenX = this.visualX - camX;
        const screenY = this.visualY - camY;
        const bob = this.moving ? Math.sin(this.bobTimer * 10) * 2 : Math.sin(this.bobTimer * 2) * 0.5;
        const scale = TILE_SIZE / 24;

        ctx.fillStyle = '#5dcaa5';
        ctx.fillRect(screenX - 7 * scale, screenY - 10 * scale + bob, 14 * scale, 18 * scale);

        ctx.fillStyle = '#3a3a5e';
        ctx.fillRect(screenX - 7 * scale, screenY + 6 * scale + bob, 14 * scale, 3 * scale);

        ctx.fillStyle = '#e0e0f0';
        ctx.fillRect(screenX - 5 * scale, screenY - 8 * scale + bob, 4 * scale, 4 * scale);
        ctx.fillRect(screenX + 1 * scale, screenY - 8 * scale + bob, 4 * scale, 4 * scale);

        ctx.fillStyle = '#1a1a2e';
        if (this.facing === 'down') {
            ctx.fillRect(screenX - 3 * scale, screenY - 6 * scale + bob, 2 * scale, 2 * scale);
            ctx.fillRect(screenX + 1 * scale, screenY - 6 * scale + bob, 2 * scale, 2 * scale);
        } else if (this.facing === 'up') {
            ctx.fillStyle = '#3a3a5e';
            ctx.fillRect(screenX - 5 * scale, screenY - 8 * scale + bob, 10 * scale, 3 * scale);
        } else if (this.facing === 'left') {
            ctx.fillRect(screenX - 4 * scale, screenY - 6 * scale + bob, 2 * scale, 2 * scale);
        } else if (this.facing === 'right') {
            ctx.fillRect(screenX + 2 * scale, screenY - 6 * scale + bob, 2 * scale, 2 * scale);
        }
    }

    _renderHUD(ctx, w, h) {
        if (!this.hudVisible) return;
        if (!this.player) return;

        ctx.save();

        // Main HUD panel
        const hudW = 240;
        const hudH = 110;
        ctx.fillStyle = 'rgba(15, 15, 26, 0.9)';
        ctx.fillRect(10, 10, hudW, hudH);
        ctx.strokeStyle = '#3a3a5e';
        ctx.lineWidth = 0.5;
        ctx.strokeRect(10, 10, hudW, hudH);

        ctx.textAlign = 'left';

        // Name
        ctx.font = 'bold 13px "Microsoft YaHei", sans-serif';
        ctx.fillStyle = '#5dcaa5';
        ctx.fillText(this.player.name, 20, 28);

        // Gen, Age, Stage
        ctx.font = '11px "Microsoft YaHei", sans-serif';
        ctx.fillStyle = '#8080a0';
        ctx.fillText(`Gen ${this.player.generation} | Age ${this.currentAge} | ${this._getStageName(this.currentStage)}`, 20, 44);
        ctx.fillText(`District: ${this.district ? this.district.name : '???'}`, 20, 58);

        // HP bar
        const hpRatio = this.player.stats.hp / this.player.stats.maxHp;
        ctx.fillStyle = '#2a2a4e';
        ctx.fillRect(20, 68, 100, 8);
        ctx.fillStyle = hpRatio > 0.3 ? '#5dcaa5' : '#e04040';
        ctx.fillRect(20, 68, 100 * hpRatio, 8);
        ctx.fillStyle = '#8080a0';
        ctx.fillText(`HP ${this.player.stats.hp}/${this.player.stats.maxHp}`, 130, 76);

        // AP bar
        const apRatio = this.actionsPerYear > 0 ? (this.actionsPerYear - this.actionsThisYear) / this.actionsPerYear : 0;
        ctx.fillStyle = '#2a2a4e';
        ctx.fillRect(20, 82, 100, 6);
        ctx.fillStyle = '#c0a060';
        ctx.fillRect(20, 82, 100 * apRatio, 6);
        ctx.fillStyle = '#8080a0';
        ctx.fillText(`AP ${this.actionsPerYear - this.actionsThisYear}/${this.actionsPerYear}`, 130, 88);

        // Money
        ctx.fillStyle = '#c0a060';
        ctx.font = 'bold 12px "Microsoft YaHei", sans-serif';
        ctx.fillText(`$${this.player.money || 0}`, 20, 104);

        // Resonance shards
        if (this.player.resonanceShards > 0) {
            ctx.fillStyle = '#a080ff';
            ctx.fillText(`\u25C6 ${this.player.resonanceShards}`, 80, 104);
        }

        // FPS
        ctx.fillStyle = '#303040';
        ctx.font = '10px monospace';
        ctx.textAlign = 'right';
        ctx.fillText(`FPS: ${this.game.fps}`, w - 10, 20);
        ctx.restore();
    }

    _renderInteractionPrompt(ctx, w, h) {
        if (this.nearbyNpc) {
            ctx.save();
            ctx.fillStyle = 'rgba(93, 202, 165, 0.9)';
            ctx.font = 'bold 12px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            if (Math.floor(this.bobTimer * 2) % 2 === 0) {
                ctx.fillText(`[SPACE] Talk to ${this.nearbyNpc.name}`, w / 2, h - 80);
            }
            ctx.restore();
        }

        if (this.nearbyPoi) {
            ctx.save();
            ctx.fillStyle = 'rgba(192, 160, 96, 0.9)';
            ctx.font = 'bold 12px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            if (Math.floor(this.bobTimer * 2) % 2 === 0) {
                ctx.fillText(`[SPACE] Explore ${this.nearbyPoi.name}`, w / 2, h - 80);
            }
            ctx.restore();
        }

        // End year hint
        ctx.save();
        ctx.fillStyle = '#404060';
        ctx.font = '10px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('[E] End year', w / 2, h - 38);

        // Idle hint
        if (this.player && this.player.age >= 19) {
            ctx.fillText('[I] Slack off (-$50, +weight, -AP)', w / 2, h - 20);
        }
        ctx.restore();
    }

    _renderMessage(ctx, w, h) {
        const boxH = 70;
        const boxY = h - boxH - 20;

        ctx.fillStyle = 'rgba(15, 15, 26, 0.95)';
        ctx.fillRect(20, boxY, w - 40, boxH);
        ctx.strokeStyle = '#3a3a5e';
        ctx.lineWidth = 1;
        ctx.strokeRect(20, boxY, w - 40, boxH);

        ctx.fillStyle = '#e0e0e0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'left';
        this._wrapTextLeft(ctx, this.currentMessage.text, 36, boxY + 22, w - 72);

        if (Math.floor(this.bobTimer * 2) % 2 === 0) {
            ctx.fillStyle = '#5dcaa5';
            ctx.font = '11px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'right';
            ctx.fillText('space >', w - 36, boxY + boxH - 12);
        }
    }

    _renderDialogue(ctx, w, h) {
        const line = this.dialogue.getCurrentLine();
        if (!line) return;

        const boxH = 160;
        const boxY = h - boxH - 20;

        ctx.fillStyle = 'rgba(15, 15, 26, 0.97)';
        ctx.fillRect(20, boxY, w - 40, boxH);
        ctx.strokeStyle = '#5dcaa5';
        ctx.lineWidth = 1;
        ctx.strokeRect(20, boxY, w - 40, boxH);

        ctx.fillStyle = '#5dcaa5';
        ctx.fillRect(20, boxY, 4, boxH);

        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 13px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'left';
        ctx.fillText(line.speaker, 36, boxY + 22);

        ctx.fillStyle = '#e0e0e0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        this._wrapTextLeft(ctx, line.text, 36, boxY + 44, w - 72);

        if (line.choices.length > 0) {
            const hasMultipleChoices = line.choices.length > 1 || (line.choices[0].text !== '...');
            if (hasMultipleChoices) {
                line.choices.forEach((choice, i) => {
                    const cy = boxY + 70 + i * 20;
                    const selected = i === (this._dialogueChoiceIndex || 0);
                    ctx.fillStyle = selected ? '#5dcaa5' : '#606080';
                    ctx.font = selected ? 'bold 13px "Microsoft YaHei", sans-serif' : '12px "Microsoft YaHei", sans-serif';
                    ctx.fillText((selected ? '> ' : '  ') + choice.text, 44, cy);
                });
            }
        }
    }

    _renderEvent(ctx, w, h) {
        if (!this.activeEvent) return;

        const boxH = 200;
        const boxY = h / 2 - boxH / 2;

        // Dim background
        ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
        ctx.fillRect(0, 0, w, h);

        // Event box
        ctx.fillStyle = 'rgba(15, 15, 26, 0.98)';
        ctx.fillRect(40, boxY, w - 80, boxH);
        ctx.strokeStyle = '#c0a060';
        ctx.lineWidth = 1;
        ctx.strokeRect(40, boxY, w - 80, boxH);

        ctx.fillStyle = '#c0a060';
        ctx.fillRect(40, boxY, 4, boxH);

        if (this.eventResult) {
            ctx.fillStyle = '#5dcaa5';
            ctx.font = 'bold 14px "Microsoft YaHei", sans-serif';
            ctx.textAlign = 'center';
            ctx.fillText('Result', w / 2, boxY + 30);

            ctx.fillStyle = '#e0e0e0';
            ctx.font = '14px "Microsoft YaHei", sans-serif';
            this._wrapTextCenter(ctx, this.eventResult, w / 2, boxY + 60, w - 120);

            if (Math.floor(this.bobTimer * 2) % 2 === 0) {
                ctx.fillStyle = '#5dcaa5';
                ctx.font = '11px "Microsoft YaHei", sans-serif';
                ctx.fillText('space to continue', w / 2, boxY + boxH - 20);
            }
            return;
        }

        // Title
        ctx.fillStyle = '#c0a060';
        ctx.font = 'bold 15px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(this.activeEvent.title, w / 2, boxY + 28);

        // Description
        ctx.fillStyle = '#e0e0e0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        this._wrapTextCenter(ctx, this.activeEvent.description, w / 2, boxY + 55, w - 120);

        // Choices
        if (this.activeEvent.choices) {
            this.activeEvent.choices.forEach((choice, i) => {
                const cy = boxY + 100 + i * 24;
                const selected = i === this.eventChoiceIndex;
                ctx.fillStyle = selected ? '#5dcaa5' : '#606080';
                ctx.font = selected ? 'bold 14px "Microsoft YaHei", sans-serif' : '13px "Microsoft YaHei", sans-serif';
                ctx.textAlign = 'left';
                ctx.fillText((selected ? '> ' : '  ') + choice.text, 60, cy);
            });
        }

        ctx.fillStyle = '#404060';
        ctx.font = '11px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('up/down to choose, space to confirm', w / 2, boxY + boxH - 12);
    }

    _renderCombat(ctx, w, h) {
        // Dim background
        ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.fillRect(0, 0, w, h);

        const boxY = 40;
        const boxH = h - 80;

        // Combat box
        ctx.fillStyle = 'rgba(15, 15, 26, 0.98)';
        ctx.fillRect(20, boxY, w - 40, boxH);
        ctx.strokeStyle = '#e04040';
        ctx.lineWidth = 1;
        ctx.strokeRect(20, boxY, w - 40, boxH);

        ctx.fillStyle = '#e04040';
        ctx.fillRect(20, boxY, 4, boxH);

        if (this.combatResult) {
            this._renderCombatResult(ctx, w, h, boxY, boxH);
            return;
        }

        if (!this.combatEnemy) {
            ctx.textAlign = 'center';
            ctx.fillStyle = '#e04040';
            ctx.font = 'bold 16px "Microsoft YaHei", sans-serif';
            ctx.fillText('...', w / 2, h / 2);
            return;
        }

        // Enemy info
        ctx.textAlign = 'left';
        ctx.fillStyle = '#e04040';
        ctx.font = 'bold 16px "Microsoft YaHei", sans-serif';
        ctx.fillText(this.combatEnemy.name, 40, boxY + 28);

        // Enemy HP bar
        const enemyHpRatio = this.combatEnemy.stats.hp / this.combatEnemy.stats.maxHp;
        ctx.fillStyle = '#2a2a4e';
        ctx.fillRect(40, boxY + 38, 200, 10);
        ctx.fillStyle = enemyHpRatio > 0.3 ? '#e04040' : '#600000';
        ctx.fillRect(40, boxY + 38, 200 * enemyHpRatio, 10);
        ctx.fillStyle = '#8080a0';
        ctx.font = '11px "Microsoft YaHei", sans-serif';
        ctx.fillText(`HP ${this.combatEnemy.stats.hp}/${this.combatEnemy.stats.maxHp}`, 250, boxY + 47);

        // Player info
        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 14px "Microsoft YaHei", sans-serif';
        ctx.fillText(this.player.name, 40, boxY + 70);
        const playerHpRatio = this.player.stats.hp / this.player.stats.maxHp;
        ctx.fillStyle = '#2a2a4e';
        ctx.fillRect(40, boxY + 78, 200, 10);
        ctx.fillStyle = playerHpRatio > 0.3 ? '#5dcaa5' : '#e04040';
        ctx.fillRect(40, boxY + 78, 200 * playerHpRatio, 10);
        ctx.fillStyle = '#8080a0';
        ctx.font = '11px "Microsoft YaHei", sans-serif';
        ctx.fillText(`HP ${this.player.stats.hp}/${this.player.stats.maxHp}`, 250, boxY + 87);

        // Combat log
        ctx.fillStyle = '#a0a0c0';
        ctx.font = '12px "Microsoft YaHei", sans-serif';
        const logStartY = boxY + 110;
        this.combatLog.forEach((entry, i) => {
            ctx.fillStyle = entry.text.includes('attacks') ? '#e08080' : '#a0a0c0';
            this._wrapTextLeft(ctx, entry.text, 40, logStartY + i * 18, w - 80);
        });

        // Actions
        const actions = this.combat.getAvailableActions({ inventory: this.player.inventory });
        const actionsY = boxY + boxH - 120;
        ctx.fillStyle = '#606080';
        ctx.font = '11px "Microsoft YaHei", sans-serif';
        ctx.fillText('Actions:', 40, actionsY);

        actions.forEach((action, i) => {
            const ay = actionsY + 20 + i * 22;
            const selected = i === this.combatActionIndex;
            ctx.fillStyle = selected ? '#5dcaa5' : '#606080';
            ctx.font = selected ? 'bold 14px "Microsoft YaHei", sans-serif' : '13px "Microsoft YaHei", sans-serif';
            ctx.fillText((selected ? '> ' : '  ') + action.label, 50, ay);
        });

        ctx.fillStyle = '#404060';
        ctx.font = '10px "Microsoft YaHei", sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText('up/down to choose, space to confirm', w / 2, boxY + boxH - 12);
    }

    _renderCombatResult(ctx, w, h, boxY, boxH) {
        const r = this.combatResult;
        ctx.textAlign = 'center';

        ctx.fillStyle = r.result === 'victory' ? '#5dcaa5' : '#e04040';
        ctx.font = 'bold 24px "Microsoft YaHei", sans-serif';
        ctx.fillText(r.result === 'victory' ? 'VICTORY' : (r.result === 'fled' ? 'ESCAPED' : 'DEFEATED'), w / 2, boxY + 50);

        ctx.fillStyle = '#e0e0e0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        if (r.rewards) {
            ctx.fillText(`Earned $${r.rewards.money || 0}`, w / 2, boxY + 80);
            if (this.combatEnemy.drops && this.combatEnemy.drops.resonanceShards) {
                ctx.fillStyle = '#a080ff';
                ctx.fillText(`+${this.combatEnemy.drops.resonanceShards} Resonance Shard`, w / 2, boxY + 105);
            }
        } else if (r.result === 'defeat') {
            ctx.fillText('Lost $30. Took damage.', w / 2, boxY + 80);
        }

        if (Math.floor(this.bobTimer * 2) % 2 === 0) {
            ctx.fillStyle = '#5dcaa5';
            ctx.font = '12px "Microsoft YaHei", sans-serif';
            ctx.fillText('space to continue', w / 2, boxY + boxH - 30);
        }
    }

    _renderYearEnd(ctx, w, h) {
        if (!this.yearEndData) return;

        const d = this.yearEndData;
        const boxH = 220;
        const boxY = h / 2 - boxH / 2;

        ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.fillRect(0, 0, w, h);

        ctx.fillStyle = 'rgba(15, 15, 26, 0.98)';
        ctx.fillRect(40, boxY, w - 80, boxH);
        ctx.strokeStyle = '#5dcaa5';
        ctx.lineWidth = 1;
        ctx.strokeRect(40, boxY, w - 80, boxH);

        ctx.fillStyle = '#5dcaa5';
        ctx.fillRect(40, boxY, 4, boxH);

        ctx.textAlign = 'center';
        ctx.fillStyle = '#5dcaa5';
        ctx.font = 'bold 20px "Microsoft YaHei", sans-serif';
        ctx.fillText(`Age ${d.newAge}`, w / 2, boxY + 38);

        ctx.fillStyle = '#8080a0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        ctx.fillText(`Stage: ${d.stageName}`, w / 2, boxY + 62);

        // HP change
        ctx.fillStyle = d.hpChange > 0 ? '#5dcaa5' : (d.hpChange < 0 ? '#e04040' : '#8080a0');
        ctx.font = '13px "Microsoft YaHei", sans-serif';
        const hpText = d.hpChange > 0 ? `HP +${d.hpChange} (recovered)` : (d.hpChange < 0 ? `HP ${d.hpChange} (aging)` : 'HP stable');
        ctx.fillText(hpText, w / 2, boxY + 90);

        // Money
        ctx.fillStyle = '#c0a060';
        ctx.fillText(`Money: $${d.money}`, w / 2, boxY + 112);

        // Stage transition
        if (d.stageChanged) {
            ctx.fillStyle = '#a080ff';
            ctx.font = 'bold 14px "Microsoft YaHei", sans-serif';
            ctx.fillText(`New stage: ${this._getStageName(d.stageChanged)}!`, w / 2, boxY + 140);
            ctx.fillStyle = '#606080';
            ctx.font = '12px "Microsoft YaHei", sans-serif';
            const stageMsgs = {
                teen: 'you noticed something off for the first time.',
                young_adult: 'time to go see the city.',
                prime: 'you are no longer alone.',
                middle_age: 'time to pass things on.',
                elder: 'the last story.'
            };
            if (stageMsgs[d.stageChanged]) {
                ctx.fillText(stageMsgs[d.stageChanged], w / 2, boxY + 162);
            }
        }

        // AP restored
        ctx.fillStyle = '#8080a0';
        ctx.font = '12px "Microsoft YaHei", sans-serif';
        ctx.fillText(`Action points restored: ${this.actionsPerYear}`, w / 2, boxY + 185);

        if (Math.floor(this.bobTimer * 2) % 2 === 0) {
            ctx.fillStyle = '#5dcaa5';
            ctx.font = '12px "Microsoft YaHei", sans-serif';
            ctx.fillText('space to continue', w / 2, boxY + boxH - 15);
        }
    }

    _renderGameOver(ctx, w, h) {
        ctx.fillStyle = 'rgba(0, 0, 0, 0.8)';
        ctx.fillRect(0, 0, w, h);

        ctx.textAlign = 'center';
        ctx.fillStyle = '#e04040';
        ctx.font = 'bold 28px "Microsoft YaHei", sans-serif';
        ctx.fillText('IT ATE YOU', w / 2, h / 2 - 30);

        ctx.fillStyle = '#8080a0';
        ctx.font = '14px "Microsoft YaHei", sans-serif';
        ctx.fillText(`${this.player.name}, age ${this.currentAge}.`, w / 2, h / 2);
        ctx.fillText('Kuncheng moves on. It always does.', w / 2, h / 2 + 22);

        if (Math.floor(this.bobTimer * 2) % 2 === 0) {
            ctx.fillStyle = '#5dcaa5';
            ctx.font = '12px "Microsoft YaHei", sans-serif';
            ctx.fillText('space to return to title', w / 2, h / 2 + 60);
        }
    }

    _wrapTextLeft(ctx, text, x, y, maxWidth) {
        const words = text.split(' ');
        let line = '';
        let lineY = y;
        for (const word of words) {
            const testLine = line + (line ? ' ' : '') + word;
            if (ctx.measureText(testLine).width > maxWidth && line) {
                ctx.fillText(line, x, lineY);
                line = word;
                lineY += 18;
            } else {
                line = testLine;
            }
        }
        ctx.fillText(line, x, lineY);
    }

    _wrapTextCenter(ctx, text, centerX, y, maxWidth) {
        const words = text.split(' ');
        let line = '';
        let lineY = y;
        for (const word of words) {
            const testLine = line + (line ? ' ' : '') + word;
            if (ctx.measureText(testLine).width > maxWidth && line) {
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

    onExit() {
        if (this.player) {
            const state = this.game.getGameState();
            if (state) {
                state.currentCharacter = this.player.serialize();
                this.game.setGameState(state);
            }
        }
    }
}
