vs_3_0
def c3, 4, 0, 0, 0
dcl_position v0
dcl_position o0
mul r0.xyz, c1.xyz, v0.yyy
mad r0.xyz, c0.xyz, v0.xxx, r0.xyz
mad o0.xyz, c2.xyz, v0.zzz, r0.xyz
mov o0.w, c3.x
