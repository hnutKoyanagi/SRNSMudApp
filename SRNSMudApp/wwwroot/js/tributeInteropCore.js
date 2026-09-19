(function (root, factory) {
    if (typeof module === 'object' && module.exports) {
        module.exports = factory();
    } else {
        root.tributeInteropCore = factory();
    }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    function extractText(element) {
        if (!element) return '';
        if (element.textContent.trim() === '' && element.querySelectorAll('[data-url]').length === 0) {
            return '';
        }

        function traverse(node) {
            if (!node) return '';
            if (node.nodeType === 3) {
                return node.textContent.replace(/\u00A0/g, ' ');
            }
            if (node.nodeType === 1) {
                if (node.hasAttribute('data-url')) {
                    return node.getAttribute('data-url');
                }
                if (node.hasAttribute('data-extract')) {
                    return node.getAttribute('data-extract');
                }
                if (node.tagName === 'BR') {
                    return '\n';
                }
                var result = '';
                var isBlock = ['DIV', 'P'].includes(node.tagName);
                for (var i = 0; i < node.childNodes.length; i++) {
                    result += traverse(node.childNodes[i]);
                }
                if (isBlock && result.length > 0 && !result.endsWith('\n')) {
                    result += '\n';
                }
                return result;
            }
            return '';
        }

        var text = '';
        for (var i = 0; i < element.childNodes.length; i++) {
            text += traverse(element.childNodes[i]);
        }
        return text;
    }

    function createCompositionSyncController(sync) {
        var isComposing = false;

        return {
            start: function () {
                isComposing = true;
            },
            end: function () {
                isComposing = false;
                sync();
            },
            input: function () {
                if (!isComposing) {
                    sync();
                }
            }
        };
    }

    return {
        extractText: extractText,
        createCompositionSyncController: createCompositionSyncController
    };
});
