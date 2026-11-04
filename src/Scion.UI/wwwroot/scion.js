// The only script of the UI: it measures whether a clamped text is cut, which CSS alone cannot tell, it takes
// over Ctrl+S (Cmd+S on macOS) while a file is being edited, it keeps Enter out of a one-line textarea, it
// keeps the arrow keys and Space of a listbox from scrolling the page, and it scrolls a growing text to its end. A page cannot
// do the last four without a script.
window.scion = (() => {
    const observers = new WeakMap();
    let saveShortcutListener = null;

    const offSaveShortcut = () => {
        if (saveShortcutListener) {
            document.removeEventListener("keydown", saveShortcutListener);
            saveShortcutListener = null;
        }
    };

    return {
        observeClamp(element, reference) {
            const report = () => {
                // An expanded text is never cut, so only a clamped one says anything.
                if (element.classList.contains("clamped")) {
                    reference.invokeMethodAsync("SetOverflowing", element.scrollHeight > element.clientHeight + 1);
                }
            };

            const observer = new ResizeObserver(report);
            observer.observe(element);
            observers.set(element, observer);
        },

        unobserveClamp(element) {
            observers.get(element)?.disconnect();
            observers.delete(element);
        },

        // A single-line text in a textarea: Enter does nothing (not even a space). A composition that Enter
        // commits (an input method) is left alone.
        ignoreEnter(element) {
            element.addEventListener("keydown", (event) => {
                if (event.key === "Enter" && !event.isComposing) {
                    event.preventDefault();
                }
            });
        },

        // A listbox moved with the arrow keys and chosen with Space: the page behind it must not scroll meanwhile.
        keepListboxKeys(element) {
            element.addEventListener("keydown", (event) => {
                if (event.key === "ArrowUp" || event.key === "ArrowDown" || event.key === " ") {
                    event.preventDefault();
                }
            });
        },

        scrollToEnd(element) {
            element.scrollTop = element.scrollHeight;
        },

        onSaveShortcut(reference) {
            offSaveShortcut();

            saveShortcutListener = (event) => {
                const isSave = (event.ctrlKey || event.metaKey) && !event.altKey && !event.shiftKey
                    && event.key.toLowerCase() === "s";

                if (isSave) {
                    event.preventDefault();
                    reference.invokeMethodAsync("SaveShortcut");
                }
            };

            document.addEventListener("keydown", saveShortcutListener);
        },

        offSaveShortcut,
    };
})();
