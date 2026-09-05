ps_3_0
def c0, 5, 0, 1, 0
def c1, 3, 4, 1, 0
dcl_texcoord v0
dcl_2d s0
add r0.xy, c0.xx, v0.yz
if_lt -v0.y, c0.y
texldl r1, v0, s0
add r0.zw, r0.xy, r1.xy
else
add r2, v0, v0
texldl r1, r2, s0
add r0.zw, r0.xy, -r1.xy
endif
if_ge v0.y, c0.y
add r2, c0.z, v0
texldl r2, r2, s0
add r0.zw, r0.zw, r2.xy
else
mov r1.zw, c1.xy
mov r1.xy, c0.zy
endif
add r0.xy, r0.zw, r1.xy
texld r0, r0.xy, s0
add r0, r0, r1
add r1, r1, c1.zwxy
cmp oC0, -v0.x, r0, r1
