const assert = require("node:assert/strict");
const fs = require("node:fs");
const vm = require("node:vm");
const test = require("node:test");

const app = fs.readFileSync("src/Sciuscia.Ui/wwwroot/app.js", "utf8");
const start = app.indexOf("  function dataCalendarioItaliana(");
const end = app.indexOf("  async function apriAnteprimaArtigiani(", start);
assert(start >= 0 && end > start);
const context = vm.createContext({});
vm.runInContext(app.slice(start, end), context);

for (const [instant, expected] of [
  ["2026-01-21T22:59:59Z", "2026-01-21"],
  ["2026-01-21T23:00:00Z", "2026-01-22"],
  ["2026-07-21T21:59:59Z", "2026-07-21"],
  ["2026-07-21T22:00:00Z", "2026-07-22"],
  ["2026-03-29T00:59:59Z", "2026-03-29"],
  ["2026-03-29T01:00:00Z", "2026-03-29"],
  ["2026-10-25T00:59:59Z", "2026-10-25"],
  ["2026-10-25T01:00:00Z", "2026-10-25"],
]) {
  test(`Calendario italiano: ${instant}`, () => {
    assert.equal(vm.runInContext(`dataCalendarioItaliana(new Date(${JSON.stringify(instant)}))`, context), expected);
  });
}

test("Oggi usa il calendario italiano anche con il computer in un altro fuso", () => {
  vm.runInContext(`Date = class extends Date {
    constructor(...args) { super(...(args.length ? args : ["2026-10-21T22:30:00Z"])); }
  }`, context);
  assert.equal(vm.runInContext("dataCalendarioItaliana()", context), "2026-10-22");
});
