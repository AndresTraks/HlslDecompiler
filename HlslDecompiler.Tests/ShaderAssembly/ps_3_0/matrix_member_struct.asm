ps_3_0
def c9, 1, 0, 0, 0
dcl_texcoord v0
mul r0.xyz, c5.xyz, v0.yyy
mad r0.xyz, v0.xxx, c4.xyz, r0.xyz
mad r0.xyz, v0.zzz, c6.xyz, r0.xyz
mad r0.xyz, v0.www, c7.xyz, r0.xyz
dp4 r1.x, v0, c0
dp4 r1.y, v0, c1
dp4 r1.z, v0, c2
add r0.xyz, r0.xyz, r1.xyz
add oC0.xyz, r0.xyz, c8.xyz
dp4 r0.x, v0, c3
add r0.x, r0.x, c8.w
add oC0.w, r0.x, c9.x
