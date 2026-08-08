const DEATH_CAUSES = [
    'died peacefully in sleep, unfortunately',
    'lost a chess game to something very old in Yeping. won the next one. then died.',
    'ate something from a Yanao container that was probably not food. it was fine. mostly.',
    'argued with a parking ticket until natural death. the ticket outlived them.',
    'fell asleep on the last train. the one not on the schedule. did not wake up at a station.',
    'the commute card expired. so did they. the timing was suspicious.',
    'tried to cross the road during rush hour. the road won.'
];

const EPILOGUES = [
    'they won. we are not sure what they won.',
    'the commute card still had money on it. and grievances.',
    'the tea restaurant boss still remembers the unpaid bill.',
    'they left behind a receipt from a convenience store. it was 30 years old.',
    'nobody knows what was in the left pocket. probably nothing useful.',
    'the resonance shards hummed for three days after. then stopped. then started again.'
];

const PROPERTY_EVENTS = {
    NORMAL: { chance: 0.50, desc: 'inherited smoothly' },
    DEMOLITION: { chance: 0.15, desc: 'demolished! sudden windfall' },
    UNFINISHED: { chance: 0.10, desc: 'an unfinished building. a cross-generation easter egg.' },
    DISPUTE: { chance: 0.10, desc: 'inheritance dispute. lawyers involved.' },
    MORTGAGE: { chance: 0.10, desc: 'inherited along with its mortgage.' },
    FIRE: { chance: 0.05, desc: 'a fire. the insurance had expired. of course.' }
};

const BIRTH_LOTTERY_CURSE_CHANCE = 0.06;

export class InheritanceSystem {
    constructor() {
        this.maxSkillRecipes = 4;
        this.maxEquipment = 6;
        this.equipmentDurabilityMultiplier = 0.7;
        this.moneyEstateTax = 0.10;
    }

    createInheritance(player, familyLog, citySystem) {
        const skillRecipes = this._extractSkillRecipes(player);
        const equipment = this._selectEquipment(player);
        const money = this._calculateMoney(player);
        const property = this._rollProperty(player);
        const reputation = this._calculateRep(player);
        const districtKnowledge = this._extractDistrictKnowledge(player);
        const resonanceShards = player.resonanceShards || 0;
        const birthLotteryStatus = this._determineBirthLottery(player, citySystem);
        const logEntry = this._createLogEntry(player);

        return {
            skillRecipes: skillRecipes.slice(0, this.maxSkillRecipes),
            equipment: equipment.slice(0, this.maxEquipment),
            money: money,
            property: property,
            familyRep: reputation,
            districtKnowledge: districtKnowledge,
            resonanceShards: resonanceShards,
            birthLotteryStatus: birthLotteryStatus,
            logEntry: logEntry,
            familyLog: [...familyLog, logEntry]
        };
    }

    _extractSkillRecipes(player) {
        const recipes = [];
        if (player.knownRecipes) {
            for (const recipe of player.knownRecipes) {
                recipes.push({
                    id: recipe.id || recipe,
                    name: recipe.name || recipe,
                    difficultyMultiplier: 0.5,
                    inherited: true
                });
            }
        }
        return recipes;
    }

    _selectEquipment(player) {
        const equipped = [];
        if (player.body) {
            for (const comp of player.body.getAll()) {
                if (comp.equipment) {
                    equipped.push({
                        ...comp.equipment,
                        durability: Math.floor((comp.equipment.durability || 100) * this.equipmentDurabilityMultiplier)
                    });
                }
            }
        }
        return equipped;
    }

    _calculateMoney(player) {
        const raw = player.money || 0;
        return Math.floor(raw * (1 - this.moneyEstateTax));
    }

    _rollProperty(player) {
        if (!player.flags || !player.flags.hasProperty) return null;

        const roll = Math.random();
        let cumulative = 0;
        for (const [key, event] of Object.entries(PROPERTY_EVENTS)) {
            cumulative += event.chance;
            if (roll < cumulative) {
                return {
                    type: key.toLowerCase(),
                    desc: event.desc,
                    district: player.flags.propertyDistrict || player.district
                };
            }
        }
        return { type: 'normal', desc: PROPERTY_EVENTS.NORMAL.desc, district: player.district };
    }

