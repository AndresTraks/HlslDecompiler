ps_2_0
def c3, 0, 1, 0, -1
dcl t0.xyz
dcl t1.xyz
mov r0.z, c3.y
nrm r1.xyz, t0.xyz
mul r2.xyz, r1.zxy, t1.yzx
mad r2.xyz, r1.yzx, t1.zxy, -r2.xyz
dp3 r1.w, r2.xyz, r2.xyz
rsq r0.x, r1.w
mul r0.y, r0.x, r1.w
mov r2.xyz, c0.xyz
add r2.xyz, r2.xyz, c1.xyz
nrm r3.xyz, r2.xyz
dp3 r1.w, r1.xyz, r3.xyz
dp3 r0.x, r1.xyz, c0.xyz
dp3 r0.w, r1.xyz, c1.xyz
cmp r1.x, -r0.x, c3.x, c3.y
mul r1.y, r0.x, r1.x
cmp r0.x, -r1.w, c3.x, r1.x
pow r2.x, r1.w, c2.x
mul r1.z, r0.x, r2.x
mul r0.x, r1.z, r1.z
mul r1.yz, r0.yz, r1.yz
mov r1.x, c3.y
cmp r1.w, -r0.w, c3.x, c3.y
cmp r0.y, r0.w, c3.z, c3.w
add r1.w, r0.y, r1.w
mad r0.xyz, r0.xxx, r1.www, r1.xyz
mov r0.w, r0.x
mov oC0, r0
