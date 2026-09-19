//! Heddle cross-stack benchmark harness (Rust): Askama 0.16.0 and Tera 2.0.0
//! measured against the Heddle golden corpus under parity contract v2.
//!
//! The crate is an unpublished harness (`publish = false`); every `pub` item is internal
//! to its binaries.

pub mod corpus;
pub mod engines;
pub mod gates;
pub mod models;
pub mod normalize;
pub mod verifier;
