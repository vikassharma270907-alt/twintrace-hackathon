import { World, SessionMode, RayInteractable, PokeInteractable, OneHandGrabbable } from "@iwsdk/core";
import {
  AmbientLight, BoxGeometry, CanvasTexture, Color, CylinderGeometry, DirectionalLight,
  Group, Mesh, MeshBasicMaterial, MeshStandardMaterial, Quaternion, SRGBColorSpace, Vector3,
} from "three";
import { createSession } from "./casefiles/model.js";
import fixtures from "./casefiles/cases.json";

const enter = document.querySelector("#enter");
const status = document.querySelector("#status");

function makeLabel(width, height, color = "#132d36") {
  const canvas = document.createElement("canvas");
  canvas.width = 1280;
  canvas.height = Math.round(1280 * height / width);
  const ctx = canvas.getContext("2d");
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  const front = new MeshBasicMaterial({ map: texture });
  const edge = new MeshBasicMaterial({ color: "#25434b" });
  const mesh = new Mesh(new BoxGeometry(width, height, 0.009), [edge, edge, edge, edge, front, edge]);
  function print(lines, fill = color) {
    ctx.fillStyle = fill;
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    const heights = lines.map(line => line.size * 1.3);
    const factor = Math.min(1, canvas.height * 0.90 / heights.reduce((a, b) => a + b, 0));
    let y = (canvas.height - heights.reduce((a, b) => a + b, 0) * factor) / 2;
    lines.forEach((line, i) => {
      const heightPx = heights[i] * factor;
      ctx.fillStyle = line.ink || "#f5f4eb";
      ctx.font = (line.bold ? "700 " : "500 ") + line.size * factor + "px system-ui, sans-serif";
      ctx.fillText(line.text, canvas.width / 2, y + heightPx / 2, canvas.width * 0.94);
      y += heightPx;
    });
    texture.needsUpdate = true;
  }
  return { mesh, print };
}

