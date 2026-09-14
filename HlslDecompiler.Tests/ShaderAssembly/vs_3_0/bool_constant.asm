vs_3_0
def c5, -2, 0, 0, 0
dcl_position v0
dcl_color v1
dcl_position o0
dcl_color o1
mul r0.x, c4.x, v0.y
mad r0.y, r0.x, c5.x, v0.y
mov r0.xzw, v0.xzw
dp4 o0.x, r0, c0
dp4 o0.y, r0, c1
dp4 o0.z, r0, c2
dp4 o0.w, r0, c3
if b0
mov r0, v1
rep i0
add r0, r0, r0
endrep
mov o1, r0
else
mov o1, v1
endif
