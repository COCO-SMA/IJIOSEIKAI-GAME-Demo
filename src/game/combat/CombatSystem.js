export const COMBAT_ACTIONS = {
    DIRECT: 'direct',
    IGNORE: 'ignore',
    USE_ITEM: 'use_item',
    SAY_SOMETHING: 'say_something',
    FLEE: 'flee'
};

export const ACTION_LABELS = {
    direct: 'just deal with it',
    ignore: 'pretend nothing happened',
    use_item: 'try something from your bag',
    say_something: 'say something',
    flee: 'get out of here'
};

export class CombatSystem {
    constructor() {
        this.active = false;
        this.turn = 0;
        this.party = [];
        this.enemies = [];
        this.currentActor = null;
        this.turnQueue = [];
        this.log = [];
        this.onCombatEnd = null;
    }

    start(party, enemies, onEnd) {
        this.active = true;
        this.turn = 0;
        this.party = party;
        this.enemies = enemies;
        this.onCombatEnd = onEnd;
        this.log = [];
        this.turnQueue = this._buildTurnQueue();
        this.currentActor = this.turnQueue[0];

        this._addLog('combat started');
    }

    _buildTurnQueue() {
        const all = [
            ...this.party.map(p => ({ entity: p, side: 'party' })),
            ...this.enemies.map(e => ({ entity: e, side: 'enemy' }))
        ];
        all.sort((a, b) => {
            const aSpeed = a.entity.stats ? a.entity.stats.speed || 10 : 10;
            const bSpeed = b.entity.stats ? b.entity.stats.speed || 10 : 10;
            return bSpeed - aSpeed;
        });
        return all;
    }

    executeAction(action, target = null, context = {}) {
        if (!this.active || !this.currentActor) return;

        const actor = this.currentActor;
        const district = context.district || 'unknown';

        switch (action) {
            case COMBAT_ACTIONS.DIRECT:
                this._executeDirect(actor, target, district);
                break;
            case COMBAT_ACTIONS.IGNORE:
                this._executeIgnore(actor, target, district);
                break;
            case COMBAT_ACTIONS.USE_ITEM:
                this._executeUseItem(actor, target, context.item, district);
                break;
            case COMBAT_ACTIONS.SAY_SOMETHING:
                this._executeSaySomething(actor, target, district);
                break;
            case COMBAT_ACTIONS.FLEE:
                this._executeFlee(actor, district);
                break;
        }

        if (this._checkEnd()) return;

        this._nextTurn();
    }

    _executeDirect(actor, target, district) {
        if (!target) {
            target = this.enemies[0];
        }
        const damage = this._calcDamage(actor, target);
        target.stats.hp = Math.max(0, (target.stats.hp || 0) - damage);
        this._addLog(`${actor.name} deals with ${target.name} for ${damage}`);

        if (target.stats && target.onHit) {
            const dialogue = target.onHit();
            if (dialogue) this._addLog(`"${dialogue}"`);
        }
    }

    _executeIgnore(actor, target, district) {
        this._addLog(`${actor.name} pretends nothing happened`);
        if (Math.random() < 0.3) {
            if (target) {
                target.flags = target.flags || {};
                target.flags.erased = true;
                this._addLog(`it... actually stopped existing?`);
                target.stats.hp = 0;
            }
        } else {
            this._addLog(`it did not work. it is still there.`);
        }
    }

    _executeUseItem(actor, target, item, district) {
        if (!item) {
            this._addLog(`${actor.name} fumbles in the bag. finds nothing useful.`);
            return;
        }
        const effect = this._resolveItemEffect(item, district);
        this._addLog(`${actor.name} uses ${item.name}. ${effect.description}`);

        if (effect.damage && target) {
            target.stats.hp = Math.max(0, (target.stats.hp || 0) - effect.damage);
        }
        if (effect.heal) {
            actor.stats.hp = Math.min(actor.stats.maxHp, (actor.stats.hp || 0) + effect.heal);
        }

        if (actor.body) {
            const rightHand = actor.body.get('right_hand');
            if (rightHand && rightHand.anomaly === 'cant_stop_hand' && Math.random() < 0.3) {
                this._addLog(`right hand thinks that was not good enough. does it again.`);
                if (effect.damage && target) {
                    target.stats.hp = Math.max(0, target.stats.hp - effect.damage);
                }
            }
        }
    }

