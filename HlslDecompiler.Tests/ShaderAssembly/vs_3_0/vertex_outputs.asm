vs_3_0
dcl_position v0
dcl_position o0
dcl_psize o1
dcl_fog o2.x
dcl_color o3
dp4 o0.x, v0, c0
dp4 o0.y, v0, c1
dp4 o0.w, v0, c3
dp4 r0.x, v0, c2
mov o0.z, r0.x
mov o2.x, r0.x
mov o1, c4.x
mov o3, c5
