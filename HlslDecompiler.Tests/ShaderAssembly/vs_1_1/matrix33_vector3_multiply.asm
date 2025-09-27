vs_1_1
def c3, 3, 1, 0, 0
dcl_position v0
mul r0.xyz, v0.yyy, c1.xyz
mad r1.xyz, c0.xyz, v0.xxx, r0.xyz
mad oPos.xyz, c2.xyz, v0.zzz, r1.xyz
mul r1.xyz, v0.xxx, c1.xyz
mad r1.xyz, c0.xyz, v0.yyy, r1.xyz
mad oT1.xyz, c2.xyz, v0.zzz, r1.xyz
max r1.xyz, -v0.yxz, v0.yxz
mul r2.xyz, r1.yyy, c1.xyz
mad r1.xyw, c0.xyz, r1.xxx, r2.xyz
mad oT2.xyz, c2.xyz, r1.zzz, r1.xyw
add r0.w, v0.x, v0.x
mad r0.xyz, c0.xyz, r0.www, r0.xyz
mul r0.w, v0.z, c3.x
mad oT3.xyz, c2.xyz, r0.www, r0.xyz
mov oPos.w, c3.y
