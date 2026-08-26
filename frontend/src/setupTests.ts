import '@testing-library/jest-dom/vitest';

// Tests never talk to a real backend. Node's built-in fetch is available in this
// jsdom environment by default, so apiRequest() calls were racing a real network
// attempt (DNS/connect/refuse) against RTL's 1000ms waitFor timeout instead of
// failing instantly - under load this consistently loses the race and flakes.
// Every feature component already has an offline/unreachable-backend fallback
// path (.catch(() => {})); this just makes that path deterministic in tests.
globalThis.fetch = () => Promise.reject(new Error('fetch is disabled in tests — mock apiRequest if a test needs a response'));
