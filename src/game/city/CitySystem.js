const DISTRICT_IDS = [
    'jinyong', 'yundun', 'jiuxu', 'tiewei', 'chagang',
    'yanao', 'zhongling', 'yeping', 'hetang', 'chaowan', 'bianpu'
];

const AFFINITY_LEVELS = {
    STRANGER: { min: 0, name: 'stranger', greeting: 'who are you' },
    ACQUAINTANCE: { min: 10, name: 'acquaintance', greeting: 'oh, you again' },
    RECOGNIZED: { min: 30, name: 'regular', greeting: 'you are the X family kid, right?' },
    FAMILIAR: { min: 50, name: 'insider', greeting: 'back again? your dad still owes me money' },
    LOCAL_BOSS: { min: 70, name: 'local_boss', greeting: 'sit down, you know where' }
};

const AFFINITY_DECAY = {
    NOT_LIVING: -2,
    NEGATIVE_EVENT: -10,
    SEVERE_NEGATIVE: -20,
    CROSS_GEN_MULTIPLIER: 0.7
};

const AFFINITY_GAIN = {
    LIVING: 3,
    QUEST: 10,
    NEMESIS: 18,
    EVENT: 3,
    NPC: 6,
    CONSUME: 1
};

export class CitySystem {
    constructor(saveManager) {
        this.save = saveManager;
        this.affinity = {};
        this.npcMemories = {};
        this.rooted = false;
    }

    initFromSave(saveData) {
        this.affinity = saveData.cityAffinity || {};
        this.npcMemories = saveData.npcMemories || {};
        this.rooted = saveData.rooted || false;
    }

    getDistrictAffinity(districtId) {
        return this.affinity[districtId] || 0;
    }

    addDistrictAffinity(districtId, amount) {
        this.affinity[districtId] = Math.min(100, (this.affinity[districtId] || 0) + amount);
    }

    getTotalAffinity() {
        let total = 0;
        for (const d of DISTRICT_IDS) {
            total += this.getDistrictAffinity(d);
        }
        return Math.round(total / DISTRICT_IDS.length);
    }

    getAffinityLevel(districtId) {
        const aff = this.getDistrictAffinity(districtId);
        if (aff >= 70) return 'local_boss';
        if (aff >= 50) return 'insider';
        if (aff >= 30) return 'regular';
        if (aff >= 10) return 'acquaintance';
        return 'stranger';
    }

    getAffinityLevelInfo(districtId) {
        const aff = this.getDistrictAffinity(districtId);
        let level = AFFINITY_LEVELS.STRANGER;
        for (const lv of Object.values(AFFINITY_LEVELS)) {
            if (aff >= lv.min) level = lv;
        }
        return level;
    }

    checkLocalBossTrigger() {
        if (!this.rooted) return false;
        const total = this.getTotalAffinity();
        if (total < 70) return false;
        let count = 0;
        for (const d of DISTRICT_IDS) {
            if (this.getDistrictAffinity(d) >= 60) count++;
        }
        return count >= 5;
    }

    applyYearlyDecay(livingDistrict) {
        for (const d of DISTRICT_IDS) {
            if (d !== livingDistrict) {
                this.addDistrictAffinity(d, AFFINITY_DECAY.NOT_LIVING);
            }
        }
    }

    applyCrossGenDecay() {
        for (const d of DISTRICT_IDS) {
            this.affinity[d] = Math.round((this.affinity[d] || 0) * AFFINITY_DECAY.CROSS_GEN_MULTIPLIER);
        }
    }

    applyNegativeEvent(districtId, severe = false) {
        const amount = severe ? AFFINITY_DECAY.SEVERE_NEGATIVE : AFFINITY_DECAY.NEGATIVE_EVENT;
        this.addDistrictAffinity(districtId, amount);
    }

    setRooted(value) {
        this.rooted = value;
    }

    isRooted() {
        return this.rooted;
    }

    getNpcMemory(npcId) {
        if (!this.npcMemories[npcId]) {
            this.npcMemories[npcId] = {
                familiarity: 0,
                generationMet: [],
                events: [],
                lastInteraction: null,
                deceased: false,
                successor: null,
                attitudeShift: 0
            };
        }
        return this.npcMemories[npcId];
    }

    interactWithNpc(npcId, generation, event = null) {
        const mem = this.getNpcMemory(npcId);
        mem.familiarity += AFFINITY_GAIN.NPC;
        mem.lastInteraction = generation;
        if (!mem.generationMet.includes(generation)) {
            mem.generationMet.push(generation);
        }
        if (event) {
            mem.events.push({ generation, ...event });
            if (mem.events.length > 20) {
                mem.events = mem.events.slice(-20);
            }
        }
        return this.getNpcFamiliarityLevel(npcId);
    }

    getNpcFamiliarityLevel(npcId) {
        const mem = this.getNpcMemory(npcId);
        const fam = mem.familiarity;
        let level = AFFINITY_LEVELS.STRANGER;
        for (const lv of Object.values(AFFINITY_LEVELS)) {
            if (fam >= lv.min) level = lv;
        }
        return level;
    }

    getNpcDialogueModifier(npcId, generation) {
        const mem = this.getNpcMemory(npcId);
        const level = this.getNpcFamiliarityLevel(npcId);
        const firstGen = mem.generationMet[0] || generation;
        const generationsKnown = generation - firstGen;

        return {
            level: level.name,
            greeting: level.greeting,
            generationsKnown,
            isFamily: generationsKnown > 0,
            attitudeShift: mem.attitudeShift || 0,
            tone: this._getTone(level.name, generationsKnown)
        };
    }

    _getTone(levelName, generationsKnown) {
        if (levelName === 'stranger') return 'cold';
        if (levelName === 'acquaintance') return 'neutral';
        if (levelName === 'regular') return 'warm';
        if (levelName === 'insider') return 'casual';
        if (levelName === 'local_boss') return 'rude';
        return 'cold';
    }

    ageNpc(npcId, generation) {
        const mem = this.getNpcMemory(npcId);
        const lastGen = mem.generationMet[mem.generationMet.length - 1];
        if (lastGen && generation - lastGen >= 3) {
            mem.deceased = true;
        }
    }

    serialize() {
        return {
            cityAffinity: this.affinity,
            npcMemories: this.npcMemories,
            rooted: this.rooted
        };
    }
}
