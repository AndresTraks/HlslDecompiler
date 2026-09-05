vs_3_0
def c17, 4, 0, 0, 0
dcl_position v0
dcl_position o0
mov r0.x, c16.x
mul r0.x, r0.x, c17.x
mova a0.x, r0.x
dp4 o0.x, v0, c0[a0.x]
dp4 o0.y, v0, c1[a0.x]
dp4 o0.z, v0, c2[a0.x]
dp4 o0.w, v0, c3[a0.x]
