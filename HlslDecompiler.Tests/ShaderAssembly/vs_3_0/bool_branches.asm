vs_3_0
dcl_position v0
dcl_color v1
dcl_position o0
dcl_color o1
mov r0.x, c4.x
mad r0, r0.x, c6, v0
mad r1.xyz, r0.xyz, c7.xxx, -r0.xyz
mad r0.xyz, c5.xxx, r1.xyz, r0.xyz
dp4 o0.x, r0, c0
dp4 o0.y, r0, c1
dp4 o0.z, r0, c2
dp4 o0.w, r0, c3
add r0, -v1.wzyx, v1
mad o1, c4.x, r0, v1.wzyx
