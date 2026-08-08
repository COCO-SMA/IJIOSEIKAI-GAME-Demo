export class EventSystem {
    constructor() {
        this.eventTemplates = new Map();
        this.activeEvents = [];
        this.eventHistory = [];
        this.seed = Math.random();
    }

    registerTemplate(id, template) {
        this.eventTemplates.set(id, template);
    }

    generateEvent(districtId, districtAnomalyType, playerContext) {
        const template = this._selectTemplate(districtAnomalyType, playerContext);
        if (!template) return null;

        const params = this._generateParams(template, districtId, playerContext);
        const event = this._instantiate(template, params);

        event.id = `evt_${Date.now()}_${Math.floor(Math.random() * 1000)}`;
        event.district = districtId;
        event.timestamp = Date.now();

        return event;
    }

    _selectTemplate(anomalyType, context) {
        const matching = [];
        for (const [id, tmpl] of this.eventTemplates) {
            if (tmpl.anomalyTypes && tmpl.anomalyTypes.includes(anomalyType)) {
                matching.push(tmpl);
            } else if (!tmpl.anomalyTypes) {
                matching.push(tmpl);
            }
        }
        if (matching.length === 0) return null;
        return matching[Math.floor(Math.random() * matching.length)];
    }

    _generateParams(template, districtId, context) {
        const params = {};

        if (template.paramSchema) {
            for (const [key, schema] of Object.entries(template.paramSchema)) {
                switch (schema.type) {
                    case 'range':
                        params[key] = schema.min + Math.random() * (schema.max - schema.min);
                        break;
                    case 'choice':
                        params[key] = schema.options[Math.floor(Math.random() * schema.options.length)];
                        break;
                    case 'boolean':
                        params[key] = Math.random() > 0.5;
                        break;
                    case 'npc':
                        params[key] = context.availableNpcs
                            ? context.availableNpcs[Math.floor(Math.random() * context.availableNpcs.length)]
                            : null;
                        break;
                }
            }
        }

        return params;
    }

    _instantiate(template, params) {
        const event = {
            type: template.type,
            title: this._fillTemplate(template.title, params),
            description: this._fillTemplate(template.description, params),
            choices: template.choices.map(c => ({
                id: c.id,
                text: this._fillTemplate(c.text, params),
                consequence: c.consequence,
                conditions: c.conditions || null
            })),
            rewards: template.rewards || [],
            params: params
        };
        return event;
    }

    _fillTemplate(text, params) {
        return text.replace(/\{(\w+)\}/g, (match, key) => {
            return params[key] !== undefined ? params[key] : match;
        });
    }

    resolveEvent(event, choiceId) {
        const choice = event.choices.find(c => c.id === choiceId);
        if (!choice) return null;

        const result = {
            eventId: event.id,
            choiceId: choiceId,
            consequences: [],
            text: ''
        };

        if (choice.consequence) {
            for (const [key, value] of Object.entries(choice.consequence)) {
                result.consequences.push({ type: key, value: value });
            }
        }

        this.eventHistory.push({ event, choiceId, result });
        return result;
    }

    getEventHistory() {
        return this.eventHistory;
    }

    setSeed(seed) {
        this.seed = seed;
    }
}
