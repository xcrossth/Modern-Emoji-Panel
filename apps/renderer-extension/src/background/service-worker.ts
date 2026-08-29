chrome.runtime.onInstalled.addListener(({ reason }) => {
  if (reason === chrome.runtime.OnInstalledReason.INSTALL ||
      reason === chrome.runtime.OnInstalledReason.UPDATE) {
    void chrome.storage.local.set({ rendererSchemaVersion: 1 });
  }
});