    _calculateRep(player) {
        const rep = {};
        if (player.district) {
            rep[player.district] = 10;
        }
        if (player.quests) {
            for (const quest of player.quests) {
                if (quest.completed && quest.district) {
                    rep[quest.district] = (rep[quest.district] || 0) + 5;
                }
            }
        }
        return rep;
    }

    _extractDistrictKnowledge(player) {
        const knowledge = {};
        if (player.flags && player.flags.discoveredPOIs) {
            for (const poi of player.flags.discoveredPOIs) {
                const dist = poi.district || player.district;
                if (!knowledge[dist]) knowledge[dist] = [];
                knowledge[dist].push(poi);
            }
        }
        return knowledge;
    }

    _determineBirthLottery(player, citySystem) {
        const wasNative = player.flags && player.flags.birthLottery === 'native';

        if (wasNative) {
            if (Math.random() < BIRTH_LOTTERY_CURSE_CHANCE) {
                return {
                    status: 'drifter',
                    cursed: true,
                    curseDesc: 'your parents went back to their hometown. you arrive in Kuncheng from zero.'
                };
            }
            return { status: 'native', cursed: false };
        }

        if (citySystem && citySystem.isRooted()) {
            return { status: 'native', cursed: false, newlyNative: true };
        }

        return { status: 'drifter', cursed: false };
    }

    _createLogEntry(player) {
        const cause = DEATH_CAUSES[Math.floor(Math.random() * DEATH_CAUSES.length)];
        return {
            generation: player.generation,
            name: player.name,
            age: player.age,
            district: player.district,
            origin: player.origin,
            causeOfDeath: cause,
            title: this._generateTitle(player),
            timestamp: Date.now()
        };
    }

    _generateTitle(player) {
        const epilogue = EPILOGUES[Math.floor(Math.random() * EPILOGUES.length)];
        const templates = [
            `Generation ${player.generation}, ${player.name}. ${epilogue}`,
            `Generation ${player.generation}. ${player.name} from ${player.district}. ${epilogue}`,
            `${player.generation}th generation. ${player.name}. ${epilogue}`
        ];
        return templates[Math.floor(Math.random() * templates.length)];
    }

    applyInheritance(newPlayer, inheritance) {
        if (inheritance.skillRecipes) {
            newPlayer.knownRecipes = inheritance.skillRecipes.map(r => ({
                ...r,
                difficultyMultiplier: (r.difficultyMultiplier || 1) * 0.5
            }));
        }

        if (inheritance.equipment) {
            for (const item of inheritance.equipment) {
                newPlayer.addItem(item);
            }
        }

        if (inheritance.money) {
            newPlayer.money += inheritance.money;
        }

        if (inheritance.familyRep) {
            newPlayer.flags = newPlayer.flags || {};
            newPlayer.flags.familyRep = inheritance.familyRep;
        }

        if (inheritance.resonanceShards) {
            newPlayer.resonanceShards = inheritance.resonanceShards;
        }

        if (inheritance.birthLotteryStatus) {
            newPlayer.flags = newPlayer.flags || {};
            newPlayer.flags.birthLottery = inheritance.birthLotteryStatus.status;
            if (inheritance.birthLotteryStatus.cursed) {
                newPlayer.flags.returnCurse = true;
            }
            if (inheritance.birthLotteryStatus.newlyNative) {
                newPlayer.flags.newlyNative = true;
            }
        }

        if (inheritance.property) {
            newPlayer.flags = newPlayer.flags || {};
            if (inheritance.property.type === 'normal' || inheritance.property.type === 'mortgage') {
                newPlayer.flags.hasProperty = true;
                newPlayer.flags.propertyDistrict = inheritance.property.district;
            } else if (inheritance.property.type === 'demolition') {
                newPlayer.money += 5000;
            } else if (inheritance.property.type === 'unfinished') {
                newPlayer.flags.unfinishedProperty = inheritance.property.district;
            }
        }
    }

    applyCrossGenNpcMemory(citySystem, generation) {
        if (!citySystem) return;
        for (const npcId of Object.keys(citySystem.npcMemories)) {
            citySystem.ageNpc(npcId, generation);
            const mem = citySystem.getNpcMemory(npcId);
            if (!mem.deceased) {
                mem.attitudeShift = (mem.attitudeShift || 0) + Math.floor(mem.familiarity * 0.1);
            }
        }
    }
}
