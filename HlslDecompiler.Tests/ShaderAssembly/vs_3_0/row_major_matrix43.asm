vs_3_0
def c16, 1, 0, 0, 0
dcl_position v0
dcl_position o0
mul r0.xyz, c13.xyz, v0.yyy
mad r0.xyz, v0.xxx, c12.xyz, r0.xyz
mad r0.xyz, v0.zzz, c14.xyz, r0.xyz
mad r0.xyz, v0.www, c15.xyz, r0.xyz
mul r1.xyz, c9.xyz, v0.yyy
mad r1.xyz, v0.xxx, c8.xyz, r1.xyz
mad r1.xyz, v0.zzz, c10.xyz, r1.xyz
mad r1.xyz, v0.www, c11.xyz, r1.xyz
add o0.xyz, r0.xyz, r1.xyz
mov o0.w, c16.x
