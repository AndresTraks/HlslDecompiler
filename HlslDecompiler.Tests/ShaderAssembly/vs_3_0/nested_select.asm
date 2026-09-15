vs_3_0
def c5, 0, 0, 0, 0
dcl_position v0
dcl_color v1
dcl_position o0
dcl_color o1
dp4 o0.x, v0, c0
dp4 o0.y, v0, c1
dp4 o0.z, v0, c2
dp4 o0.w, v0, c3
slt r0.xy, v1.xy, c5.xx
lrp r1.x, r0.y, c4.x, c4.y
add r0.y, r1.x, -c4.z
mad o1.x, r0.x, r0.y, c4.z
max r0.xy, c4.xy, v1.xy
min o1.y, r0.y, r0.x
sge r0.x, v1.z, c4.z
lrp r0.y, v1.w, c4.y, c4.x
mul o1.z, r0.y, r0.x
add r0.x, -v1.y, v1.x
add r0.y, c4.w, -v1.z
mul o1.w, r0.y, -r0.x
