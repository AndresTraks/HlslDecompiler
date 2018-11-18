vs_3_0
def c3, 3, 1, 0, 0
dcl_position v0
dcl_position o0
dcl_position1 o1.xyz
dcl_position2 o2.xyz
dcl_position3 o3.xyz
mul r0.xyz, c1.xyz, v0.yyy
mad r1.xyz, c0.xyz, v0.xxx, r0.xyz
mad o0.xyz, c2.xyz, v0.zzz, r1.xyz
mul r1.xyz, c1.xyz, v0.xxx
mad r1.xyz, c0.xyz, v0.yyy, r1.xyz
mad o1.xyz, c2.xyz, v0.zzz, r1.xyz
mul r1.xyz, c1.xyz, v0.xxx_abs
mad r1.xyz, c0.xyz, v0.yyy_abs, r1.xyz
mad o2.xyz, c2.xyz, v0.zzz_abs, r1.xyz
add r0.w, v0.x, v0.x
mad r0.xyz, c0.xyz, r0.www, r0.xyz
mul r0.w, c3.x, v0.z
mad o3.xyz, c2.xyz, r0.www, r0.xyz
mov o0.w, c3.y
