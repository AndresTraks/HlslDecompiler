ps_2_0
def c3, 2, -1, 32, 1
dcl t0.xy
dcl t1.xyz
dcl_2d s0
texld r0, t0.xy, s0
mad r0.xyz, r0.xyz, c3.xxx, c3.yyy
nrm r1.xyz, r0.xyz
dp3 r1.w, -c0.xyz, r1.xyz
add r1.w, r1.w, r1.w
mad r0.xyz, r1.xyz, -r1.www, -c0.xyz
add r2.xyz, -t1.xyz, c1.xyz
nrm r3.xyz, r2.xyz
dp3_sat r1.w, r0.xyz, r3.xyz
dp3_sat r0.x, r1.xyz, r3.xyz
add r0.x, -r0.x, c3.w
pow r0.y, r1.w, c3.z
add r0.yzw, r0.yyy, c2.zyx
mul r1.x, r0.x, r0.x
mul r1.x, r1.x, r1.x
mul r0.x, r0.x, r1.x
mad r0.xyz, r0.xxx, c2.www, r0.wzy
mov r0.w, c3.w
mov oC0, r0
