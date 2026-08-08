export const COMPONENT_TYPES = {
    LEFT_LEG: 'left_leg',
    RIGHT_LEG: 'right_leg',
    LEFT_HAND: 'left_hand',
    RIGHT_HAND: 'right_hand',
    TORSO: 'torso',
    BRAIN: 'brain'
};

export const COMPONENT_NAMES = {
    left_leg: '左腿',
    right_leg: '右腿',
    left_hand: '左手',
    right_hand: '右手',
    torso: '躯干',
    brain: '大脑'
};

export class BodyComponent {
    constructor(type) {
        this.type = type;
        this.name = COMPONENT_NAMES[type];
        this.efficacy = 10;
        this.stability = 10;
        this.growth = 1.0;
        this.equipment = null;
        this.anomaly = null;
        this.anomalyLevel = 0;
    }

    grow(amount) {
        this.efficacy += amount * this.growth;
        this.stability += amount * this.growth * 0.5;
    }

    setAnomaly(anomalyId, level = 1) {
        this.anomaly = anomalyId;
        this.anomalyLevel = level;
    }

    removeAnomaly() {
        this.anomaly = null;
        this.anomalyLevel = 0;
    }

    equip(item) {
        this.equipment = item;
    }

    unequip() {
        const prev = this.equipment;
        this.equipment = null;
        return prev;
    }

    getTotalEfficacy() {
        let total = this.efficacy;
        if (this.equipment && this.equipment.efficacyBonus) {
            total += this.equipment.efficacyBonus;
        }
        if (this.anomaly && this.anomaly.efficacyMod) {
            total *= this.anomaly.efficacyMod;
        }
        return Math.floor(total);
    }

    serialize() {
        return {
            type: this.type,
            efficacy: this.efficacy,
            stability: this.stability,
            growth: this.growth,
            equipment: this.equipment,
            anomaly: this.anomaly,
            anomalyLevel: this.anomalyLevel
        };
    }

    static deserialize(data) {
        const comp = new BodyComponent(data.type);
        comp.efficacy = data.efficacy;
        comp.stability = data.stability;
        comp.growth = data.growth;
        comp.equipment = data.equipment;
        comp.anomaly = data.anomaly;
        comp.anomalyLevel = data.anomalyLevel;
        return comp;
    }
}

export class BodySystem {
    constructor() {
        this.components = new Map();
        for (const type of Object.values(COMPONENT_TYPES)) {
            this.components.set(type, new BodyComponent(type));
        }
    }

    get(type) {
        return this.components.get(type);
    }

    getAll() {
        return Array.from(this.components.values());
    }

    growAll(amount) {
        for (const comp of this.components.values()) {
            comp.grow(amount);
        }
    }

    getComponentByEquipSlot(slot) {
        const slotMap = {
            'left_leg': COMPONENT_TYPES.LEFT_LEG,
            'right_leg': COMPONENT_TYPES.RIGHT_LEG,
            'left_hand': COMPONENT_TYPES.LEFT_HAND,
            'right_hand': COMPONENT_TYPES.RIGHT_HAND,
            'torso': COMPONENT_TYPES.TORSO,
            'brain': COMPONENT_TYPES.BRAIN
        };
        return this.get(slotMap[slot]);
    }

    serialize() {
        const data = {};
        for (const [type, comp] of this.components) {
            data[type] = comp.serialize();
        }
        return data;
    }

    static deserialize(data) {
        const system = new BodySystem();
        for (const [type, compData] of Object.entries(data)) {
            system.components.set(type, BodyComponent.deserialize(compData));
        }
        return system;
    }
}
