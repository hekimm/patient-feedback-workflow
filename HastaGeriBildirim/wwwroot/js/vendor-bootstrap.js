window.bootstrap = window.bootstrap || {};

window.bootstrap.Alert = class {
    constructor(element) {
        this.element = element;
    }

    close() {
        if (this.element && this.element.parentNode) {
            this.element.parentNode.removeChild(this.element);
        }
    }
};
