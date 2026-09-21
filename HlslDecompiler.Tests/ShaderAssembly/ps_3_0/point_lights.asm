ps_3_0
def c11, 2, -1, 0.100000001, 0
def c12, 0, -1, -2, -3
dcl_texcoord v0.xyz
dcl_texcoord1 v1.xyz
dcl_texcoord2 v2.xyz
dcl_texcoord3 v3.xy
dcl_color v4
dcl_2d s0
dcl_2d s1
dcl_cube s2
mul r0.xy, c10.xy, v3.xy
texld r1, r0.xy, s0
mul r1, r1, v4
nrm r2.xyz, v1.xyz
dp3 r0.z, v2.xyz, r2.xyz
mad r3.xyz, r2.xyz, -r0.zzz, v2.xyz
nrm r4.xyz, r3.xyz
mul r3.xyz, r2.zxy, r4.yzx
mad r3.xyz, r2.yzx, r4.zxy, -r3.xyz
texld r0, r0.xy, s1
mad r0.xyz, r0.xyz, c11.xxx, c11.yyy
mul r3.xyz, r3.xyz, r0.yyy
mad r0.xyw, r0.xxx, r4.xyz, r3.xyz
mad r0.xyz, r0.zzz, r2.xyz, r0.xyw
nrm r2.xyz, r0.xyz
add r0.xyz, c8.xyz, -v0.xyz
nrm r3.xyz, r0.xyz
mov r0, c11.zzzw
rep i0
add r4, r0.w, c12
mov r2.w, c11.w
cmp r5.xyz, -r4.xxx_abs, c0.xyz, r2.www
cmp r5.xyz, -r4.yyy_abs, c1.xyz, r5.xyz
cmp r5.xyz, -r4.zzz_abs, c2.xyz, r5.xyz
cmp r5.xyz, -r4.www_abs, c3.xyz, r5.xyz
add r5.xyz, r5.xyz, -v0.xyz
dp3 r3.w, r5.xyz, r5.xyz
rsq r5.w, r3.w
mul r6.xyz, r5.www, r5.xyz
dp3_sat r6.x, r2.xyz, r6.xyz
mad r5.xyz, r5.xyz, r5.www, r3.xyz
nrm r7.xyz, r5.xyz
dp3_sat r5.x, r2.xyz, r7.xyz
pow r6.y, r5.x, c9.x
cmp r5.xyz, -r4.xxx_abs, c4.xyz, r2.www
cmp r5.xyz, -r4.yyy_abs, c5.xyz, r5.xyz
cmp r4.xyz, -r4.zzz_abs, c6.xyz, r5.xyz
cmp r4.xyz, -r4.www_abs, c7.xyz, r4.xyz
add r2.w, r6.y, r6.x
mul r4.xyz, r2.www, r4.xyz
add r2.w, r3.w, -c11.y
rcp r2.w, r2.w
mad r0.xyz, r4.xyz, r2.www, r0.xyz
add r0.w, r0.w, -c11.y
endrep
dp3 r0.w, -r3.xyz, r2.xyz
add r0.w, r0.w, r0.w
mad r4.xyz, r2.xyz, -r0.www, -r3.xyz
texld r4, r4.xyz, s2
dp3_sat r0.w, r2.xyz, r3.xyz
add r0.w, -r0.w, -c11.y
mul r0.w, r0.w, r0.w
mul r0.w, r0.w, r0.w
mul r2.xyz, r0.www, r4.xyz
mad oC0.xyz, r1.xyz, r0.xyz, r2.xyz
mov oC0.w, r1.w