function buildDesk(world) {
  let storage;
  try { storage = window.localStorage; } catch { /* A blocked storage policy permits an in-memory run. */ }
  const session = createSession(fixtures, storage);
  const root = new Group();
  const rootEntity = world.createTransformEntity(root);
  world.scene.background = new Color("#0b1920");
  world.scene.add(new AmbientLight(0xffffff, 2));
  const key = new DirectionalLight(0xffefce, 2.1);
  key.position.set(-1, 3, 1);
  world.scene.add(key);

  function add(object, x, y, z) {
    object.position.set(x, y, z);
    return world.createTransformEntity(object, { parent: rootEntity });
  }
  function actionable(mesh, entity, handler, activateOnPress = false) {
    entity.addComponent(RayInteractable).addComponent(PokeInteractable);
    let last = -Infinity;
    function activate() {
      if (!mesh.visible || performance.now() - last < 350) return;
      last = performance.now();
      handler();
    }
    // The SDK documents property handlers for both hand and browser pointers.
    mesh.onClick = activate;
    if (activateOnPress) mesh.onPointerDown = activate;
  }
  const header = makeLabel(0.57, 0.146);
  add(header.mesh, 0, 0.103, -0.70);
  const metric = makeLabel(0.53, 0.056);
  add(metric.mesh, 0, -0.179, -0.63);

  function makeSpecimen(x, color, movable) {
    const body = new Mesh(new BoxGeometry(0.133, 0.067, 0.024),
      new MeshStandardMaterial({ color: "#334c53", roughness: 0.68 }));
    const home = new Vector3(x, -0.065, -0.53);
    const entity = add(body, home.x, home.y, home.z);
    const bars = [];
    const barGeometry = new BoxGeometry(0.012, 1, 0.012);
    const material = new MeshStandardMaterial({ color, roughness: 0.45 });
    for (let index = 0; index < 6; index++) {
      const bar = new Mesh(barGeometry, material);
      bar.position.set((index - 2.5) * 0.02, 0, 0.02);
      body.add(bar);
      bars.push(bar);
    }
    const label = makeLabel(0.19, 0.043);
    add(label.mesh, x, -0.123, -0.55);
    function returnHome() {
      body.position.copy(home);
      body.quaternion.identity();
    }
    if (movable) {
      actionable(body, entity, () => {
        if (session.getState().stage === "inspect") apply("inspect");
      }, true);
      entity.addComponent(OneHandGrabbable, { translate: true, rotate: true });
      body.onPointerUp = () => setTimeout(returnHome, 40);
      body.onPointerCancel = returnHome;
    }
    return { body, bars, label, home, returnHome };
  }
  const sample = makeSpecimen(-0.136, "#efc978", true);
  const twin = makeSpecimen(0.136, "#91d6c9", false);
  const connector = new Mesh(new CylinderGeometry(0.0025, 0.0025, 0.13, 8),
    new MeshBasicMaterial({ color: "#f5df97" }));
  add(connector, 0, -0.065, -0.53);
  connector.rotation.z = Math.PI / 2;
  const buttons = [-0.112, 0.112].map(x => {
    const label = makeLabel(0.208, 0.055, "#315860");
    const entity = add(label.mesh, x, -0.244, -0.555);
    const control = { label, action: null };
    actionable(label.mesh, entity, () => { if (control.action) apply(control.action); });
    return control;
  });
  function utility(label, x, callback) {
    const face = makeLabel(0.135, 0.033);
    face.print([{ text: label, size: 138 }]);
    const entity = add(face.mesh, x, -0.299, -0.56);
    actionable(face.mesh, entity, callback);
  }
  let requestCenter = false;
  utility("Recenter", -0.148, () => { requestCenter = true; });
  utility("Restart", 0, () => { sample.returnHome(); apply("reset"); });
  utility("Exit VR", 0.148, () => {
    if (world.renderer.xr.isPresenting) world.exitXR();
    else status.textContent = "Desktop preview. Enter VR on your headset to use tracked hands.";
  });
  function showSpecimen(view, row, heading) {
    view.bars.forEach((bar, index) => {
      const length = 0.015 + Math.max(0, Math.min(1, row.features[index])) * 0.047;
      bar.scale.y = length;
      bar.position.y = -0.029 + length / 2;
    });
    view.label.print([{ text: heading, size: 88, bold: true },
      { text: String(row.groupId), size: 97, ink: "#ecd592" }]);
  }
  function draw() {
    const s = session.getState();
    const control = s.caseKind === "control";
    const discovered = s.stage !== "inspect";
    const repaired = s.stage === "vote" || s.stage === "done";
    const expected = s.expectedVerdict || (control ? "release" : "hold");
    const phrases = {
      inspect: ["Can this model ship?", "Lift the test specimen. Its six bars encode one observation."],
      repair: control
        ? ["Different source groups", "The closest training example comes from another source. Keep the split."]
        : ["One source in both trays", "Quarantine its training copies and replace them with fresh groups."],
      vote: ["Would you release this model?", "Choose before opening the untouched audit tray."],
      done: [s.verdict === expected ? "Review complete" : "Review the evidence",
        control ? "Distinct groups and independent audit results support release."
          : "The independent audit exposes the original evaluation shortcut."],
    };
    const [title, hint] = phrases[s.stage];
    header.print([
      {text: "TWINTRACE   /   CASE " + (s.caseIndex + 1), size: 40, ink: "#a7d7ce"},
      {text: title, size: 76, bold: true}, {text: hint, size: 39},
    ]);
    showSpecimen(sample, s.sample, "Evaluation");
    showSpecimen(twin, s.neighbor, repaired && !control ? "Quarantined twin" : "Nearest training sample");
    twin.body.visible = twin.label.mesh.visible = discovered;
    connector.visible = s.stage === "repair" && !control;
    metric.print(s.stage === "done" ? [
      {text: "Your call: " + s.verdict.toUpperCase() + "   /   Reference: " + expected.toUpperCase(), size: 53, bold: true},
      {text: "Evaluation " + s.evalCorrect + "/" + s.evalTotal + "     Untouched audit " + s.auditCorrect + "/" + s.auditTotal, size: 57},
    ] : [
      {text: "Evaluation " + s.evalCorrect + "/" + s.evalTotal + "     Training " + s.trainCount, size: 60, bold: true},
      {text: discovered ? "Shared source groups: " + s.overlap : "Does this score hold on unseen objects?", size: 48},
    ]);
    const actions = { inspect: [["Inspect sample", "inspect"]], repair: [control ? ["Keep split", "repair"] : ["Repair groups", "repair"]],
      vote: [["Hold model", "hold"], ["Release model", "release"]], done: [["Next case", "next"]] }[s.stage];
    buttons.forEach((button, index) => {
      const choice = actions[index];
      button.label.mesh.visible = Boolean(choice);
      button.action = choice?.[1];
      if (choice) {
        button.label.mesh.position.x = actions.length === 1 ? 0 : (index === 0 ? -0.112 : 0.112);
        button.label.print([{ text: choice[0], size: 145, bold: true }]);
      }
    });
  }
  function apply(action) {
    session.act(action);
    if (action === "reset" || action === "next") sample.returnHome();
    draw();
  }
  function recenter(position, orientation) {
    const forward = new Vector3(0, 0, -1).applyQuaternion(orientation);
    root.position.copy(position);
    root.rotation.set(0, Math.atan2(-forward.x, -forward.z), 0);
  }
  function centerBrowser() {
    recenter(world.camera.getWorldPosition(new Vector3()), world.camera.getWorldQuaternion(new Quaternion()));
  }
  centerBrowser();
  world.renderer.xr.addEventListener("sessionstart", () => { requestCenter = true; });
  world.renderer.xr.addEventListener("sessionend", () => {
    sample.returnHome();
    requestCenter = false;
    requestAnimationFrame(centerBrowser);
  });
  world.onXRFrame(frame => {
    if (!requestCenter) return;
    const reference = world.renderer.xr.getReferenceSpace();
    const pose = reference && frame.getViewerPose(reference);
    if (pose) {
      const p = pose.transform.position;
      const q = pose.transform.orientation;
      recenter(new Vector3(p.x, p.y, p.z), new Quaternion(q.x, q.y, q.z, q.w));
      requestCenter = false;
    }
  });
  draw();
}

async function start() {
  const world = await World.create(document.querySelector("#scene"), {
    xr: { sessionMode: SessionMode.ImmersiveVR, features: { handTracking: true } },
    input: { canvasPointerEvents: true }, features: { grabbing: true },
  });
  buildDesk(world);
  status.textContent = "Point and pinch in VR. Click a specimen or action in desktop preview.";
  enter.disabled = false;
  enter.onclick = async () => {
    try {
      await world.launchXR();
    } catch (error) {
      status.textContent = "VR could not start. Use HTTPS in Quest Browser, or try the desktop preview. " + error.message;
    }
  };
}
start().catch(error => { status.textContent = "Review desk could not load. " + error.message; });
