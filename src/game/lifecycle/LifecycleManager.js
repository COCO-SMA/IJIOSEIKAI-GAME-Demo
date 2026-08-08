export const LIFE_STAGES = {
    BABY: {
        id: 'baby',
        name: 'baby',
        ageRange: [0, 5],
        controlLevel: 'none',
        coreActivity: 'auto-played, key choices only, random events',
        actionsPerYear: 0,
        duration: 8
    },
    CHILDHOOD: {
        id: 'childhood',
        name: 'childhood',
        ageRange: [6, 12],
        controlLevel: 'low',
        coreActivity: 'passive growth, key choices, absurdist options',
        actionsPerYear: 4,
        duration: 12
    },
    TEEN: {
        id: 'teen',
        name: 'teen',
        ageRange: [13, 18],
        controlLevel: 'medium',
        coreActivity: 'first exploration, first anomaly, sub-menus',
        actionsPerYear: 7,
        duration: 14
    },
    YOUNG_ADULT: {
        id: 'young_adult',
        name: 'young_adult',
        ageRange: [19, 35],
        controlLevel: 'high',
        coreActivity: 'core adventure, party, combat, marriage eligible',
        actionsPerYear: 14,
        duration: 40
    },
    PRIME: {
        id: 'prime',
        name: 'prime',
        ageRange: [36, 65],
        controlLevel: 'high',
        coreActivity: 'peak power, hard challenges, raise heir, can idle',
        actionsPerYear: 14,
        duration: 36
    },
    MIDDLE_AGE: {
        id: 'middle_age',
        name: 'middle_age',
        ageRange: [66, 80],
        controlLevel: 'medium',
        coreActivity: 'pass on knowledge, manage family, attribute decline',
        actionsPerYear: 14,
        duration: 18
    },
    ELDER: {
        id: 'elder',
        name: 'elder',
        ageRange: [81, 150],
        controlLevel: 'low',
        coreActivity: 'final events, inheritance prep, HP decline accelerates',
        actionsPerYear: 14,
        duration: 12
    }
};

const STAGE_TRANSITIONS = {
    6: { event: 'childhood_begin', text: 'you are old enough to remember things' },
    13: { event: 'first_anomaly', text: 'you noticed something off for the first time' },
    19: { event: 'open_city', text: 'time to go see the city' },
    36: { event: 'heir_born', text: 'you are no longer alone' },
    66: { event: 'inheritance_activate', text: 'time to pass things on' },
    81: { event: 'final_chapter', text: 'the last story' },
    90: { event: 'death_line_normal', text: 'HP decline begins to accelerate' },
    120: { event: 'death_line_extended', text: 'only life-extending items keep you going' },
    150: { event: 'death_line_forced', text: 'time is up.' }
};

const DEATH_LINES = {
    NORMAL: 90,
    EXTENDED: 120,
    FORCED: 150
};

const WEIGHT_STATES = {
    UNDERWEIGHT: { id: 'underweight', name: 'underweight', actionMod: 0.9, dodgeMod: 1.1 },
    NORMAL: { id: 'normal', name: 'normal', actionMod: 1.0, dodgeMod: 1.0 },
    OVERWEIGHT: { id: 'overweight', name: 'overweight', actionMod: 0.95, dodgeMod: 0.9 },
    OBESE: { id: 'obese', name: 'obese', actionMod: 0.85, dodgeMod: 0.75 },
    MODIFIED: { id: 'modified', name: 'modified body', actionMod: 1.0, dodgeMod: 1.0 }
};

const MARRIAGE_AGE = 19;

export class LifecycleManager {
    constructor(player) {
        this.player = player;
        this.currentStage = LIFE_STAGES.BABY;
        this.timeInStage = 0;
        this.transitionTriggered = {};
        this.actionsThisYear = 0;
        this.currentYear = 0;
        this.idleCount = 0;
    }

    initFromSave(saveData) {
        if (saveData && saveData.lifeStage) {
            const stage = Object.values(LIFE_STAGES).find(s => s.id === saveData.lifeStage);
            if (stage) this.currentStage = stage;
        }
    }

