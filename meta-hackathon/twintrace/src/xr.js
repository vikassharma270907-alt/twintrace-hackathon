// TwinTrace uses WebXR hand joints and WebGL directly. All scene assets are local.
const BOARD_Z = -0.67;
const SPECIMEN = [-0.15, -0.07, -0.49];
const NEIGHBOR = [0.15, -0.07, -0.49];
const GATE = [0,-0.07,-0.52];
const RADIUS = 0.046;
const identity = () => new Float32Array([1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1]);
function multiply(a,b) {
  const out = new Float32Array(16);
  for (let c=0;c<4;c++) for (let r=0;r<4;r++) {
    for (let k=0;k<4;k++) out[c*4+r]+=a[k*4+r]*b[c*4+k];
  }
  return out;
}
function transform(position, size=[1,1,1], yaw=0) {
  const c=Math.cos(yaw), s=Math.sin(yaw);
  return new Float32Array([c*size[0],0,-s*size[0],0, 0,size[1],0,0, s*size[2],0,c*size[2],0, ...position,1]);
}
function distance(a,b) { return Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]); }
function raySphere(origin, dir, center, r) {
  const delta = center.map((v,i)=>v-origin[i]);
  const along = delta.reduce((n,x,i)=>n+x*dir[i],0);
  return along>0 && distance(delta,dir.map(x=>x*along))<=r ? along : Infinity;
}
function aimAtBoard(origin,dir) {
  if (!dir || Math.abs(dir[2])<1e-6) return null;
  const t=(BOARD_Z-origin[2])/dir[2];
  return t>0 ? [origin[0]+t*dir[0],origin[1]+t*dir[1],BOARD_Z] : null;
}

const VERTEX = `
attribute vec3 point;
attribute vec2 uv;
uniform mat4 mvp;
varying vec2 texCoord;
void main(){ texCoord=uv; gl_Position=mvp*vec4(point,1.0); }`;
const FRAGMENT = `
precision mediump float;
uniform vec4 color;
uniform bool textured;
uniform sampler2D surface;
varying vec2 texCoord;
void main(){ gl_FragColor=textured ? texture2D(surface,texCoord) : color; }`;

