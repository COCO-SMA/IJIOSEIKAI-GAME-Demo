const SAVE_KEY = 'kuncheng_rpg_save';
const OLD_SAVE_KEY = 'shenzhen_rpg_save';
const FAMILY_LOG_MAX = 200;
const NPC_MEMORY_MAX = 500;

export class SaveManager {
    constructor() {
        this.currentSave = null;
        this._migrated = false;
        this._storageAvailable = null; // null = not tested yet
    }

    _checkStorage() {
        if (this._storageAvailable !== null) return this._storageAvailable;
        try {
            const testKey = '__kuncheng_test__';
            localStorage.setItem(testKey, '1');
            localStorage.removeItem(testKey);
            this._storageAvailable = true;
        } catch (e) {
            this._storageAvailable = false;
        }
        return this._storageAvailable;
    }

    _ensureMigrated() {
        if (this._migrated) return;
        this._migrated = true;
        if (!this._checkStorage()) return;
        try {
            const oldRaw = localStorage.getItem(OLD_SAVE_KEY);
            if (oldRaw && !localStorage.getItem(SAVE_KEY)) {
                localStorage.setItem(SAVE_KEY, oldRaw);
                localStorage.removeItem(OLD_SAVE_KEY);
            }
        } catch (e) {
            // localStorage may not be available
        }
    }

    save(state) {
        if (!this._checkStorage()) return false;
        this._ensureMigrated();
        const compressed = this._compress(state);
        try {
            localStorage.setItem(SAVE_KEY, JSON.stringify(compressed));
            return true;
        } catch (e) {
            console.error('Save failed:', e);
            return false;
        }
    }

    load() {
        if (!this._checkStorage()) return null;
        this._ensureMigrated();
        try {
            const raw = localStorage.getItem(SAVE_KEY);
            if (!raw) return null;
            return this._decompress(JSON.parse(raw));
        } catch (e) {
            console.error('Load failed:', e);
            return null;
        }
    }

    hasSave() {
        if (!this._checkStorage()) return false;
        this._ensureMigrated();
        return !!localStorage.getItem(SAVE_KEY);
    }

    deleteSave() {
        if (!this._checkStorage()) return;
        try {
            localStorage.removeItem(SAVE_KEY);
        } catch (e) {}
    }

    exportSave() {
        if (!this._checkStorage()) return null;
        try {
            const raw = localStorage.getItem(SAVE_KEY);
            if (!raw) return null;
            const blob = new Blob([raw], { type: 'application/json' });
            return URL.createObjectURL(blob);
        } catch (e) {
            return null;
        }
    }

    _compress(state) {
        const compressed = { ...state };

        if (compressed.familyLog && compressed.familyLog.length > FAMILY_LOG_MAX) {
            const keep = compressed.familyLog.slice(-FAMILY_LOG_MAX);
            const summary = this._summarizeLog(compressed.familyLog.slice(0, -FAMILY_LOG_MAX));
            compressed.familyLog = keep;
            compressed.familyLogSummary = summary;
        }

        if (compressed.npcMemories) {
            for (const [npcId, mem] of Object.entries(compressed.npcMemories)) {
                if (mem.events && mem.events.length > 20) {
                    mem.events = mem.events.slice(-20);
                }
            }
            const npcIds = Object.keys(compressed.npcMemories);
            if (npcIds.length > NPC_MEMORY_MAX) {
                const sorted = npcIds.sort((a, b) =>
                    (compressed.npcMemories[b].familiarity || 0) -
                    (compressed.npcMemories[a].familiarity || 0)
                );
                const kept = {};
                for (const id of sorted.slice(0, NPC_MEMORY_MAX)) {
                    kept[id] = compressed.npcMemories[id];
                }
                compressed.npcMemories = kept;
            }
        }

        return compressed;
    }

    _decompress(state) {
        return state;
    }

    _summarizeLog(entries) {
        return {
            count: entries.length,
            generations: [...new Set(entries.map(e => e.generation))],
            preview: entries.slice(-3).map(e => e.title)
        };
    }

    createNewSave(createOptions) {
        return {
            version: 2,
            createdAt: Date.now(),
            seed: Math.floor(Math.random() * 999999),
            generation: 1,
            currentCharacter: null,
            familyLog: [],
            familyLogSummary: null,
            familyRep: {},
            cityAffinity: {},
            rooted: false,
            npcMemories: {},
            inheritedSkillRecipes: [],
            inheritedItems: [],
            inheritedKnowledge: [],
            resonanceShards: 0,
            forgedEquipment: [],
            createOptions: createOptions
        };
    }
}