    _executeSaySomething(actor, target, district) {
        const phrases = [
            'this is fine',
            'i have seen weirder things on the commute line',
            'are you from HR?',
            'do you know where building 5 is?',
            'i am just going to pretend you make sense'
        ];
        const phrase = phrases[Math.floor(Math.random() * phrases.length)];
        this._addLog(`${actor.name}: "${phrase}"`);

        if (target && target.stats) {
            if (Math.random() < 0.4) {
                target.stats.confused = (target.stats.confused || 0) + 1;
                this._addLog(`${target.name} looks confused. or more confused than before.`);
            }
        }
    }

    _executeFlee(actor, district) {
        if (actor.body) {
            const leftLeg = actor.body.get('left_leg');
            if (leftLeg && leftLeg.anomaly === 'routing_habit' && Math.random() < 0.5) {
                this._addLog(`${actor.name} tries to flee. left leg has other ideas. takes a detour through a parking lot.`);
                this._addLog(`somehow ended up finding a hidden item on the way out.`);
            }
        }
        this._addLog(`${actor.name} got out of there`);
        this._endCombat('fled');
    }

    _calcDamage(attacker, target) {
        let base = 10;
        if (attacker.body) {
            const rightHand = attacker.body.get('right_hand');
            if (rightHand) {
                base += Math.floor(rightHand.getTotalEfficacy() * 0.5);
            }
        }
        return base + Math.floor(Math.random() * 5);
    }

    _resolveItemEffect(item, district) {
        const baseEffect = item.baseEffect || {};
        const districtEffect = item.districtEffects && item.districtEffects[district];
        return districtEffect || baseEffect;
    }

    _enemyTurn(enemy) {
        const target = this.party[Math.floor(Math.random() * this.party.length)];
        if (!target) return;

        const damage = 5 + Math.floor(Math.random() * 8);
        target.takeDamage(damage);
        this._addLog(`${enemy.name} attacks ${target.name} for ${damage}`);

        if (enemy.onAct) {
            const dialogue = enemy.onAct();
            if (dialogue) this._addLog(`"${dialogue}"`);
        }
    }

    _nextTurn() {
        const currentIdx = this.turnQueue.indexOf(this.currentActor);
        this.currentActor = this.turnQueue[(currentIdx + 1) % this.turnQueue.length];

        if (this.currentActor.side === 'enemy' && this.currentActor.entity.stats.hp > 0) {
            this._enemyTurn(this.currentActor.entity);
            if (this._checkEnd()) return;
            this._nextTurn();
        } else if (this.currentActor.entity.stats.hp <= 0) {
            this._nextTurn();
        }
    }

    _checkEnd() {
        const partyAlive = this.party.some(p => p.stats.hp > 0);
        const enemiesAlive = this.enemies.some(e => e.stats.hp > 0);

        if (!enemiesAlive) {
            this._endCombat('victory');
            return true;
        }
        if (!partyAlive) {
            this._endCombat('defeat');
            return true;
        }
        return false;
    }

    _endCombat(result) {
        this.active = false;
        const rewards = result === 'victory' ? this._calcRewards() : null;
        this._addLog(`combat ended: ${result}`);
        if (this.onCombatEnd) {
            this.onCombatEnd(result, rewards);
        }
    }

    _calcRewards() {
        return {
            exp: this.enemies.length * 15,
            items: [],
            money: Math.floor(Math.random() * 50) + 10
        };
    }

    _addLog(text) {
        this.log.push({ turn: this.turn, text });
    }

    getLog() {
        return this.log;
    }

    getAvailableActions(actor) {
        const actions = [
            { id: COMBAT_ACTIONS.DIRECT, label: ACTION_LABELS.direct },
            { id: COMBAT_ACTIONS.IGNORE, label: ACTION_LABELS.ignore },
            { id: COMBAT_ACTIONS.SAY_SOMETHING, label: ACTION_LABELS.say_something },
            { id: COMBAT_ACTIONS.FLEE, label: ACTION_LABELS.flee }
        ];

        if (actor && actor.inventory && actor.inventory.length > 0) {
            actions.splice(2, 0, { id: COMBAT_ACTIONS.USE_ITEM, label: ACTION_LABELS.use_item });
        }

        return actions;
    }
}
