// fortunes-encoded model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 4; the 12
// pinned rows byte-for-byte from Phase 1 workloads.md workload 7, ids 1–12). Row 11 is the
// TechEmpower XSS payload; row 12 the Japanese string; rows 4 and 8 carry a literal U+2014 em
// dash. Every value satisfies the untrusted-data alphabet.
import { deepFreeze } from "./_deep-freeze.mjs";

export const model = deepFreeze({
  rows: [
    { id: 1, message: "A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1" },
    { id: 2, message: "A computer program does what you tell it to do, not what you want it to do." },
    { id: 3, message: "A computer scientist is someone who fixes things that aren't broken." },
    { id: 4, message: "A list is only as strong as its weakest link. — Donald Knuth" },
    { id: 5, message: "After enough decimal places, nobody gives a damn." },
    { id: 6, message: "Any program that runs right is obsolete." },
    { id: 7, message: "Computers make very fast, very accurate mistakes." },
    { id: 8, message: "Emacs is a nice operating system, but I prefer UNIX. — Tom Christiansen" },
    { id: 9, message: "Feature: A bug with seniority." },
    { id: 10, message: "fortune: No such file or directory" },
    { id: 11, message: '<script>alert("This should not be displayed in a browser alert box.");</script>' },
    { id: 12, message: "フレームワークのベンチマーク" },
  ],
});
