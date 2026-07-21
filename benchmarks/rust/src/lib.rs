//! Heddle cross-stack benchmark harness — Phase 2 (Rust): Askama 0.16.0 and Tera 2.0.0
//! measured against the Phase 1 golden corpus under parity contract v2.
//!
//! Spec: `docs/spec/cross-stack-benchmarks/phase-2-rust/README.md`. The crate is an
//! unpublished harness (`publish = false`); every `pub` item is internal to its binaries.

pub mod corpus;
pub mod engines;
pub mod gates;
pub mod models;
pub mod normalize;
pub mod verifier;
