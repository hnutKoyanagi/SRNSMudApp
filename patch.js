const fs = require('fs');
let code = fs.readFileSync('SRNSMudApp/wwwroot/js/tributeInterop.js', 'utf8');

// 1. Remove the unwrap loop
code = code.replace(/\/\/ Unwrap existing manual candidates[\s\S]*?\/\/ Apply new candidates/, '// Apply new candidates');

// 2. We need to unwrap only manual candidates that are NO LONGER in the candidates list.
// Or actually, just let them be! If they are no longer in the candidates list, they just stay as a pill until the user clicks it or edits it.
// Actually, it's better to unwrap them if they are invalid.
const unwrapCode = `
        // Unwrap existing manual candidates that are no longer valid candidates
        var oldCandidates = element.querySelectorAll('.link-candidate-pill');
        var hasChanges = false;
        oldCandidates.forEach(function(pill) {
            var tagId = pill.getAttribute('data-tag-id');
            var textStr = pill.getAttribute('data-extract');
            var stillValid = false;
            for (var i = 0; i < candidates.length; i++) {
                if (candidates[i].tagId.toString() === tagId && candidates[i].originalText === textStr && !candidates[i].isAutoReplace) {
                    stillValid = true;
                    break;
                }
            }
            if (!stillValid) {
                if (textStr) {
                    var text = document.createTextNode(textStr);
                    pill.parentNode.replaceChild(text, pill);
                    hasChanges = true;
                }
            }
        });

        // Apply new candidates
`;
code = code.replace('// Apply new candidates', unwrapCode);

// 3. Remove \u200B\u00A0
code = code.replace("fragment.appendChild(document.createTextNode('\\u200B\\u00A0'));", "");

// 4. Change hasMatch to trigger hasChanges
// Wait, regex might match multiple times. We can just use hasChanges = true when a new pill is added.
code = code.replace('hasMatch = true;', 'hasMatch = true; hasChanges = true;');

// 5. Only dispatch event if hasChanges
const syncCode = `        if (doc.activeElement === element) {
            this.setCaretCharacterOffsetWithin(element, caretOffset);
        }

        if (hasChanges) {
            // Trigger sync only if we actually modified the DOM
            var event = new Event('tribute-replaced', { bubbles: true });
            element.dispatchEvent(event);
        }`;
code = code.replace(/if \(doc\.activeElement === element\) \{[\s\S]*?element\.dispatchEvent\(event\);\n        \}/, syncCode);

fs.writeFileSync('SRNSMudApp/wwwroot/js/tributeInterop.js', code);