function createRenderer(gl) {
  const program=gl.createProgram();
  const shaders=[];
  const buffers=[];
  const textures=[];
  function dispose() {
    for (const t of textures) gl.deleteTexture(t);
    for (const b of buffers) gl.deleteBuffer(b);
    for (const s of shaders) gl.deleteShader(s);
    gl.deleteProgram(program);
  }
  try {
    for (const [type,code] of [[gl.VERTEX_SHADER,VERTEX],[gl.FRAGMENT_SHADER,FRAGMENT]]) {
      const shader=gl.createShader(type);
      shaders.push(shader);
      gl.shaderSource(shader,code);
      gl.compileShader(shader);
      if (!gl.getShaderParameter(shader,gl.COMPILE_STATUS)) throw Error(gl.getShaderInfoLog(shader));
      gl.attachShader(program,shader);
    }
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program,gl.LINK_STATUS)) throw Error(gl.getProgramInfoLog(program));
  } catch(error) { dispose(); throw error; }
  const point=gl.getAttribLocation(program,'point'), uv=gl.getAttribLocation(program,'uv');
  const mvp=gl.getUniformLocation(program,'mvp'), tint=gl.getUniformLocation(program,'color');
  const tex=gl.getUniformLocation(program,'textured');
  function buffer(values, usage=gl.STATIC_DRAW) {
    const b=gl.createBuffer(); buffers.push(b); gl.bindBuffer(gl.ARRAY_BUFFER,b);
    gl.bufferData(gl.ARRAY_BUFFER,new Float32Array(values),usage); return b;
  }
  const quad=buffer([-.5,-.5,0,.5,-.5,0,.5,.5,0, -.5,-.5,0,.5,.5,0,-.5,.5,0]);
  const coords=buffer([0,0,1,0,1,1, 0,0,1,1,0,1]);
  const shape=buffer([],gl.DYNAMIC_DRAW);
  const surface=gl.createTexture(); textures.push(surface);
  gl.bindTexture(gl.TEXTURE_2D,surface);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);
  const swapSheet = canvas => {
    gl.bindTexture(gl.TEXTURE_2D,surface);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);
    gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,canvas);
  };
  const panel = view => {
    gl.uniformMatrix4fv(mvp,false,multiply(view,transform([0,-.042,BOARD_Z],[.78,.48,1])));
    gl.bindBuffer(gl.ARRAY_BUFFER,quad); gl.enableVertexAttribArray(point); gl.vertexAttribPointer(point,3,gl.FLOAT,false,0,0);
    gl.bindBuffer(gl.ARRAY_BUFFER,coords); gl.enableVertexAttribArray(uv); gl.vertexAttribPointer(uv,2,gl.FLOAT,false,0,0);
    gl.uniform1i(tex,1); gl.bindTexture(gl.TEXTURE_2D,surface); gl.drawArrays(gl.TRIANGLES,0,6);
    gl.disableVertexAttribArray(uv); gl.vertexAttrib2f(uv,0,0);
  };
  const draw = (view,model,vertices,mode,color) => {
    gl.uniformMatrix4fv(mvp,false,multiply(view,model)); gl.uniform4fv(tint,color); gl.uniform1i(tex,0);
    gl.bindBuffer(gl.ARRAY_BUFFER,shape); gl.bufferData(gl.ARRAY_BUFFER,vertices,gl.DYNAMIC_DRAW);
    gl.enableVertexAttribArray(point); gl.vertexAttribPointer(point,3,gl.FLOAT,false,0,0);
    gl.drawArrays(mode,0,vertices.length/3);
  };
  return {program,swapSheet,panel,draw,dispose};
}
function specimenShape(features) {
  const ring=Array.from({length:6},(_,i)=>{
    const radius=.6+.40*Math.min(1,Math.max(0,Number(features[i])||0));
    return [radius*Math.cos(i*Math.PI/3),radius*Math.sin(i*Math.PI/3),0];
  });
  const faces=[],edges=[];
  const tip=[0,0,.7],back=[0,0,-.7];
  for(let i=0;i<6;i++){
    const a=ring[i],b=ring[(i+1)%6];
    faces.push(...a,...b,...tip,...b,...a,...back);
    edges.push(...a,...b,...a,...tip,...a,...back);
  }
  return {faces:new Float32Array(faces),edges:new Float32Array(edges)};
}
const handMarker=specimenShape([1,1,1,1,1,1]).faces;

