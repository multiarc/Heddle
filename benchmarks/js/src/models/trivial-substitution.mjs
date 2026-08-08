// trivial-substitution model.
// Pinned scalar values transcribed from benchmarks/dotnet/src/Models/SubstitutionContent.cs
// (snake_case keys per the cross-stack dictionary-view convention; price is a .NET int -> JS
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
