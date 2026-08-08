export class MapManager {
    constructor(assetManager) {
        this.assets = assetManager;
        this.loadedDistricts = new Map();
        this.currentDistrictId = null;
        this.maxCached = 3;
    }

    async loadDistrict(districtId) {
        if (this.loadedDistricts.has(districtId)) {
            return this.loadedDistricts.get(districtId);
        }

        const data = await this._fetchDistrictData(districtId);
        if (!data) return null;

        const district = {
            id: districtId,
            name: data.name,
            anomalyType: data.anomalyType,
            tiles: data.tiles,
            width: data.width,
            height: data.height,
            npcs: data.npcs || [],
            exits: data.exits || [],
            points: data.points || [],
            music: data.music
        };

        this.loadedDistricts.set(districtId, district);
        this._evictCache(districtId);

        return district;
    }

    async _fetchDistrictData(districtId) {
        try {
            const response = await fetch(`src/data/districts/${districtId}.json`);
            if (!response.ok) return null;
            return await response.json();
        } catch (e) {
            console.warn(`District data not found: ${districtId}`, e);
            return null;
        }
    }

    _evictCache(keepId) {
        while (this.loadedDistricts.size > this.maxCached) {
            let oldestKey = null;
            for (const key of this.loadedDistricts.keys()) {
                if (key !== keepId && key !== this.currentDistrictId) {
                    oldestKey = key;
                    break;
                }
            }
            if (oldestKey) {
                this.loadedDistricts.delete(oldestKey);
            } else {
                break;
            }
        }
    }

    async switchDistrict(districtId) {
        const district = await this.loadDistrict(districtId);
        if (!district) return false;

        this.currentDistrictId = districtId;
        return district;
    }

    getCurrentDistrict() {
        return this.loadedDistricts.get(this.currentDistrictId);
    }

    getDistrict(districtId) {
        return this.loadedDistricts.get(districtId);
    }

    isWalkable(districtId, x, y) {
        const district = this.loadedDistricts.get(districtId || this.currentDistrictId);
        if (!district) return false;
        if (x < 0 || y < 0 || x >= district.width || y >= district.height) return false;
        const tile = district.tiles[y] && district.tiles[y][x];
        const solidTiles = new Set([1, 2, 3, 8, 9, 11, 12, 13, 14, 18, 19, 20, 21]);
        // Grass(0), roads(4/22/23), sidewalks(5), plaza(6), floor(7), doors(10), bridge(15), parking(17), sand(16) are walkable
        return !solidTiles.has(tile);
    }

    getAdjacentDistricts(districtId) {
        const district = this.loadedDistricts.get(districtId || this.currentDistrictId);
        if (!district || !district.exits) return [];
        return district.exits.filter(e => e.type === 'district').map(e => e.target);
    }

    preloadAdjacent(districtId) {
        const adjacent = this.getAdjacentDistricts(districtId);
        for (const adjId of adjacent) {
            if (!this.loadedDistricts.has(adjId)) {
                this.loadDistrict(adjId);
            }
        }
    }
}
