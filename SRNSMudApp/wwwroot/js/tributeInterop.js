window.tributeInterop = {
    instances: {},
    extractText: window.tributeInteropCore.extractText,
    init: function (elementId, dotNetHelper) {
        var wrapper = document.getElementById(elementId);
        if (!wrapper) {
            console.error('TributeInterop: wrapper not found for ' + elementId);
            return;
        }
        var element = wrapper.isContentEditable || wrapper.getAttribute('contenteditable') === 'true'
            ? wrapper
            : (wrapper.querySelector('[contenteditable="true"]') || (wrapper.tagName === 'TEXTAREA' || wrapper.tagName === 'INPUT' ? wrapper : wrapper.querySelector('textarea, input')));

        if (!element) {
            console.error('TributeInterop: editable element not found inside ' + elementId);
            return;
        }
        console.log('TributeInterop: initialized successfully on elementId: ' + elementId + ', isContentEditable: ' + element.isContentEditable);

        var tribute = new Tribute({
            collection: [
                {
                    trigger: '#',
                    requireLeadingSpace: false,
                    values: function (text, cb) {
                        console.log('TributeInterop: querying tags for ' + text);
                        dotNetHelper.invokeMethodAsync('SearchTags', text)
                            .then(results => {
                                cb(results);
                            }).catch(err => {
                                console.error(err);
                                cb([]);
                            });
                    },
                    lookup: 'name',
                    fillAttr: 'replacement',
                    selectTemplate: function (item) {
                        if (element.isContentEditable) {
                            return '<span class="internal-link-preview-pill" data-testid="internal-link-preview-pill" data-url="' + item.original.replacement + '" contenteditable="false">' + item.original.name + '</span>&#8203;\u00A0';
                        }
                        return item.original.replacement + ' ';
                    },
                    menuItemTemplate: function (item) {
                        return '<span style="display:flex;align-items:center;"><svg style="width:16px;height:16px;margin-right:4px;" focusable="false" viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M20 10h-8.3l.5-4.8c.1-.5-.1-1.1-.6-1.4-.4-.3-1-.3-1.5-.1L5.3 5.4c-.4.2-.7.6-.8 1.1l-.6 5.5H2c-.6 0-1 .4-1 1s.4 1 1 1h1.7l-.5 4H2c-.6 0-1 .4-1 1s.4 1 1 1h1l-.5 4.8c-.1.5.1 1.1.6 1.4.2.1.4.2.6.2.3 0 .7-.1.9-.3l4.8-1.7c.4-.2.7-.6.8-1.1l.6-5.5h8.3l-.5 4.8c-.1.5.1 1.1.6 1.4.2.1.4.2.6.2.3 0 .7-.1.9-.3l4.8-1.7c.4-.2.7-.6.8-1.1l.6-5.5H22c.6 0 1-.4 1-1s-.4-1-1-1h-1.7l.5-4H22c.6 0 1-.4 1-1s-.4-1-1-1h-1l.5-4.8c.1-.5-.1-1.1-.6-1.4-.4-.3-1-.3-1.5-.1l-4.8 1.7c-.4.2-.7.6-.8 1.1L13.7 10zM6.5 7.1l3.6-1.3-.4 3.2H6.1l.4-1.9zM7.5 16l3.6-1.3-.4 3.2H7.1l.4-1.9zm8-2h-8.3l.6-5.5h8.3l-.6 5.5zm1-5.9l-3.6 1.3.4-3.2h3.6l-.4 1.9zm1 8.9l-3.6 1.3.4-3.2h3.6l-.4 1.9z"></path></svg>' + item.original.name + '</span>';
                    }
                },
                {
                    trigger: '@',
                    requireLeadingSpace: false,
                    values: function (text, cb) {
                        console.log('TributeInterop: querying users for ' + text);
                        dotNetHelper.invokeMethodAsync('SearchUsers', text)
                            .then(results => {
                                cb(results);
                            }).catch(err => {
                                console.error(err);
                                cb([]);
                            });
                    },
                    lookup: 'name',
                    fillAttr: 'replacement',
                    selectTemplate: function (item) {
                        if (element.isContentEditable) {
                            return '<span class="internal-link-preview-pill" data-testid="internal-link-preview-pill" data-url="' + item.original.replacement + '" contenteditable="false">' + item.original.name + '</span>&#8203;\u00A0';
                        }
                        return item.original.replacement + ' ';
                    },
                    menuItemTemplate: function (item) {
                        return '<span style="display:flex;align-items:center;"><svg style="width:16px;height:16px;margin-right:4px;" focusable="false" viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"></path></svg>' + item.original.name + '</span>';
                    }
                }
            ]
        });

        tribute.attach(element);

        if (element.isContentEditable) {
            var hiddenInput = document.getElementById(elementId + '-hidden') || document.querySelector('textarea[name="_newItem.Content"]');
            var compositionSync = window.tributeInteropCore.createCompositionSyncController(sync);

            function sync() {
                var text = window.tributeInterop.extractText(element);
                if (hiddenInput) {
                    hiddenInput.value = text;
                    var event = new Event('input', { bubbles: true });
                    hiddenInput.dispatchEvent(event);
                }
            }

            element.addEventListener('compositionstart', function () {
                compositionSync.start();
            });

            element.addEventListener('compositionend', function () {
                compositionSync.end();
            });

            element.addEventListener('input', compositionSync.input);
            element.addEventListener('tribute-replaced', function (e) {
                console.log('TributeInterop: tribute-replaced on contenteditable');
                compositionSync.end();
            });

            element.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) {
                    e.preventDefault();
                    dotNetHelper.invokeMethodAsync('SubmitForm');
                }
            });

            element.addEventListener('focus', function () {
                if (element.parentElement) {
                    element.parentElement.classList.add('focused');
                }
            });

            element.addEventListener('blur', function () {
                if (element.parentElement) {
                    element.parentElement.classList.remove('focused');
                }
            });
        } else {
            element.addEventListener('tribute-replaced', function (e) {
                console.log('TributeInterop: replaced, dispatching input event');
                var event = new Event('input', { bubbles: true });
                element.dispatchEvent(event);
            });
        }

        this.instances[elementId] = tribute;
    },
    clear: function (elementId) {
        var wrapper = document.getElementById(elementId);
        if (!wrapper) return;
        var element = wrapper.isContentEditable || wrapper.getAttribute('contenteditable') === 'true'
            ? wrapper
            : wrapper.querySelector('[contenteditable="true"]');
        if (element) {
            element.innerHTML = '';
            var hiddenInput = document.getElementById(elementId + '-hidden') || document.querySelector('textarea[name="_newItem.Content"]');
            if (hiddenInput) {
                hiddenInput.value = '';
                var event = new Event('input', { bubbles: true });
                hiddenInput.dispatchEvent(event);
            }
        }
    },
    destroy: function (elementId) {
        var tribute = this.instances[elementId];
        if (tribute) {
            var wrapper = document.getElementById(elementId);
            var element = wrapper && (wrapper.isContentEditable || wrapper.getAttribute('contenteditable') === 'true' || wrapper.tagName === 'TEXTAREA' || wrapper.tagName === 'INPUT')
                ? wrapper
                : (wrapper ? (wrapper.querySelector('[contenteditable="true"]') || wrapper.querySelector('textarea, input')) : null);
            if (element) {
                tribute.detach(element);
            }
            delete this.instances[elementId];
        }
    }
};
