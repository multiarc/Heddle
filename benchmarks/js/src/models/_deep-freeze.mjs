// Shared deep-freeze helper for the eight workload model modules: every model is a single
// frozen object, deep-frozen at module load, never rebuilt per benchmark op.

/** Recursively freezes an object graph in place and returns it. */
export function deepFreeze(value) {
  if (value === null || typeof value !== "object") return value;
  for (const key of Object.getOwnPropertyNames(value)) {
    deepFreeze(value[key]);
  }
  return Object.freeze(value);
}
