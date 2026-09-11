vs_3_0
def c5, 0.00999999978, 0, 10, 0
dcl_position v0
dcl_texcoord v1
dcl_2d s0
dcl_position o0
mov r0.zw, c5.yy
mov r1, v0
add r1, -r1, v1
mad r1, c4.x, r1, v0
mul r0.xy, r1.xz, c5.xx
texldl r0, r0, s0
mad r1.y, r0.x, c5.z, r1.y
dp4 o0.x, r1, c0
dp4 o0.y, r1, c1
dp4 o0.z, r1, c2
dp4 o0.w, r1, c3
