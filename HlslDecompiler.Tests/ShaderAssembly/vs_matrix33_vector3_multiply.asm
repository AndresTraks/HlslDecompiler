vs_3_0
def c3, 1, 0, 0, 0
dcl_position v0
dcl_position o0
dcl_position1 o1.xyz
dcl_position2 o2.xyz
mul r0.xyz, c1.xyz, v0.yyy
mad r0.xyz, c0.xyz, v0.xxx, r0.xyz
mad o0.xyz, c2.xyz, v0.zzz, r0.xyz
mul r0.xyz, c1.xyz, v0.xxx
mad r0.xyz, c0.xyz, v0.yyy, r0.xyz
mad o1.xyz, c2.xyz, v0.zzz, r0.xyz
mul r0.xyz, c1.xyz, v0.xxx_abs
mad r0.xyz, c0.xyz, v0.yyy_abs, r0.xyz
mad o2.xyz, c2.xyz, v0.zzz_abs, r0.xyz
mov o0.w, c3.x