const WIDTH=1200, HEIGHT=740;
const boardToPx = p => [(p[0]/.78+.5)*WIDTH,(.5-(p[1]+.042)/.48)*HEIGHT];
const cx = (x,w) => Math.round((x-w/2)*WIDTH);
function controls(s) {
  const choices=s.stage==='inspect'?[['inspect','Inspect specimen']]:
    s.stage==='repair'?[['repair',s.caseKind==='control'?'Keep split':'Repair group split']]:
    s.stage==='vote'?[['hold','Hold release'],['release','Release model']]:
    [['next','New case'],['reset','Replay']];
  return [...choices.map(([id,label],i)=>({id,label,x:cx(choices.length===1?.5:.30+.40*i,.35),y:548,w:420,h:102})),
    {id:'recenter',label:'Center',x:832,y:18,w:140,h:54},{id:'exit',label:'Exit VR',x:984,y:18,w:164,h:54}];
}
function shorten(text,max){text=String(text??'');return text.length>max ? text.slice(0,max-1)+'…' : text;}
function boardSheet(canvas,s,highlight,message) {
  const c=canvas.getContext('2d');
  c.fillStyle='#121d29';c.fillRect(0,0,WIDTH,HEIGHT);
  c.strokeStyle='#436073';c.lineWidth=5;c.strokeRect(3,3,WIDTH-6,HEIGHT-6);
  const label=(text,x,y,size,color,weight='500')=>{
    c.font=weight+' '+size+'px system-ui, sans-serif';c.fillStyle=color;c.fillText(text,x,y);
  };
  label('TWINTRACE   /   CASE '+s.seed,40,54,28,'#89e0dc','700');
  const stage=s.stage;
  const control=s.caseKind==='control';
  const titles={inspect:'Can you trust this score?',repair:control?'Are these separate objects?':'Two readings, one object',vote:'Would you release this model?',done:'Your model-review record'};
  const help={inspect:'Pinch and lift the teal specimen. Turn your hand to inspect it.',repair:control?'Compare the group IDs. Keep this split if the groups are separate.':'Place the teal specimen in the gate, or select Repair group split.',vote:'Commit your decision. The sealed audit opens afterward.',done:'The audit used entirely new objects. Compare both counts.'};
  label(titles[stage]||'Inspect the case',40,135,49,'#eef5f6','700');
  label(help[stage]||'',40,184,28,'#c7d3d6');
  label('EVALUATION',46,252,23,'#83b5bd','700');
  label(s.evalCorrect+' / '+s.evalTotal,46,315,53,'#ffffff','700');
  label('GROUP OVERLAP',434,252,23,'#83b5bd','700');
  label(String(s.overlap),434,315,53,'#ffffff','700');
  label('UNSEEN AUDIT',830,252,23,'#83b5bd','700');
  label(stage==='done'?s.auditCorrect+' / '+s.auditTotal:'Sealed',830,315,stage==='done'?53:42,'#ffffff','700');
  label('TEST',45,415,23,'#77e7d7','700');
  label(shorten(s.sample?.group,18),45,453,23,'#c9d8dc');
  label(stage==='vote'||stage==='done' ? (control?'TRAIN MATCH':'REMOVED TWIN'):'TRAIN',890,415,23,'#ffc782','700');
  label(stage==='inspect'?'Inspect to trace':shorten(s.neighbor?.group,18),890,453,23,'#c9d8dc');
  if(stage==='repair'&&!control)label('GROUP GATE',509,484,24,'#c8f5e4','700');
  if(stage==='done')label('Your call: '+s.verdict+'    •    Evidence supports: '+(s.expectedVerdict||s.verdict),40,514,26,'#e7e7d0','650');
  if(stage==='vote')label('Training rows: '+s.trainCount+'    •    Prior evaluation: '+s.leakyEvalCorrect+' / '+s.evalTotal,40,514,25,'#e7e7d0');
  for (const b of controls(s)){
    c.fillStyle=highlight===b.id?'#a3f1df':b.id==='exit'||b.id==='recenter'?'#263947':'#294c56';
    c.fillRect(b.x,b.y,b.w,b.h);
    c.strokeStyle=highlight===b.id?'#ffffff':'#69949a';c.lineWidth=2;c.strokeRect(b.x,b.y,b.w,b.h);
    c.textAlign='center';label(b.label,b.x+b.w/2,b.y+b.h*.62,b.h>70?32:23,highlight===b.id?'#123738':'#f1faf9','650');c.textAlign='left';
  }
  label(message||'Use either hand. Point and pinch a button, or touch it and pinch.',40,702,25,'#afc6cd');
}
function inside(p,b){return p && p[0]>=b.x && p[0]<=b.x+b.w && p[1]>=b.y && p[1]<=b.y+b.h;}

