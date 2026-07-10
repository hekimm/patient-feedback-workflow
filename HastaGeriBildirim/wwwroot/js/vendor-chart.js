(function () {
    if (window.Chart) {
        return;
    }

    window.Chart = class {
        constructor(canvas, config) {
            const element = canvas && canvas.getContext ? canvas : null;
            if (!element) {
                return;
            }

            this.canvas = element;
            this.config = config || {};
            this.draw();
        }

        draw() {
            const ctx = this.canvas.getContext('2d');
            if (!ctx) {
                return;
            }

            const width = this.canvas.width || this.canvas.clientWidth || 640;
            const height = this.canvas.height || this.canvas.clientHeight || 180;
            this.canvas.width = width;
            this.canvas.height = height;

            ctx.clearRect(0, 0, width, height);
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, width, height);
            ctx.strokeStyle = '#d9e2ec';
            ctx.lineWidth = 1;

            for (let i = 0; i < 5; i++) {
                const y = 16 + i * ((height - 32) / 4);
                ctx.beginPath();
                ctx.moveTo(32, y);
                ctx.lineTo(width - 16, y);
                ctx.stroke();
            }

            const datasets = (this.config.data && this.config.data.datasets) || [];
            const labels = (this.config.data && this.config.data.labels) || [];
            datasets.forEach((dataset, datasetIndex) => {
                const values = (dataset.data || []).map(Number).filter(value => !Number.isNaN(value));
                if (values.length === 0) {
                    return;
                }

                const max = Math.max(...values, 1);
                const min = Math.min(...values, 0);
                const span = Math.max(max - min, 1);
                const color = dataset.borderColor || dataset.backgroundColor || ['#0d6efd', '#198754', '#dc3545'][datasetIndex % 3];

                ctx.strokeStyle = color;
                ctx.fillStyle = color;
                ctx.lineWidth = 2;
                ctx.beginPath();
                values.forEach((value, index) => {
                    const x = 32 + (labels.length <= 1 ? 0 : index * ((width - 56) / Math.max(labels.length - 1, 1)));
                    const y = height - 24 - ((value - min) / span) * (height - 48);
                    if (index === 0) {
                        ctx.moveTo(x, y);
                    } else {
                        ctx.lineTo(x, y);
                    }
                });
                ctx.stroke();

                values.forEach((value, index) => {
                    const x = 32 + (labels.length <= 1 ? 0 : index * ((width - 56) / Math.max(labels.length - 1, 1)));
                    const y = height - 24 - ((value - min) / span) * (height - 48);
                    ctx.beginPath();
                    ctx.arc(x, y, 3, 0, Math.PI * 2);
                    ctx.fill();
                });
            });
        }
    };
})();
