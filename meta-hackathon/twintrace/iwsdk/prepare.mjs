import { copyFile, mkdir, readFile, writeFile, stat } from "node:fs/promises";
import { dirname, resolve, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const sourceRoot = dirname(here);
const scaffold = process.argv[2] && resolve(process.argv[2]);
const target = scaffold ? join(scaffold, "src", "twintrace-review") : here;
const dataDir = join(target, "casefiles");

if (scaffold) {
  const pkg = JSON.parse(await readFile(join(scaffold, "package.json"), "utf8"));
  if (!pkg.dependencies?.["@iwsdk/core"] && !pkg.devDependencies?.["@iwsdk/core"]) {
    throw new Error("Select a project created by the official IWSDK scaffold.");
  }
  const occupied = await Promise.all([target, join(scaffold, "twintrace.html")].map(async path => {
    try { await stat(path); return true; } catch (error) {
      if (error.code === "ENOENT") return false;
      throw error;
    }
  }));
  if (occupied.some(Boolean)) throw new Error("Twintrace output already exists. Choose a fresh scaffold.");
}
await mkdir(dataDir, { recursive: true });
await copyFile(join(sourceRoot, "src", "model.js"), join(dataDir, "model.js"));
await copyFile(join(sourceRoot, "data", "cases.json"), join(dataDir, "cases.json"));
if (scaffold) {
  await copyFile(join(here, "scene.js"), join(target, "scene.js"));
  const html = await readFile(join(here, "index.html"), "utf8");
  await writeFile(join(scaffold, "twintrace.html"), html.replace('./scene.js', './src/twintrace-review/scene.js'), { flag: "wx" });
  console.log("Open the scaffold dev server URL with /twintrace.html appended.");
} else {
  console.log("Prepared Twintrace model and case fixtures.");
}
