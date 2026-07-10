(function () {
    document.addEventListener('submit', function (event) {
        const form = event.target;
        if (!form || typeof form.checkValidity !== 'function') {
            return;
        }

        if (!form.checkValidity()) {
            event.preventDefault();
            form.reportValidity();
        }
    }, true);
})();
