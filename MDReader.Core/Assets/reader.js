(() => {
    const root = document.getElementById('reader-content');
    if (!root) return;
    let activeQuery = '', matches = [], selected = -1, pending;
    const notify = message => {
        if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage(message);
    };
    const position = () => {
        const height = document.documentElement.scrollHeight - window.innerHeight;
        return height > 0 ? Math.max(0, Math.min(1, window.scrollY / height)) : 0;
    };
    const clear = () => {
        for (const mark of root.querySelectorAll('mark[data-reader-match]')) {
            const parent = mark.parentNode;
            parent.replaceChild(document.createTextNode(mark.textContent), mark);
            parent.normalize();
        }
        matches = []; selected = -1;
    };
    window.mdreader = {
        apply(settings) {
            const p = position();
            document.body.classList.toggle('dark', !!settings.darkMode);
            document.documentElement.style.setProperty('--font-size', Math.max(12, Math.min(32, settings.fontSize)) + 'px');
            document.documentElement.style.setProperty('--content-width', settings.contentWidth ? settings.contentWidth + 'px' : 'none');
            this.restore(p);
        },
        position,
        restore(value) {
            const p = Number.isFinite(value) ? Math.max(0, Math.min(1, value)) : 0;
            requestAnimationFrame(() => requestAnimationFrame(() => window.scrollTo(0, p * Math.max(0, document.documentElement.scrollHeight - window.innerHeight))));
        },
        goTo(id) { const el = document.getElementById(id); if (el) el.scrollIntoView({ block: 'start' }); },
        search(value, direction = 0) {
            const query = String(value || '').slice(0, 256);
            if (query !== activeQuery) {
                clear(); activeQuery = query;
                if (query) {
                    const nodes = [], walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
                    while (walker.nextNode()) nodes.push(walker.currentNode);
                    const term = query.toLocaleLowerCase();
                    for (const node of nodes) {
                        if (matches.length >= 1000) break;
                        const text = node.textContent, lower = text.toLocaleLowerCase();
                        let start = 0, index = lower.indexOf(term);
                        if (index < 0) continue;
                        const fragment = document.createDocumentFragment();
                        while (index >= 0 && matches.length < 1000) {
                            fragment.append(document.createTextNode(text.slice(start, index)));
                            const mark = document.createElement('mark');
                            mark.dataset.readerMatch = 'true';
                            mark.textContent = text.slice(index, index + query.length);
                            fragment.append(mark); matches.push(mark);
                            start = index + query.length;
                            index = lower.indexOf(term, start);
                        }
                        fragment.append(document.createTextNode(text.slice(start)));
                        node.replaceWith(fragment);
                    }
                }
            }
            if (matches.length) {
                if (selected >= 0) matches[selected].classList.remove('reader-current');
                selected = selected < 0 ? 0 : (selected + direction + matches.length) % matches.length;
                matches[selected].classList.add('reader-current');
                matches[selected].scrollIntoView({ block: 'center' });
            }
            const result = { count: matches.length, index: selected + 1 };
            notify({ type: 'search', ...result });
            return result;
        }
    };
    window.addEventListener('scroll', () => {
        clearTimeout(pending);
        pending = setTimeout(() => notify({ type: 'position', value: position() }), 350);
    }, { passive: true });
})();