export async function startXR(canvas,api) {
  if (!globalThis.isSecureContext) throw Error('Open this page over HTTPS or localhost to enter VR.');
  if (!navigator.xr) throw Error('WebXR is unavailable. Open this URL in a WebXR browser on Quest with hand tracking enabled.');
  // Request immediately under the initiating user gesture.
  const session=await navigator.xr.requestSession('immersive-vr',{requiredFeatures:['hand-tracking'],optionalFeatures:['local-floor']});
  let renderer;
  let ended=false;
  session.addEventListener('end',()=>{ended=true;renderer?.dispose();},{once:true});
  try {
    const gl=canvas.getContext('webgl',{alpha:false,antialias:true,xrCompatible:true})||
      canvas.getContext('webgl2',{alpha:false,antialias:true,xrCompatible:true});
    if (!gl) throw Error('An available WebGL canvas is required for the headset view.');
    if(gl.makeXRCompatible) await gl.makeXRCompatible();
    const layer=new XRWebGLLayer(session,gl);
    session.updateRenderState({baseLayer:layer,depthNear:.025,depthFar:8});
    const space=await session.requestReferenceSpace('local');
    renderer=createRenderer(gl);
    const image=document.createElement('canvas');image.width=WIDTH;image.height=HEIGHT;
    const hands=new Map();
    const sheetCache={key:''};
    let anchor=null, held=null, trackingMessage='', shapeKey='', lastSource=null;
    let specimen=null,neighbor=null,objectPosition=[...SPECIMEN],rotation=0;
    const originMatrix=()=>transform(anchor.position,[1,1,1],anchor.yaw);
    const toLocal=(v,vector=false)=>{
      const [x,y,z]=v.map((q,i)=>q-(vector?0:anchor.position[i]));
      const c=Math.cos(anchor.yaw),s=Math.sin(anchor.yaw);
      return [c*x-s*z,y,s*x+c*z];
    };
    function doAction(id) {
      if (id==='exit') { session.end().catch(()=>{});return; }
      if (id==='recenter') {anchor=null;held=null;objectPosition=[...SPECIMEN];return;}
      try {api.act(id);trackingMessage='';}catch(error){trackingMessage=shorten(error.message,74);}
    }
    function onFrame(time,frame) {
      if(ended)return;
      session.requestAnimationFrame(onFrame);
      const pose=frame.getViewerPose(space);
      if(!pose || !session.renderState.baseLayer)return;
      if(!anchor){
        const m=pose.transform.matrix;
        anchor={position:[m[12],m[13],m[14]],yaw:Math.atan2(m[8],m[10])};
      }
      let state=api.getState();
      let shapeId=String(state.seed)+'/'+state.stage+'/'+String(state.sample?.id)+'/'+String(state.neighbor?.id);
      if(shapeKey!==shapeId){
        specimen=specimenShape(state.sample?.features||[]);
        neighbor=specimenShape(state.neighbor?.features||[]);
        shapeKey=shapeId;
      }
      const inputs=[...session.inputSources].filter(x=>x.hand);
      const visuals=[];
      let active=null;
      for(const source of inputs){
        const get=(joint)=>{
          const jointSpace=source.hand.get(joint);
          const p=jointSpace && frame.getJointPose(jointSpace,space)?.transform.position;
          return p?toLocal([p.x,p.y,p.z]):null;
        };
        const tip=get('index-finger-tip'),thumb=get('thumb-tip'),palm=get('wrist');
        if(!tip || !thumb){
          hands.delete(source);
          if(held?.source===source){held=null;objectPosition=[...SPECIMEN];}
          continue;
        }
        const mid=tip.map((q,i)=>(q+thumb[i])/2);
        const ray=frame.getPose(source.targetRaySpace,space)?.transform.matrix;
        const origin=ray?toLocal([ray[12],ray[13],ray[14]]):tip;
        const direction=ray?toLocal([-ray[8],-ray[9],-ray[10]],true):null;
        const boardPoint=aimAtBoard(origin,direction);
        const nearBoard=Math.abs(tip[2]-BOARD_Z)<.038;
        const targetPoint=nearBoard ? tip : boardPoint;
        const b=targetPoint && controls(state).find(b=>inside(boardToPx(targetPoint),b));
        const nearBall=distance(mid,objectPosition)<.095;
        const farBall=direction && Number.isFinite(raySphere(origin,direction,objectPosition,.061));
        const onBall=nearBall || (farBall && !nearBoard);
        const previous=hands.get(source)||{pinched:false};
        const pinched=distance(tip,thumb)<(previous.pinched?.033:.021);
        const hit=onBall?'ball':b?.id;
        if(hit && (source===lastSource||!active))active=hit;
        if(pinched && !previous.pinched){
          lastSource=source;
          if(hit==='ball' && !held){
            const angle=palm?Math.atan2(mid[1]-palm[1],mid[0]-palm[0]):0;
            held={source,start:mid,position:[...objectPosition],angle,turn:rotation};
            if(state.stage==='inspect')doAction('inspect');
          }else if(hit && hit!=='ball' && !held)doAction(hit);
        }
        if(!anchor)return; // A recenter request establishes a new reference on the next frame.
        if(held?.source===source){
          if(pinched){
            objectPosition=mid.map((q,i)=>held.position[i]+q-held.start[i]);
            const angle=palm?Math.atan2(mid[1]-palm[1],mid[0]-palm[0]):held.angle;
            rotation=held.turn+angle-held.angle+(mid[0]-held.start[0])*5;
          }else {
            if(state.stage==='repair' && state.caseKind!=='control' && distance(objectPosition,GATE)<.067)doAction('repair');
            held=null;objectPosition=[...SPECIMEN];
          }
        }
        hands.set(source,{pinched});
        visuals.push({tip,thumb,palm,mid,point:hit==='ball'?objectPosition:targetPoint,hit});
      }
      for(const source of hands.keys())if(!inputs.includes(source)){
        hands.delete(source);if(held?.source===source){held=null;objectPosition=[...SPECIMEN];}
      }
      state=api.getState();
      const msg=visuals.length?trackingMessage:'Raise a hand in view. Hand tracking must be enabled on your headset.';
      const stamp=[state.seed,state.stage,state.evalCorrect,state.overlap,state.verdict,state.sample?.id,state.neighbor?.id,active,msg].join('|');
      if(stamp!==sheetCache.key){boardSheet(image,state,active,msg);renderer.swapSheet(image);sheetCache.key=stamp;}
      gl.bindFramebuffer(gl.FRAMEBUFFER,layer.framebuffer);gl.enable(gl.DEPTH_TEST);gl.disable(gl.CULL_FACE);
      gl.useProgram(renderer.program);gl.activeTexture(gl.TEXTURE0);
      gl.clearColor(.022,.038,.060,1);gl.clear(gl.COLOR_BUFFER_BIT|gl.DEPTH_BUFFER_BIT);
      for(const eye of pose.views){
        const vp=layer.getViewport(eye);gl.viewport(vp.x,vp.y,vp.width,vp.height);
        const view=multiply(multiply(eye.projectionMatrix,eye.transform.inverse.matrix),originMatrix());
        renderer.panel(view);
        const drawGem=(obj,where,yaw,fill,line)=>{
          const size=[RADIUS,RADIUS,RADIUS], model=transform(where,size,yaw);
          renderer.draw(view,model,obj.faces,gl.TRIANGLES,fill);
          renderer.draw(view,transform(where,size.map(v=>v*1.012),yaw),obj.edges,gl.LINES,line);
        };
        drawGem(specimen,objectPosition,rotation,[.06,.36,.35,1],[.45,1,.88,1]);
        if(state.stage!=='inspect'){
          const removed=state.caseKind!=='control' && (state.stage==='vote'||state.stage==='done');
          drawGem(neighbor,NEIGHBOR,0,removed?[.16,.19,.22,1]:[.5,.26,.12,1],removed?[.42,.48,.5,1]:[1,.78,.48,1]);
          if(!removed)renderer.draw(view,identity(),new Float32Array([...objectPosition,...NEIGHBOR]),gl.LINES,[1,.85,.53,1]);
        }
        if(state.stage==='repair' && state.caseKind!=='control'){
          const ring=[];
          for(let i=0;i<=24;i++)ring.push(.053*Math.cos(i*Math.PI/12),.053*Math.sin(i*Math.PI/12),0);
          renderer.draw(view,transform(GATE),new Float32Array(ring),gl.LINE_STRIP,[.43,1,.76,1]);
        }
        for(const v of visuals){
          if(v.palm){
            renderer.draw(view,identity(),new Float32Array([...v.palm,...v.tip,...v.palm,...v.thumb]),gl.LINES,[.6,.8,.88,1]);
          }
          for(const p of [v.tip,v.thumb])renderer.draw(view,transform(p,[.006,.006,.006]),handMarker,gl.TRIANGLES,[.71,.97,.91,1]);
          if(v.point && v.hit && !held){
            renderer.draw(view,identity(),new Float32Array([...v.mid,...v.point]),gl.LINES,[.44,.74,.76,1]);
            renderer.draw(view,transform([v.point[0],v.point[1],v.point[2]+.004],[.005,.005,.005]),handMarker,gl.TRIANGLES,[1,.92,.70,1]);
          }
        }
      }
    }
    session.requestAnimationFrame(onFrame);
    return session;
  } catch(error) {
    if(!ended)await session.end().catch(()=>{});
    throw error;
  }
}