    update(delta, gameMinutes = 0) {
        this.timeInStage += delta;
        const stageDuration = this.currentStage.duration * 60;
        const agePerStage = this.currentStage.ageRange[1] - this.currentStage.ageRange[0];

        const progress = this.timeInStage / stageDuration;
        this.player.age = Math.floor(this.currentStage.ageRange[0] + progress * agePerStage);

        this._checkTransition();

        if (this.timeInStage >= stageDuration) {
            this._advanceStage();
        }

        this._checkDeathLine();
    }

    _checkTransition() {
        const age = this.player.age;
        if (STAGE_TRANSITIONS[age] && !this.transitionTriggered[age]) {
            this.transitionTriggered[age] = true;
            return STAGE_TRANSITIONS[age];
        }
        return null;
    }

    _advanceStage() {
        const stages = Object.values(LIFE_STAGES);
        const currentIdx = stages.indexOf(this.currentStage);
        if (currentIdx < stages.length - 1) {
            this.currentStage = stages[currentIdx + 1];
            this.timeInStage = 0;
            this.player.lifeStage = this.currentStage.id;
            return this.currentStage;
        } else {
            return null;
        }
    }

    _checkDeathLine() {
        const age = this.player.age;
        if (age >= DEATH_LINES.FORCED) {
            this.player.flags = this.player.flags || {};
            this.player.flags.forceDeath = true;
        } else if (age >= DEATH_LINES.EXTENDED) {
            if (!this.player.flags || !this.player.flags.lifeExtender) {
                this.player.takeDamage(9999);
            }
        } else if (age >= DEATH_LINES.NORMAL) {
            this.player.takeDamage(delta * 0.5);
        }
    }

    isEndOfLife() {
        if (this.player.flags && this.player.flags.forceDeath) return true;
        if (this.player.hp <= 0 && this.player.age >= 80) return true;
        return this.currentStage === LIFE_STAGES.ELDER && this.timeInStage >= this.currentStage.duration * 60;
    }

    canMarry() {
        return this.player.age >= MARRIAGE_AGE;
    }

    canIdle() {
        return this.player.age >= 19;
    }

    idle() {
        if (!this.canIdle()) return false;
        this.idleCount++;
        this.player.money = Math.max(0, (this.player.money || 0) - 50);
        this.player.flags = this.player.flags || {};
        this.player.flags.weightGain = (this.player.flags.weightGain || 0) + 1;
        if (this.player.stats && this.player.stats.actionPoints) {
            this.player.stats.actionPoints = Math.max(0, this.player.stats.actionPoints - 1);
        }
        return true;
    }

    getWeightState() {
        const gain = (this.player.flags && this.player.flags.weightGain) || 0;
        if (this.player.flags && this.player.flags.bodyModified) return WEIGHT_STATES.MODIFIED;
        if (gain >= 20) return WEIGHT_STATES.OBESE;
        if (gain >= 10) return WEIGHT_STATES.OVERWEIGHT;
        if (gain <= -5) return WEIGHT_STATES.UNDERWEIGHT;
        return WEIGHT_STATES.NORMAL;
    }

    getActionsPerYear() {
        return this.currentStage.actionsPerYear;
    }

    getStageProgress() {
        const stageDuration = this.currentStage.duration * 60;
        return Math.min(1, this.timeInStage / stageDuration);
    }

    getLifeProgress() {
        const totalDuration = Object.values(LIFE_STAGES).reduce((sum, s) => sum + s.duration, 0);
        let elapsed = 0;
        const stages = Object.values(LIFE_STAGES);
        for (const s of stages) {
            if (s === this.currentStage) {
                elapsed += this.timeInStage / 60;
                break;
            }
            elapsed += s.duration;
        }
        return Math.min(1, elapsed / totalDuration);
    }

    getCurrentStage() {
        return this.currentStage;
    }

    isBabyPhase() {
        return this.currentStage === LIFE_STAGES.BABY;
    }

    shouldSkipBabyPhase(generation) {
        return generation > 1;
    }
}
