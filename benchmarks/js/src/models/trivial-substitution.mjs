// trivial-substitution model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 4).
// Pinned scalar values transcribed from src/Heddle.Performance/Runners/SubstitutionContent.cs
// (snake_case keys per the Phase 1 dictionary-view convention; price is a .NET int -> JS
// number, rating is the pinned string "4.8").
import { deepFreeze } from "./_deep-freeze.mjs";

export const model = deepFreeze({
  title: "Heddle Handbook",
  sku: "HB-2001",
  price: 4200,
  brand: "Heddle Press",
  category: "Reference",
  availability: "In stock",
  url: "/catalog/handbook",
  image_url: "/img/handbook.png",
  summary: "A concise field guide to the engine.",
  rating: "4.8",
});
