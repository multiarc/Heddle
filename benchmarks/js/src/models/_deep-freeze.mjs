// Shared deep-freeze helper for the eight workload model modules (Phase 4 WI2; spec:
// docs/spec/cross-stack-benchmarks/phase-4-js/templates-and-models.md §Models — every model is
// a single frozen object, deep-frozen at module load, never rebuilt per benchmark op).

/** Recursively freezes an object graph in place and returns it. */
export function deepFreeze(value) {
  if (value === null || typeof value !== "object") return value;
  for (const key of Object.getOwnPropertyNames(value)) {
    deepFreeze(value[key]);
  }
  return Object.freeze(value);
}
