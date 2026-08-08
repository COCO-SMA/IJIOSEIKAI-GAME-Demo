export class DialogueSystem {
    constructor() {
        this.currentNode = null;
        this.currentSpeaker = null;
        this.dialogueTree = null;
        this.history = [];
        this.onComplete = null;
    }

    start(dialogueTree, speaker, onComplete) {
        this.dialogueTree = dialogueTree;
        this.currentSpeaker = speaker;
        this.onComplete = onComplete;
        this.history = [];
        this.currentNode = dialogueTree.start || dialogueTree.nodes[Object.keys(dialogueTree.nodes)[0]];
        return this.getCurrentLine();
    }

    getCurrentLine() {
        if (!this.currentNode) return null;

        const node = this.currentNode;
        const text = typeof node.text === 'function' ? node.text() : node.text;

        return {
            speaker: node.speaker || this.currentSpeaker,
            text: text,
            portrait: node.portrait || null,
            choices: this._getChoices(node),
            isEnd: !node.choices || node.choices.length === 0
        };
    }

    _getChoices(node) {
        if (!node.choices) return [];

        return node.choices
            .filter(choice => {
                if (!choice.condition) return true;
                return choice.condition();
            })
            .map(choice => ({
                id: choice.id,
                text: typeof choice.text === 'function' ? choice.text() : choice.text,
                next: choice.next
            }));
    }

    selectChoice(choiceId) {
        const node = this.currentNode;
        if (!node || !node.choices) return null;

        const choice = node.choices.find(c => c.id === choiceId);
        if (!choice) return null;

        this.history.push({
            node: this.currentNode.id || 'start',
            choice: choiceId,
            text: typeof choice.text === 'function' ? choice.text() : choice.text
        });

        if (choice.action) {
            choice.action();
        }

        if (choice.next) {
            this.currentNode = this.dialogueTree.nodes[choice.next];
        } else {
            this.currentNode = null;
        }

        if (!this.currentNode) {
            if (this.onComplete) {
                this.onComplete(this.history);
            }
            return null;
        }

        return this.getCurrentLine();
    }

    advance() {
        const line = this.getCurrentLine();
        if (line && line.isEnd) {
            if (this.onComplete) {
                this.onComplete(this.history);
            }
            return null;
        }
        if (line && line.choices.length === 1 && line.choices[0].next) {
            return this.selectChoice(line.choices[0].id);
        }
        return line;
    }

    isActive() {
        return this.currentNode !== null;
    }

    getHistory() {
        return this.history;
    }

    static createSimpleDialogue(speaker, lines, onComplete) {
        const nodes = {};
        let prevId = null;

        lines.forEach((line, i) => {
            const id = `node_${i}`;
            nodes[id] = {
                id: id,
                speaker: line.speaker || speaker,
                text: line.text,
                choices: i < lines.length - 1
                    ? [{ id: 'next', text: '...', next: `node_${i + 1}` }]
                    : []
            };
        });

        return {
            start: nodes['node_0'],
            nodes: nodes
        };
    }
}
