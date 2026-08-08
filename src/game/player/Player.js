import { BodySystem } from './BodyComponent.js';

export class Player {
    constructor(options = {}) {
        this.name = options.name || 'Player';
        this.district = options.district || null;
        this.origin = options.origin || null;
        this.generation = options.generation || 1;

        this.age = 0;
        this.lifeStage = 'childhood';

        this.body = new BodySystem();

        this.stats = {
            hp: 50,
            maxHp: 50,
            stamina: 30,
            maxStamina: 30,
            actionPoints: 2,
            perception: 10,
            fortune: 10,
            resilience: 5
        };

        this.inventory = [];
        this.money = options.startingMoney || 100;
        this.skills = [];
        this.knownRecipes = [];
        this.resonanceShards = 0;

        this.position = { x: 0, y: 0 };
        this.facing = 'down';

        this.currentDistrict = options.district;
        this.quests = [];
        this.flags = {
            birthLottery: options.birthLottery || null,
            rooted: false,
            weightGain: 0
        };
    }

    getStat(key) {
        return this.stats[key] || 0;
    }

    setStat(key, value) {
        this.stats[key] = value;
        if (key === 'maxHp') {
            this.stats.hp = Math.min(this.stats.hp, value);
        }
        if (key === 'maxStamina') {
            this.stats.stamina = Math.min(this.stats.stamina, value);
        }
    }

    modifyStat(key, delta) {
        this.stats[key] = Math.max(0, (this.stats[key] || 0) + delta);
    }

    takeDamage(amount) {
        this.stats.hp = Math.max(0, this.stats.hp - amount);
        return this.stats.hp <= 0;
    }

    heal(amount) {
        this.stats.hp = Math.min(this.stats.maxHp, this.stats.hp + amount);
    }

    useStamina(amount) {
        if (this.stats.stamina < amount) return false;
        this.stats.stamina -= amount;
        return true;
    }

    restoreStamina(amount) {
        this.stats.stamina = Math.min(this.stats.maxStamina, this.stats.stamina + amount);
    }

    addItem(item) {
        this.inventory.push(item);
    }

    removeItem(itemId) {
        const idx = this.inventory.findIndex(i => i.id === itemId);
        if (idx >= 0) {
            return this.inventory.splice(idx, 1)[0];
        }
        return null;
    }

    hasItem(itemId) {
        return this.inventory.some(i => i.id === itemId);
    }

    learnSkill(skill) {
        if (!this.skills.find(s => s.id === skill.id)) {
            this.skills.push(skill);
        }
    }

    setFlag(key, value = true) {
        this.flags[key] = value;
    }

    getFlag(key) {
        return this.flags[key] || false;
    }

    serialize() {
        return {
            name: this.name,
            district: this.district,
            origin: this.origin,
            generation: this.generation,
            age: this.age,
            lifeStage: this.lifeStage,
            body: this.body.serialize(),
            stats: { ...this.stats },
            inventory: this.inventory,
            money: this.money,
            skills: this.skills,
            knownRecipes: this.knownRecipes,
            resonanceShards: this.resonanceShards,
            position: this.position,
            facing: this.facing,
            currentDistrict: this.currentDistrict,
            quests: this.quests,
            flags: this.flags
        };
    }

    static deserialize(data) {
        const player = new Player({
            name: data.name,
            district: data.district,
            origin: data.origin,
            generation: data.generation
        });
        player.age = data.age;
        player.lifeStage = data.lifeStage;
        player.body = BodySystem.deserialize(data.body);
        player.stats = { ...data.stats };
        player.inventory = data.inventory || [];
        player.money = data.money;
        player.skills = data.skills || [];
        player.knownRecipes = data.knownRecipes || [];
        player.resonanceShards = data.resonanceShards || 0;
        player.position = data.position || { x: 0, y: 0 };
        player.facing = data.facing || 'down';
        player.currentDistrict = data.currentDistrict;
        player.quests = data.quests || [];
        player.flags = data.flags || {};
        return player;
    }
}
