window.blogDraftClipboard = {
    copyText: async function (text) {
        await navigator.clipboard.writeText(text ?? "");
    }
};
